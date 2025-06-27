
using System;
using System.Windows.Input;
using System.Timers;
using System.Windows.Media;
using EOIR_Simulator.Model;
using EOIR_Simulator.Service;
using EOIR_Simulator.Util;
using EOIR_Simulator.Radar;

namespace EOIR_Simulator.ViewModel
{
    public enum AppConnectionState
    {
        Initial,
        Connected,
        Running,
        StoppedAfterRun
    }

    public sealed class ConnectionVM : ObservableObject
    {
        private readonly TcpStateChannel _tcpState;
        private readonly TcpCmdChannel _tcpCmd;
        private readonly PacketReceiver _vrx;
        private readonly Action<bool> _setDeviceState;
        private readonly RadarProcessor _radarProcessor;
        private readonly VideoVM _video;

        private AppConnectionState _appState = AppConnectionState.Initial;

        public bool ShowCmdUdp => _appState == AppConnectionState.Running;

        private string _tcpStatus = "TCP : Disconnected";
        public string TcpStatus
        {
            get => _tcpStatus;
            private set
            {
                _tcpStatus = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(TcpStateColor));
                RaisePropertyChanged(nameof(ConnectButtonText));
                RaisePropertyChanged(nameof(TpuStateColor));
                RaisePropertyChanged(nameof(CamStateColor));
            }
        }

