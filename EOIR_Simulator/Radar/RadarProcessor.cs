using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

using EOIR_Simulator.Service;

namespace EOIR_Simulator.Radar
{
    public class RadarProcessor
    {
        private readonly Canvas _radarOverlay;
        private readonly TlvParser _parser = new TlvParser();
        private SerialPort _dataPort;
        private SerialPort _cliPort;
        private readonly byte[] _magicWord = new byte[] { 2, 1, 4, 3, 6, 5, 8, 7 };
        private byte[] _buffer = new byte[65536];
        private int _bufferIndex = 0;
        private readonly Dispatcher _dispatcher;

        private Timer _portMonitorTimer;
        private string _dataPortName = "COM4";
        private int _dataPortBaud = 921600;
        private string _cliPortName = "COM3";
        private int _cliPortBaud = 115200;

        private const string ConfigFileName = "xwr16xx_profile_2025_06_12T12_43_17_656.cfg";

        private System.Windows.Shapes.Path _currentFovPath;
        private double _yawDeg = 90.0;   // 90° = 정면
        private double _halfFov = 40.0;   // ±40° (총 80°)

        private const double CanvasWidth = 300.0;
        private const double CanvasHeight = 200.0;

        private bool _fovEnabled = true;
        public bool IsDataPortOpen => _dataPort != null && _dataPort.IsOpen;
        public bool IsCliPortOpen => _cliPort != null && _cliPort.IsOpen;

        public double YawDeg
        {
            get { return _yawDeg; }
            set
            {
                _yawDeg = value;
                RedrawFov();              // 값이 바뀔 때마다 즉시 화면 갱신
            }
        }

        public RadarProcessor(Canvas radarOverlay, Dispatcher dispatcher)
        {
            _radarOverlay = radarOverlay;
            _dispatcher = dispatcher;

            DrawRadarGrid();
        }

        public bool SetupSerial(string portName, int baudRate)
        {
            try
            {
                if (_dataPort != null && _dataPort.IsOpen) return true;

                _dataPortName = portName;
                _dataPortBaud = baudRate;

                _dataPort = new SerialPort(portName, baudRate);
                _dataPort.DataReceived += DataPort_DataReceived;
                _dataPort.Open();
                return true;
            }
            catch (Exception ex)                  // IOException, UnauthorizedAccess…
            {
                _dataPort = null;
                System.Diagnostics.Debug.WriteLine($"[Radar] DATA포트 {portName} 열기 실패: {ex.Message}");
                return false;
            }
        }

        public bool SetupCli(string portName, int baudRate)
        {
            try
            {
                if (_cliPort != null && _cliPort.IsOpen) return true;
                _cliPort = new SerialPort(portName, baudRate);
                _cliPort.Open();
                return true;
            }
            catch (Exception ex)
            {
                _cliPort = null;
                System.Diagnostics.Debug.WriteLine(
                    $"[Radar] CLI포트 {portName} 열기 실패: {ex.Message}");
                return false;
            }
        }

        public void StartRadar()
        {
            StopRadar();
            if (_cliPort == null || !_cliPort.IsOpen)
            {
                bool cliOpened = SetupCli(_cliPortName, _cliPortBaud);
                if (!cliOpened || _cliPort == null || !_cliPort.IsOpen)
                {
                    //System.Diagnostics.Debug.WriteLine("[Radar] CLI 포트를 열지 못해 센서 시작 실패");
                    return; // 여기서 안전하게 종료
                }
            }

            try
            {
                foreach (string line in File.ReadAllLines(ConfigFileName))
                {
                    _cliPort.WriteLine(line);
                    Thread.Sleep(10);
                }

                _cliPort.WriteLine("sensorStart");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Radar] sensorStart 실패: {ex.Message}");
            }
        }

        public void StopRadar()
        {
            if (_cliPort != null && _cliPort.IsOpen)
                _cliPort.WriteLine("sensorStop");

            ClearDetections();
        }

        public void StartMonitoringPorts()
        {
            _portMonitorTimer = new Timer(_ =>
            {
                try
                {
                    if (_dataPort == null || !_dataPort.IsOpen)
                        SetupSerial(_dataPortName, _dataPortBaud);

                    if (_cliPort == null || !_cliPort.IsOpen)
                        SetupCli(_cliPortName, _cliPortBaud);
                }
                catch { /* 예외 무시 후 재시도 */ }

            }, null, 0, 2000); // 2초마다 확인
        }

        public void StopMonitoringPorts()
        {
            _portMonitorTimer?.Dispose();
            _portMonitorTimer = null;
        }


        private void DataPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            int bytesToRead = _dataPort.BytesToRead;
            byte[] readBuffer = new byte[bytesToRead];
            _dataPort.Read(readBuffer, 0, bytesToRead);

            Array.Copy(readBuffer, 0, _buffer, _bufferIndex, bytesToRead);
            _bufferIndex += bytesToRead;

