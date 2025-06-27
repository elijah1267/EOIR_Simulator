using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using EOIR_Simulator.Model;
using EOIR_Simulator.Service;
using EOIR_Simulator.Util;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.Annotations;

namespace EOIR_Simulator.ViewModel
{
    static class DtExt { public static double TotalMs(this TimeSpan ts) => ts.TotalMilliseconds; }
    public sealed class VideoVM : ObservableObject
    {
        private readonly ObservableCollection<LoggerEntry> _sharedLogs;
        public ObservableCollection<LoggerEntry> TcpLogs => _sharedLogs;

        /* ───── CpuTemp 그래프용 필드 ───── */
        public PlotModel CpuTempPlot { get; }     // XAML 바인딩 대상
        private readonly LineSeries _cpuSeries;
        private int _cpuIndex = 0;

        private float _latestCpuTemp = 0f;
        private readonly System.Windows.Threading.DispatcherTimer _cpuTimer;

        /* ───── FPS 그래프용 필드 ───── */
        public PlotModel FpsPlot { get; }               // XAML 바인딩 대상
        private readonly LineSeries _fpsSeries;
        private int _secIndex;                          // x축 인덱스(초)
        private TextAnnotation _lastCpuLabel;

        /* ───── FPS 측정용 ───── */
        private readonly Stopwatch _fpsSw = Stopwatch.StartNew();
        private int _fpsCounter;
        private int _lastFps = 0;
        private TextAnnotation _lastLabel;

        /* ───── 패킷 수신 ───── */
        private readonly PacketReceiver _rx;
        private readonly System.Windows.Threading.Dispatcher _ui =
            Application.Current.Dispatcher;

        public bool AcceptFrames { get; set; } = true;

        /* ───── 캡처 관련 ───── */
        public ICommand CaptureCommand { get; }
        private static readonly string _captureDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                "EOIR_Captures");

        /* ───── latency 측정 ───── */
        private double _latency;
        public double Latency
        {
            get => _latency;
            private set { _latency = value; RaisePropertyChanged(); }
        }

        /* ───── 상태 표시 ───── */
        private BitmapSource _currentFrame;
        public BitmapSource CurrentFrame
        {
            get => _currentFrame;
            private set { _currentFrame = value; RaisePropertyChanged(); }
        }

        public ObservableCollection<ObjectInfo> Objects { get; }
            = new ObservableCollection<ObjectInfo>();

        public int LastFps
        {
            get => _lastFps;
            private set { _lastFps = value; RaisePropertyChanged(); }
        }