        private string _udpStatus = "UDP : Disconnected";
        public string UdpStatus
        {
            get => _udpStatus;
            private set
            {
                _udpStatus = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(UdpStateColor));
            }
        }

        private string _cmdStatus = "CMD : Disconnected";
        public string CmdStatus
        {
            get => _cmdStatus;
            private set
            {
                _cmdStatus = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(CmdStateColor));
            }
        }

        private bool _tpuConnected;
        public bool TpuConnected
        {
            get => _tpuConnected;
            private set
            {
                _tpuConnected = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(TpuStateColor));
            }
        }

        private bool _camConnected;
        public bool CamConnected
        {
            get => _camConnected;
            private set
            {
                _camConnected = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(CamStateColor));
            }
        }

        public Brush RadarStateColor
        {
            get
            {
                return (_radarProcessor.IsDataPortOpen && _radarProcessor.IsCliPortOpen)
                       ? Brushes.LimeGreen
                       : Brushes.Red;
            }
        }

        private int _stateCode;
        public int StateCode
        {
            get => _stateCode;
            private set
            {
                _stateCode = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(StateString));
                RaisePropertyChanged(nameof(StateColor));
            }
        }

        public string StateString
        {
            get
            {
                // ① TCP · CMD 모두 끊긴 ‘Initial(Disconnect)’ 상태이면 전용 문구
                if (_appState == AppConnectionState.Initial)
                    return "Connect 버튼을 눌러 상태를 확인하세요.";

                // ② 그 외에는 기존 StateCode → 문자열 매핑 사용
                return _stateCode.ToStateString();
            }
        }
        public Brush StateColor
        {
            get
            {
                // 연결 전 초기 상태: 항상 Gray
                if (_appState == AppConnectionState.Initial)
                    return Brushes.Gray;

                // 연결 후 장치 정상(IDLE) 상태: LimeGreen
                if (_stateCode == 1)
                    return Brushes.LimeGreen;

                // 장치 점검(CHECKING): Red
                if (_stateCode == 0)
                    return Brushes.Red;

                // 장치 실행(RUNNING): LimeGreen
                if (_stateCode == 2)
                    return Brushes.LimeGreen;

                // 그 외: Red
                return Brushes.Red;
            }
        }


        private byte _modeNum;
        public byte ModeNum
        {
            get => _modeNum;
            private set
            {
                _modeNum = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(ModeString));
            }
        }

        public string ModeString => ((ModeNum)_modeNum).ToEng();

        public Brush TcpStateColor => TcpStatus.Contains("Connected") ? Brushes.LimeGreen : Brushes.Red;
        public Brush CmdStateColor => CmdStatus.Contains("Connected") ? Brushes.LimeGreen : Brushes.Red;
        public Brush TpuStateColor => TcpStatus.Contains("Connected") ? (TpuConnected ? Brushes.LimeGreen : Brushes.Red) : Brushes.Gray;
        public Brush CamStateColor => TcpStatus.Contains("Connected") ? (CamConnected ? Brushes.LimeGreen : Brushes.Red) : Brushes.Gray;
        public Brush UdpStateColor => UdpStatus.Contains("Connected") ? Brushes.LimeGreen : Brushes.Red;

        public string RunButtonText => _tcpCmd.IsConnected ? "Stop" : "Run";
        public string ConnectButtonText => _tcpState.IsConnected ? "Disconnect" : "Connect";

        public bool IsConnectEnabled => _appState != AppConnectionState.Running || StateCode == 0;
        public bool IsRunEnabled =>
            (_appState == AppConnectionState.Connected ||
             _appState == AppConnectionState.Running ||
             _appState == AppConnectionState.StoppedAfterRun)
            && StateCode != 0;          // ← CHECKING 이면 false

        public bool AreOtherButtonsEnabled => _appState == AppConnectionState.Running;

        public ICommand ConnectCommand { get; }
        public ICommand RunCommand { get; }

        public event Action ConnectTextChanged;

        private DateTime _lastUdp = DateTime.MinValue;
        private readonly Timer _timer = new Timer(500);

        public ConnectionVM(TcpStateChannel tcpState,
                        TcpCmdChannel tcpCmd,
                        PacketReceiver vrx,
                        RadarProcessor radar,          // ★ RadarProcessor 주입
                        VideoVM video,
                        Action<bool> setDeviceState)
        {
            _tcpState = tcpState;
            _tcpCmd = tcpCmd;
            _vrx = vrx;
            _radarProcessor = radar ?? throw new ArgumentNullException(nameof(radar));
            _video = video;
            _setDeviceState = setDeviceState;

            _vrx.FrameArrived += _ =>
            {
                _lastUdp = DateTime.Now;
                UdpStatus = "UDP : Connected";
            };

            _timer.Elapsed += (s, e) =>
            {
                if ((DateTime.Now - _lastUdp).TotalSeconds > 1.5
                    && UdpStatus != "UDP : Disconnected")
                {
                    UdpStatus = "UDP : Disconnected";
                    _video.Clear();        // ★ 화면·객체 지우기
                }
                RaisePropertyChanged(nameof(RadarStateColor));
            };
            _timer.Start();

            _tcpCmd.StateChanged += connected =>
            {
                CmdStatus = connected ? "CMD : Connected" : "CMD : Disconnected";
                RaisePropertyChanged(nameof(RunButtonText));
            };

            ConnectCommand = new RelayCommand(async _ =>
            {
                if (_tcpState.IsConnected)
                {
                    _tcpState.Disconnect();
                    _tcpCmd.Disconnect();
                    TcpStatus = "TCP : Disconnected";
                    _lastUdp = DateTime.MinValue;
                    UdpStatus = "UDP : Disconnected";
                    CmdStatus = "CMD : Disconnected";
                    TpuConnected = false;
                    CamConnected = false;
                    _setDeviceState(false);
                    _video.Clear();
                    _radarProcessor.StopRadar();
                    _radarProcessor.ClearFov();
                    _radarProcessor.SetFovVisible(false);
                    _appState = AppConnectionState.Initial;
                    _video.StopCpuTimer();
                    _video.ResetFpsPlot();
                }
                else
                {
                    bool success = await _tcpState.ConnectAsync();
                    TcpStatus = success ? "TCP : Connected" : "TCP : Failed";
                    _setDeviceState(success);
                    _appState = success ? AppConnectionState.Connected : AppConnectionState.Initial;
                    if (success)
                    {
                        _radarProcessor.SetFovVisible(true);   // 연결 성공 ▶ FOV On
                        _video.StartCpuTimer();
                    }
                    else
                        _radarProcessor.SetFovVisible(false);  // 실패 ▶ FOV Off
                }
                UpdateSystemState();
                RaiseAllStateProperties();
                RaisePropertyChanged(nameof(RadarStateColor));
                ConnectTextChanged?.Invoke();
            });

            RunCommand = new RelayCommand(async _ =>
            {
                if (_tcpCmd.IsConnected)
                {
                    _tcpCmd.Disconnect();
                    _appState = AppConnectionState.StoppedAfterRun;
                    _radarProcessor.StopRadar();
                    _video.Clear();
                    _video.ResetFpsPlot();
                }
                else
                {
                    await _tcpCmd.ConnectAsync();
                    _appState = AppConnectionState.Running;
                    if (_radarProcessor != null)
                        _radarProcessor.StartRadar();
                }
                UpdateSystemState();
                RaiseAllStateProperties();
                RaisePropertyChanged(nameof(RadarStateColor));
            });
            UpdateSystemState();
            RaiseAllStateProperties();
        }

        private void UpdateSystemState()
        {
            bool deviceOK = TpuConnected && CamConnected;

            // ── ① RUNNING 중 장치 오류 → 자동 Stop ───────────────────────────
            if (_appState == AppConnectionState.Running && !deviceOK)
            {
                // Stop 처리
                if (_tcpCmd.IsConnected)
                    _tcpCmd.Disconnect();

                _radarProcessor.StopRadar();
                _appState = AppConnectionState.StoppedAfterRun;   // Run 종료 후 상태
                RaisePropertyChanged(nameof(RunButtonText));
            }

            // ── ② 상태표시(StateCode) 계산 ───────────────────────────────────
            switch (_appState)
            {
                case AppConnectionState.Initial:
                    StateCode = 1;                    // Gray
                    break;

                case AppConnectionState.Connected:
                    StateCode = deviceOK ? 1 : 0;     // IDLE / CHECKING
                    break;

                case AppConnectionState.Running:
                    StateCode = deviceOK ? 2 : 0;     // RUNNING / CHECKING
                    break;

                case AppConnectionState.StoppedAfterRun:
                    StateCode = deviceOK ? 1 : 0;     // IDLE / CHECKING
                    break;
            }

            // 버튼·색상 즉시 갱신
            RaisePropertyChanged(nameof(IsConnectEnabled));
            RaisePropertyChanged(nameof(IsRunEnabled));
            RaisePropertyChanged(nameof(StateString));
            RaisePropertyChanged(nameof(StateColor));
            RaisePropertyChanged(nameof(ShowCmdUdp));
        }

        private void RaiseAllStateProperties()
        {
            RaisePropertyChanged(nameof(IsConnectEnabled));
            RaisePropertyChanged(nameof(IsRunEnabled));
            RaisePropertyChanged(nameof(AreOtherButtonsEnabled));
            RaisePropertyChanged(nameof(RunButtonText));
            RaisePropertyChanged(nameof(ConnectButtonText));
            RaisePropertyChanged(nameof(StateString));
            RaisePropertyChanged(nameof(ShowCmdUdp));
        }

        public void UpdateState(StatePacket packet)
        {
            StateCode = packet.State;
            TpuConnected = packet.Tpu;
            CamConnected = packet.Cam;
            ModeNum = (byte)packet.Mode;

            UpdateSystemState();
        }
    }
}