            ParseBuffer();
        }

        private void ParseBuffer()
        {
            int startIdx = FindMagicWord(_buffer, _bufferIndex);
            if (startIdx >= 0 && (_bufferIndex - startIdx) >= 52)
            {
                byte[] packet = _buffer.Skip(startIdx).ToArray();
                var detObj = _parser.Parse(packet);

                if (detObj != null)
                {
                    // UI 스레드가 이미 종료되었으면 그리기 생략
                    if (!_dispatcher.HasShutdownStarted && !_dispatcher.HasShutdownFinished)
                    {
                        // ▲ Render 우선순위로 비동기 실행 (대기·예외 없음)
                        _dispatcher.BeginInvoke(
                            new Action(() => DrawDetections(detObj)),
                            System.Windows.Threading.DispatcherPriority.Render);
                    }
                }

                _bufferIndex = 0;   // 버퍼 리셋
            }
        }

        private int FindMagicWord(byte[] buffer, int length)
        {
            for (int i = 0; i <= length - _magicWord.Length; i++)
            {
                if (_magicWord.SequenceEqual(buffer.Skip(i).Take(_magicWord.Length)))
                    return i;
            }
            return -1;
        }

        private void ClearDetections()
        {
            for (int i = _radarOverlay.Children.Count - 1; i >= 0; i--)
            {
                var fe = _radarOverlay.Children[i] as FrameworkElement;
                if (fe != null && (fe.Tag as string) == "detection")
                    _radarOverlay.Children.RemoveAt(i);
            }
        }

        public void ClearFov()
        {
            if (_currentFovPath != null)
            {
                _radarOverlay.Children.Remove(_currentFovPath);
                _currentFovPath = null;
            }
        }

        public void SetFovVisible(bool on)
        {
            _fovEnabled = on;
            if (!on) ClearFov();
            else RedrawFov();
        }

        private void DrawDetections(DetectionObject detObj)
        {
            ClearDetections();

            List<Point> canvasPoints = new List<Point>();

            foreach (var (x, y) in detObj.Points)
            {
                double canvasX = ((x + 5) / 10.0) * CanvasWidth;
                double canvasY = CanvasHeight - ((y / 10.0) * CanvasHeight); ;

                var ellipse = new Ellipse
                {
                    Width = 6,
                    Height = 6,
                    Fill = Brushes.LimeGreen,
                    Tag = "detection"
                };

                Canvas.SetLeft(ellipse, canvasX - 3);
                Canvas.SetTop(ellipse, canvasY - 3);
                _radarOverlay.Children.Add(ellipse);

                canvasPoints.Add(new Point(canvasX, canvasY));
            }

            // 클러스터링 (DBSCAN)
            var clusters = DbscanCluster(canvasPoints, eps: 20, minPts: 3);
            foreach (var cluster in clusters)
            {
                double minX = cluster.Min(p => p.X);
                double maxX = cluster.Max(p => p.X);
                double minY = cluster.Min(p => p.Y);
                double maxY = cluster.Max(p => p.Y);

                var rect = new Rectangle
                {
                    Width = maxX - minX,
                    Height = maxY - minY,
                    Stroke = Brushes.Red,
                    StrokeThickness = 2,
                    Tag = "detection"
                };

                Canvas.SetLeft(rect, minX);
                Canvas.SetTop(rect, minY);
                _radarOverlay.Children.Add(rect);
            }
        }

        private void DrawRadarGrid()
        {
            _radarOverlay.Children.Clear();

            // 캔버스 하단 중심이 (0 m, 0 m)
            double centerX = CanvasWidth / 2.0;   // 150 px
            double centerY = CanvasHeight;         // 200 px
            double pxPerMeter = CanvasHeight / 10.0;   // 20 px = 1 m

            // ── 1) 동심 반원 : 2.5 / 5 / 7.5 / 10 m ───────────────────────────
            foreach (double rMeter in new[] { 2.5, 5.0, 7.5, 10.0 })
            {
                double rPx = rMeter * pxPerMeter;

                // 반원 Path (Clockwise)
                var fig = new PathFigure { StartPoint = new Point(centerX - rPx, centerY) };
                fig.Segments.Add(new ArcSegment
                {
                    Point = new Point(centerX + rPx, centerY),
                    Size = new Size(rPx, rPx),
                    IsLargeArc = false,
                    SweepDirection = SweepDirection.Clockwise
                });
                _radarOverlay.Children.Add(new System.Windows.Shapes.Path
                {
                    Data = new PathGeometry(new[] { fig }),
                    Stroke = Brushes.LimeGreen,
                    StrokeThickness = 1.2,
                    Opacity = 0.75
                });

                // ▼ 레이블 위치 ─  반지름 rPx 지점에서 30° 우측 위 방향으로 12 px 안쪽
                const double labelAngleDeg = -30.0;                // 0°는 정-위, 음수는 우측
                double rad = labelAngleDeg * Math.PI / 180.0;
                double labelX = centerX + (rPx - 12) * Math.Sin(rad);
                double labelY = centerY - (rPx - 12) * Math.Cos(rad);

                var txt = new TextBlock
                {
                    Text = $"{rMeter:0.#} m",
                    Foreground = Brushes.LimeGreen,
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    Opacity = 0.85
                };
                txt.Loaded += (s, e) =>
                {
                    // 가운데 정렬 보정
                    Canvas.SetLeft(txt, labelX - txt.ActualWidth / 2);
                    Canvas.SetTop(txt, labelY - txt.ActualHeight / 2);
                };
                _radarOverlay.Children.Add(txt);
            }

            // ── 2) 방위선 : -90°~+90° 를 30° 간격으로 ────────────────────────
            for (int deg = -90; deg <= 90; deg += 30)
            {
                double rad = deg * Math.PI / 180.0;
                double x = centerX + (10.0 * Math.Sin(rad)) * pxPerMeter; // 10 m 끝까지
                double y = centerY - (10.0 * Math.Cos(rad)) * pxPerMeter;

                var line = new Line
                {
                    X1 = centerX,
                    Y1 = centerY,
                    X2 = x,
                    Y2 = y,
                    Stroke = Brushes.LimeGreen,
                    StrokeThickness = 1.0,
                    Opacity = 0.75
                };
                _radarOverlay.Children.Add(line);
            }
        }

        private void RedrawFov()
        {
            if (!_fovEnabled) return;

            // 이전 Path 제거
            if (_currentFovPath != null)
                _radarOverlay.Children.Remove(_currentFovPath);

            double pxPerMeter = CanvasHeight / 10.0;
            double cX = CanvasWidth / 2.0;
            double cY = CanvasHeight;
            double range = 10.0;

            double radL = (_yawDeg - _halfFov) * Math.PI / 180.0;
            double radR = (_yawDeg + _halfFov) * Math.PI / 180.0;

            Point pL = new Point(cX + range * Math.Cos(radL) * pxPerMeter,
                                 cY - range * Math.Sin(radL) * pxPerMeter);
            Point pR = new Point(cX + range * Math.Cos(radR) * pxPerMeter,
                                 cY - range * Math.Sin(radR) * pxPerMeter);

            PathFigure fig = new PathFigure { StartPoint = new Point(cX, cY) };
            fig.Segments.Add(new LineSegment(pL, true));
            fig.Segments.Add(new ArcSegment
            {
                Point = pR,
                Size = new Size(range * pxPerMeter, range * pxPerMeter),
                IsLargeArc = false,
                SweepDirection = SweepDirection.Counterclockwise   // 위쪽 볼록
            });
            fig.IsClosed = true;

            PathGeometry geom = new PathGeometry();
            geom.Figures.Add(fig);

            System.Windows.Shapes.Path path = new System.Windows.Shapes.Path();
            path.Data = geom;
            path.Fill = new SolidColorBrush(Color.FromArgb(40, 0, 255, 0));
            path.Stroke = Brushes.LimeGreen;
            path.StrokeThickness = 1.4;
            path.IsHitTestVisible = false;

            _radarOverlay.Children.Insert(0, path);   // 맨뒤 레이어
            _currentFovPath = path;
        }

        // ▼ 카메라 Yaw 값이 바뀔 때마다 호출해 주면 레이더 화면이 즉시 갱신됩니다.
        public void UpdateCameraYaw(double yawDeg, double halfFovDeg)
        {
            _yawDeg = yawDeg;
            _halfFov = halfFovDeg;

            // UI 쓰레드에서 FOV 재그리기
            _dispatcher.Invoke(() =>
            {
                RedrawFov();          // 새 부채꼴만 다시 그림
            });
        }

        // DBSCAN 클러스터링 (기존 유지)
        private List<List<Point>> DbscanCluster(List<Point> points, double eps, int minPts)
        {
            var clusters = new List<List<Point>>();
            var visited = new HashSet<Point>();
            var noise = new HashSet<Point>();

            foreach (var p in points)
            {
                if (visited.Contains(p))
                    continue;

                visited.Add(p);
                var neighbors = GetNeighbors(points, p, eps);

                if (neighbors.Count < minPts)
                {
                    noise.Add(p);
                }
                else
                {
                    var cluster = new List<Point>();
                    clusters.Add(cluster);
                    ExpandCluster(p, neighbors, cluster, points, visited, eps, minPts);
                }
            }

            return clusters;
        }

        private void ExpandCluster(Point p, List<Point> neighbors, List<Point> cluster, List<Point> points, HashSet<Point> visited, double eps, int minPts)
        {
            cluster.Add(p);

            for (int i = 0; i < neighbors.Count; i++)
            {
                var np = neighbors[i];
                if (!visited.Contains(np))
                {
                    visited.Add(np);
                    var npNeighbors = GetNeighbors(points, np, eps);
                    if (npNeighbors.Count >= minPts)
                        neighbors.AddRange(npNeighbors.Where(n => !neighbors.Contains(n)));
                }

                if (!cluster.Contains(np))
                    cluster.Add(np);
            }
        }

        private List<Point> GetNeighbors(List<Point> points, Point center, double eps)
        {
            return points.Where(p => (p - center).Length <= eps).ToList();
        }
    }
}