        public VideoVM(PacketReceiver rx, TcpStateChannel tcp, ObservableCollection<LoggerEntry> sharedLogs)
        {
            _sharedLogs = sharedLogs;
            _rx = rx;
            _rx.FrameArrived += OnFrame;
            tcp.StateReceived += OnStateReceived;

            /* ① PlotModel 구성 */
            FpsPlot = new PlotModel
            {
                IsLegendVisible = false,              // 범례 끔
                Background = OxyColors.Transparent
            };
            FpsPlot.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Minimum = 10,
                Maximum = 30,      // FPS 범위
                MajorStep = 10,
                MinorStep = 5,
                Title = "FPS"
            });
            FpsPlot.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                IsPanEnabled = false,
                IsZoomEnabled = false,
                Minimum = 0,
                Maximum = 60,
                MajorGridlineStyle = LineStyle.None,
                MinorGridlineStyle = LineStyle.None,
                //Title = "Time(sec)"
            });

            _fpsSeries = new LineSeries
            {
                StrokeThickness = 2,
                MarkerType = MarkerType.None // 점 숨김
            };
            FpsPlot.Series.Add(_fpsSeries);

            // ─── CPU Temp Plot 구성 ───
            CpuTempPlot = new PlotModel
            {
                IsLegendVisible = false,
                Background = OxyColors.Transparent
            };
            CpuTempPlot.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Minimum = 50,      // 예시: 20~90℃
                Maximum = 60,
                Title = "Temp"
            });
            CpuTempPlot.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Minimum = 0,
                Maximum = 60,
                IsPanEnabled = false,
                IsZoomEnabled = false,
                MajorGridlineStyle = LineStyle.None,
                MinorGridlineStyle = LineStyle.None,
                //Title = "Time (sec)"
            });

            _cpuSeries = new LineSeries
            {
                StrokeThickness = 2,
                MarkerType = MarkerType.None
            };
            CpuTempPlot.Series.Add(_cpuSeries);

            _cpuTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _cpuTimer.Tick += (s, e) => DrawCpuTemp();

            /* 캡처 커맨드 */
            CaptureCommand = new RelayCommand(
                _ => SaveCurrentFrame(),
                _ => CurrentFrame != null);
        }

        public void StartCpuTimer()
        {
            _cpuIndex = 0;
            _cpuSeries.Points.Clear();
            CpuTempPlot.Annotations.Clear();
            _cpuTimer.Start();
        }

        public void StopCpuTimer()
        {
            _cpuTimer.Stop();
            _cpuIndex = 0;
            _cpuSeries.Points.Clear();
            CpuTempPlot.Annotations.Clear();
            CpuTempPlot.InvalidatePlot(true);
        }

        public void ResetFpsPlot()
        {
            _secIndex = 0;
            _fpsSeries.Points.Clear();
            FpsPlot.Annotations.Clear();
            FpsPlot.InvalidatePlot(true);
        }

        /* ───── 프레임 수신 ───── */
        private void OnFrame(FramePacket fp)
        {
            if (!AcceptFrames) return;
            _fpsCounter++;
            //DateTime vmEnterUtc = DateTime.Now;

            Task.Run(() =>
            {
                //DateTime decodeStartUtc = DateTime.Now;
                BitmapImage bmp;
                try
                {
                    using (var ms = new MemoryStream(fp.JpegBytes))
                    {
                        bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.StreamSource = ms;
                        bmp.EndInit();
                        bmp.Freeze();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("[VideoVM] JPEG decode error: " + ex.Message);
                    return;
                }
                //DateTime decodeDoneUtc = DateTime.Now;

                _ui.BeginInvoke(new Action(() =>
                {
                    //DateTime uiDispatchUtc = DateTime.Now;
                    CurrentFrame = bmp;
                    //DateTime renderUtc = DateTime.Now;

                //    Debug.WriteLine(
                //$"[TRACE F{fp.FrameId}] " +
                //$"net:{(fp.RecvDoneUtc - fp.RecvStartUtc).TotalMs()}ms | " +
                //$"assemble:{(fp.AssembleUtc - fp.RecvDoneUtc).TotalMs()}ms | " +
                //$"toVM:{(vmEnterUtc - fp.AssembleUtc).TotalMs()}ms | " +
                //$"decode:{(decodeDoneUtc - decodeStartUtc).TotalMs()}ms | " +
                //$"dispatch:{(uiDispatchUtc - decodeDoneUtc).TotalMs()}ms | " +
                //$"render:{(renderUtc - uiDispatchUtc).TotalMs()}ms | " +
                //$"TOTAL:{(renderUtc - fp.RecvStartUtc).TotalMs()}ms");

                    //latency 측정
                    //try
                    //{
                    //    if (DateTime.TryParseExact(fp.TimeStamp,
                    //                                "yyyy-MM-ddTHH:mm:ss.fff",
                    //                                System.Globalization.CultureInfo.InvariantCulture,
                    //                                System.Globalization.DateTimeStyles.None,
                    //                                out DateTime parsedTime))
                    //    {
                    //        //var renderTime = DateTime.Now;
                    //        var latencyMs = (renderTime - parsedTime).TotalMilliseconds;
                    //        Latency = latencyMs;
                    //        //Debug.WriteLine($"[Latency] Frame {fp.FrameId}: {latencyMs:F1} ms");
                    //    }
                    //}
                    //catch (Exception ex)
                    //{
                    //    Debug.WriteLine("[Latency] ⛔ 타임스탬프 파싱 오류: " + ex.Message);
                    //}

                    Objects.Clear();
                    foreach (var o in fp.Objects)
                        Objects.Add(o);
                }));

            });

            // 1 초마다 FPS 확정
            if (_fpsSw.ElapsedMilliseconds >= 1000)
            {
                int fps = _fpsCounter;
                _fpsCounter = 0;
                _fpsSw.Restart();

                _ui.BeginInvoke(new Action(() =>
                {
                    LastFps = fps;                        // 텍스트 표시
                    //Debug.WriteLine("[FPS] : "+LastFps);

                    /* ③ 그래프 데이터 추가 */
                    int x = _secIndex++;                         // 현재 x 좌표
                    _fpsSeries.Points.Add(new DataPoint(x, fps)); // ← 중복 ++ 제거
                    if (_fpsSeries.Points.Count > 60)
                        _fpsSeries.Points.RemoveAt(0);

                    // ─── 이전 레이블 제거 ───
                    if (_lastLabel != null)
                        FpsPlot.Annotations.Remove(_lastLabel);

                    // ─── 새 레이블 생성 & 보관 ───
                    _lastLabel = new TextAnnotation
                    {
                        Text = fps.ToString(),
                        TextPosition = new DataPoint(x, fps),
                        TextHorizontalAlignment = OxyPlot.HorizontalAlignment.Center,
                        TextVerticalAlignment = OxyPlot.VerticalAlignment.Bottom,
                        Stroke = OxyColors.Transparent
                    };
                    FpsPlot.Annotations.Add(_lastLabel);

                    // x축 범위 스크롤
                    var xAxis = (LinearAxis)FpsPlot.Axes[1];
                    xAxis.Minimum = Math.Max(0, _secIndex - 60);
                    xAxis.Maximum = xAxis.Minimum + 60;

                    FpsPlot.InvalidatePlot(true);         // 즉시 재그리기
                }));
            }
        }

        void OnStateReceived(StatePacket pkt)
        {
            _latestCpuTemp = pkt.CpuTemp;
        }

        private void DrawCpuTemp()
        {
            int x = _cpuIndex++;
            float temp = _latestCpuTemp;

            _cpuSeries.Points.Add(new DataPoint(x, temp));
            if (_cpuSeries.Points.Count > 60)
                _cpuSeries.Points.RemoveAt(0);

            if (_lastCpuLabel != null)
                CpuTempPlot.Annotations.Remove(_lastCpuLabel);

            _lastCpuLabel = new TextAnnotation
            {
                Text = temp.ToString("F1"),
                TextPosition = new DataPoint(x, temp),
                TextHorizontalAlignment = OxyPlot.HorizontalAlignment.Center,
                TextVerticalAlignment = OxyPlot.VerticalAlignment.Bottom,
                Stroke = OxyColors.Transparent
            };
            CpuTempPlot.Annotations.Add(_lastCpuLabel);

            var xAxis = (LinearAxis)CpuTempPlot.Axes[1];
            xAxis.Minimum = Math.Max(0, _cpuIndex - 60);
            xAxis.Maximum = xAxis.Minimum + 60;

            CpuTempPlot.InvalidatePlot(true);
        }

        /* ───── 캡처 저장 ───── */
        private void SaveCurrentFrame()
        {
            var frame = CurrentFrame;
            if (frame == null) return;

            try
            {
                Directory.CreateDirectory(_captureDir);

                string fileName = $"capture_{DateTime.Now:yyyyMMdd_HHmmssfff}.png";
                string fullPath = Path.Combine(_captureDir, fileName);

                using (var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write))
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(frame));
                    encoder.Save(fs);
                }

                //Debug.WriteLine("[VideoVM] ✅ 이미지 저장: " + fullPath);

                //Console.WriteLine("VideoVM screen capture");

                string text = "Screen Captured";

                _ui.BeginInvoke(new Action(() =>
                {
                    TcpLogs.Insert(0, new LoggerEntry(DateTime.Now, "[CAP]", text));
                }));

            }
            catch (Exception ex)
            {
                Debug.WriteLine("[VideoVM] ❌ 이미지 저장 오류: " + ex.Message);
            }
        }

        public void Clear()
        {
            CurrentFrame = null;
            Objects.Clear();
        }
    }
}
