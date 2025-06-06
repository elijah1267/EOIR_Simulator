using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

using EOIR_Simulator.Model;
using EOIR_Simulator.Service;
using EOIR_Simulator.Utils; // RelayCommand / ObservableObject 등

namespace EOIR_Simulator.ViewModel
{
    public class MainViewModel : INotifyPropertyChanged
    {
        /* ────────────────── 서비스 ────────────────── */
        private readonly CommandSender _tcp;
        private readonly PacketReceiver _vRx;
        private readonly AngleReceiver _aRx;

        /* ────────────────── 하위 VM ────────────────── */
        public VideoVM Video { get; }
        public AngleVM Angle { get; }
        public ConnectionVM Connection { get; }

        /* ────────────────── 로그 ────────────────── */
        /// <summary>
        /// TCP 명령 전송 로그. ListBox.ItemsSource 에 바인딩됨
        /// </summary>
        public ObservableCollection<TcpCommandLog> TcpLogs { get; }
            = new ObservableCollection<TcpCommandLog>();

        /* ────────────────── 명령 ────────────────── */
        public ICommand MoveCommand { get; }
        public ICommand SetManualModeCommand { get; }

        public bool IsManualMode => Mode == ModeNum.Manual;
        public bool IsTcpConnected => _tcp.IsConnected;
        public string ConnectButtonText => IsTcpConnected ? "Disconnect" : "Connect";


        /* ────────────────── INotifyPropertyChanged ────────────────── */
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

        /* ────────────────── Mode ────────────────── */
        private ModeNum _mode = ModeNum.Manual;
        public ModeNum Mode
        {
            get => _mode;
            set
            {
                if (_mode == value) return;
                _mode = value;
                Raise();               // Mode
                Raise(nameof(IsManualMode));
                _ = SendModeAsync();
            }
        }

        private async Task SendModeAsync()
        {
            try { await _tcp.SendAsync(Mode, 0, 0); }
            catch { /* TODO: 예외 처리 및 사용자 알림 */ }
        }

        /* ────────────────── 전처리 표시 ────────────────── */

        private string _selectedPreprocessMode = "일반환경";
        public string SelectedPreprocessMode
        {
            get => _selectedPreprocessMode;
            set
            {
                if (_selectedPreprocessMode != value)
                {
                    _selectedPreprocessMode = value;
                    Raise();
                    Debug.WriteLine($"[UI] 영상 전처리 모드 선택: {value}");
                }
            }
        }

        /* ────────────────── 상태 표시 ────────────────── */
        public static string StateToStr(int state)
        {
            switch (state)
            {
                case 0: return "CHECKING";
                case 1: return "IDLE";
                case 2: return "RUNNING";
                default: return "UNKNOWN";
            }
        }

        public string StateColor
        {
            get
            {
                switch (State)
                {
                    case "CHECKING": return "Yellow";
                    case "RUNNING": return "LimeGreen";
                    case "IDLE": return "Gray";
                    default: return "Red";
                }
            }
        }

        private bool _tpuConnected;
        public bool TpuConnected
        {
            get => _tpuConnected;
            private set { if (_tpuConnected != value) { _tpuConnected = value; Raise(); } }
        }

        private bool _camConnected;
        public bool CamConnected
        {
            get => _camConnected;
            private set { if (_camConnected != value) { _camConnected = value; Raise(); } }
        }

        private string _state = "IDLE";
        public string State
        {
            get => _state;
            private set
            {
                if (_state != value)
                {
                    _state = value;
                    Raise();
                    Raise(nameof(StateColor));
                }
            }
        }

        /* ────────────────── 생성자 ────────────────── */
        public MainViewModel()
        {
            Debug.WriteLine("MainViewModel Created: " + GetHashCode());

            _tcp = new CommandSender("192.168.1.3", 9999);
            _vRx = new PacketReceiver(IcdConstants.UDP_PORT);
            _aRx = new AngleReceiver(9998);

            Video = new VideoVM(_vRx);
            Angle = new AngleVM(_aRx);
            Connection = new ConnectionVM(_tcp, _vRx, isConnected =>
            {
                TpuConnected = isConnected;
                CamConnected = isConnected;
            });

            /* ───── 명령 정의 ───── */
            SetManualModeCommand = new RelayCommand(_ =>
            {
                Mode = ModeNum.Manual;
                _tcp.SendAsync(ModeNum.Manual, 0, 0).ConfigureAwait(false);
            });

            MoveCommand = new RelayCommand(dirObj =>
            {
                if (!IsManualMode) return;
                var step = DirToStep(dirObj as string);
                _tcp.SendAsync(ModeNum.Manual, step.dx, step.dy).ConfigureAwait(false);
            });

            /* ───── TCP 이벤트 연결 ───── */
            _tcp.StateChanged += s =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Raise(nameof(IsTcpConnected));
                    Raise(nameof(ConnectButtonText));

                    if (s == TcpState.Connected)
                    {
                        Video.AcceptFrames = true;
                    }
                    else
                    {
                        State = "IDLE";
                        TpuConnected = false;
                        CamConnected = false;
                        Video.AcceptFrames = false;
                        Video.Clear();
                    }
                });
            };

            _tcp.StateReceived += (state, tpu, cam) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    State = StateToStr(state);
                    TpuConnected = tpu;
                    CamConnected = cam;


                    //TcpLogs.Insert(0, new TcpCommandLog(DateTime.Now, (ModeNum)Mode, 0, 0, 0));

                });
            };

            /* ───── TCP 명령 로그 이벤트 ───── */
            _tcp.CommandSent += log =>
            {
                //Application.Current.Dispatcher.Invoke(() => TcpLogs.Add(log));
                Application.Current.Dispatcher.Invoke(() => TcpLogs.Insert(0, log));
            };
        }

        /* ────────────────── 서비스 Start ────────────────── */
        public void StartServices()
        {
            _vRx?.Start();
            _aRx?.Start();
        }

        /* ────────────────── 유틸 ────────────────── */
        private static (sbyte dx, sbyte dy) DirToStep(string dir)
        {
            switch (dir)
            {
                case "Up": return (0, +10);
                case "Down": return (0, -10);
                case "Left": return (+10, 0);
                case "Right": return (-10, 0);
                default: return (0, 0);
            }
        }
    }
}
