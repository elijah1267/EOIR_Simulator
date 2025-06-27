using System;
using System.Linq;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

using EOIR_Simulator.Model;
using EOIR_Simulator.Service;
using EOIR_Simulator.Util;
using System.Windows.Controls;
using EOIR_Simulator.Radar;
using System.Collections.Generic;
using System.Media;
using System.Numerics;

namespace EOIR_Simulator.ViewModel
{
    public class Tick
    {
        public int Deg { get; set; }   // 0~180
        public int Size { get; set; }   // 15 / 10 / 5
        public bool ShowLabel { get; set; }
    }


    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly Dispatcher _uiDispatcher;
        private readonly TcpCmdChannel _cmdTcp;
        private readonly TcpStateChannel _stateTcp;
        private readonly PacketReceiver _vRx;
        private readonly MediaPlayer _alertPlayer = new MediaPlayer();

        private readonly Dictionary<byte, ObjectInfo> _dict = new Dictionary<byte, ObjectInfo>();
        private readonly Dictionary<byte, DateTime> _lastSeen = new Dictionary<byte, DateTime>();

        private static readonly TimeSpan _keepAlive = TimeSpan.FromSeconds(1.0);

        private RelayCommand _trackCommand;

        private byte? _currentTrackId;
        private int _lostFrames;
        private readonly int _lostThreshold = 30;

        public VideoVM Video { get; }
        public AngleVM Angle { get; }
        public ConnectionVM Connection { get; }

        public ObservableCollection<LoggerEntry> TcpLogs { get; } = new ObservableCollection<LoggerEntry>();
        public ObservableCollection<ObjectInfo> Detections { get; } = new ObservableCollection<ObjectInfo>();
        public ObservableCollection<Tick> YawTicks { get; } = new ObservableCollection<Tick>();
        public ObservableCollection<Tick> PitchTicks { get; } = new ObservableCollection<Tick>();

        public ICommand SetManualModeCommand { get; private set; }
        public ICommand DirectionCommand { get; private set; }
        public ICommand SetEOCamCommand { get; private set; }
        public ICommand SetIRCamCommand { get; private set; }
        public ICommand SendPrepCommand { get; private set; }
        public ICommand SendTrackCommand { get; private set; }
        public ICommand SendInitCommand { get; private set; }

        public bool PrepEdge { get; set; }
        public bool PrepContrast { get; set; }
        public bool PrepDehazing { get; set; }

        public bool IsManualMode => Mode == ModeNum.Manual;
        public string ModeText => Mode.ToEng();

        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

        public string ConnectionStateText => Connection.ConnectButtonText;
        public ICommand ConnectionCommand => Connection.ConnectCommand;
        public ICommand RunCommand => Connection.RunCommand;

        private ModeNum _mode = ModeNum.Manual;
        public ModeNum Mode
        {
            get => _mode;
            set
            {
                if (_mode == value) return;
                _mode = value;
                Raise();
                Raise(nameof(IsManualMode));

                // Manual 모드로 전환될 때 추적 상태 초기화
                if (_mode == ModeNum.Manual || _mode == ModeNum.Scan)
                {
                    _currentTrackId = null;
                    _lostFrames = 0;
                }

                _ = LogAndSendCommandAsync((byte)CmdFlag.ModeNum, (byte)Mode);
            }
        }

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
                    //Debug.WriteLine($"[UI] 영상 전처리 모드 선택: {value}");
                }
            }
        }

        private ObjectInfo _selectedObj;
        public ObjectInfo SelectedObj
        {
            get => _selectedObj;
            set
            {
                if (_selectedObj == value) return;
                _selectedObj = value;
                Raise();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public MainViewModel(RadarProcessor radar)
        {
            //Debug.WriteLine("MainViewModel Created: " + GetHashCode());

            _uiDispatcher = Dispatcher.CurrentDispatcher;
            _cmdTcp = new TcpCmdChannel("192.168.1.3", 9999);
            _stateTcp = new TcpStateChannel("192.168.1.3", 9000);
            _vRx = new PacketReceiver(IcdConstants.UDP_PORT);

            Video = new VideoVM(_vRx, _stateTcp, TcpLogs);
            Angle = new AngleVM(_vRx, _stateTcp, radar);
            Connection = new ConnectionVM(_stateTcp, _cmdTcp, _vRx, radar, Video, _ => { });

            _alertPlayer.Open(new Uri("pack://siteoforigin:,,,/alert.mp3"));
            _alertPlayer.Volume = 1.0;

            Connection.ConnectTextChanged += () =>
            {
                Raise(nameof(ConnectionStateText));
            };

            InitializeCommands();
            SubscribeEvents();
            _vRx.FrameArrived += OnFrameArrived;
            BuildTicks();
        }

        private void InitializeCommands()
        {
            SetManualModeCommand = new RelayCommand(_ => SetManualMode());
            DirectionCommand = new RelayCommand(param => HandleArrowKey(param));
            SetEOCamCommand = new RelayCommand(_ => SendCamCommand(CamType.EO));
            SetIRCamCommand = new RelayCommand(_ => SendCamCommand(CamType.IR));
            SendPrepCommand = new RelayCommand(_ => SendPrepOptions());
            //SendTrackCommand = new RelayCommand(_ => LogAndSendCommandAsync((byte)CmdFlag.Track, 0));
            _trackCommand = new RelayCommand(_ => SendTrack(),
                                 _ => SelectedObj != null);

            SendTrackCommand = _trackCommand;
            SendInitCommand = new RelayCommand(_ => ResetMotorCommand());
        }

        private void SetManualMode()
        {
            Mode = ModeNum.Manual;
            _currentTrackId = null;                     // ★ 추적 대상 해제
            _lostFrames = 0;

            _ = LogAndSendCommandAsync((byte)CmdFlag.ModeNum,
                                       (byte)ModeNum.Manual);
        }

        private void HandleArrowKey(object param)
        {
            var dir = param as string;
            if (!IsManualMode || dir == null) return;

            byte flag = (byte)CmdFlag.MoveMotor;
            byte cmd = 0;

            switch (dir)
            {
                case "Down": cmd = 0b10; break; // Pitch CW
                case "Up": cmd = 0b11; break; // Pitch CCW
                case "Left": cmd = 0b01; break; // Yaw CCW
                case "Right": cmd = 0b00; break; // Yaw CW
                default: return;
            }

            _ = LogAndSendCommandAsync(flag, cmd); // 여기서만 1회 전송 (로그 포함)
        }


        private void SendCamCommand(CamType cam)
        {
            _ = LogAndSendCommandAsync((byte)CmdFlag.CamNum, (byte)cam);
        }

        private void SendPrepOptions()
        {
            byte mask = 0;
            if (PrepEdge) mask |= 0b00000001;
            if (PrepContrast) mask |= 0b00000010;
            if (PrepDehazing) mask |= 0b00000100;

            _ = LogAndSendCommandAsync((byte)CmdFlag.PrepOpt, mask);
        }

        private void ResetMotorCommand()
        {
            byte cmd = 0;
            _ = LogAndSendCommandAsync((byte)CmdFlag.InitMotor, cmd);
        }

        private async Task LogAndSendCommandAsync(byte flag, byte cmd)
        {
            string text = ConvertFlagToText(flag, cmd);
            Application.Current.Dispatcher.Invoke(() =>
            {
                TcpLogs.Insert(0, new LoggerEntry(DateTime.Now, "[CMD]", text));
            });

            try
            {
                await _cmdTcp.SendCommandAsync(flag, cmd);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERR] SendCommandAsync 실패: {ex.Message}");
            }
        }

        private string ConvertFlagToText(byte flag, byte cmd)
        {
            switch ((CmdFlag)flag)
            {
                case CmdFlag.ModeNum:
                    return $"{(ModeNum)cmd} mode switched";
                case CmdFlag.CamNum:
                    return cmd == 0 ? "EO cam selection " : "IR cam selection ";
                case CmdFlag.PrepOpt:
                    return $"전처리 마스크: 0x{cmd:X2}";
                case CmdFlag.MoveMotor:
                    {
                        byte directionBits = (byte)(cmd & 0b00000011);
                        string axis = (directionBits & 0b10) == 0 ? "Yaw" : "Pitch";
                        string dir = (axis == "Yaw")
                            ? ((directionBits & 0b01) == 0 ? "left" : "right")
                            : ((directionBits & 0b01) == 0 ? "down" : "up");

                        return $"{axis} motor {dir}";
                    }
                case CmdFlag.Track:
                    return $"tracking start #{cmd} ";
                case CmdFlag.InitMotor:
                    return "init motor";
                default:
                    return $"알 수 없는 명령 (flag: 0x{flag:X2}, cmd: 0x{cmd:X2})";
            }
        }

        private void SubscribeEvents()
        {
            _cmdTcp.CommandAckReceived += (flag, cmd) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var logText = ConvertFlagToText(flag, cmd);
                    TcpLogs.Insert(0, new LoggerEntry(DateTime.Now, "[ACK]", logText));
                });
            };

            _stateTcp.StateReceived += OnStateReceived;
        }

        private void BuildTicks()
        {
            for (int deg = 0; deg <= 180; deg++)
            {
                int size =
                      deg % 10 == 0 ? 15 :
                      deg % 5 == 0 ? 10 : 5;

                bool show = deg % 10 == 0;

                YawTicks.Add(new Tick { Deg = deg, Size = size, ShowLabel = show });
                PitchTicks.Add(new Tick { Deg = deg, Size = size, ShowLabel = show });
            }
        }

        private void OnStateReceived(StatePacket pkt)
        {
            _uiDispatcher.Invoke(() =>
            {
                Connection?.UpdateState(pkt);
                Mode = (ModeNum)pkt.Mode;
            });
        }

        /* ---------- 프레임 수신 → 리스트 갱신 ---------- */

        private void OnFrameArrived(FramePacket fp)
        {
            var now = DateTime.Now;

            _uiDispatcher.Invoke(() =>
            {
                // ── 1) 이번 프레임의 객체 사전 만들기 ──
                var current = fp.Objects
                                .Where(o => o.Confidence > 0f)
                                .ToDictionary(o => o.TrackingId);

                // ── 2-A) UPDATE or ADD ──
                foreach (var kv in current)
                {
                    if (_dict.TryGetValue(kv.Key, out var exist))
                    {
                        exist.CopyFrom(kv.Value);            // 정보 갱신
                    }
                    else
                    {
                        Detections.Add(kv.Value);            // 새로 등장
                        _dict[kv.Key] = kv.Value;
                    }
                    _lastSeen[kv.Key] = now;                 // 마지막 시각 기록
                }

                // ── 2-B) TIMEOUT 제거 ──
                var expired = _lastSeen
                              .Where(kv => now - kv.Value > _keepAlive)
                              .Select(kv => kv.Key)
                              .ToList();

                foreach (var id in expired)
                {
                    Detections.Remove(_dict[id]);
                    _dict.Remove(id);
                    _lastSeen.Remove(id);
                    //Debug.WriteLine($"removed Track ID : {id}");
                }

                /* === Scan 모드 → 첫 검출 시 자동 추적 === */
                TryAutoTrack();

                if (Mode == ModeNum.Track && _currentTrackId.HasValue)
                {
                    // 현재 프레임에서 추적 ID가 보였나?
                    bool seen = _dict.ContainsKey(_currentTrackId.Value);

                    if (seen)
                    {
                        _lostFrames = 0;           // 다시 보이면 손실 카운터 리셋
                    }
                    else
                    {
                        _lostFrames++;

                        // ★ 일정 프레임 연속 손실 → 수동 전환
                        if (_lostFrames >= _lostThreshold)
                        {
                            MessageBox.Show("추적 대상이 유실되었습니다.", "추적 실패", MessageBoxButton.OK, MessageBoxImage.Error);
                            SetManualMode();       // _trackId, _lostFrames 둘 다 초기화됨
                            string text = "tracking finish";
                            var entry = new LoggerEntry(DateTime.Now, "[FIN]", text);

                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                TcpLogs.Insert(0, entry);
                            });
                        }
                    }
                }
            });
        }

        /* ---------- 추적 명령 ---------- */
        private void SendTrack()
        {
            if (SelectedObj == null) return;

            _currentTrackId = SelectedObj.TrackingId;   // ★ 추적 대상 저장
            //Debug.WriteLine($"track set : {_currentTrackId}");

            _lostFrames = 0;  // 새로 추적 시작

            byte flag = (byte)CmdFlag.Track;
            byte cmd = SelectedObj.TrackingId;   // TrackingId가 int면 (byte) 생략

            _ = LogAndSendCommandAsync(flag, cmd);
        }

        /// Scan 모드 상태에서 첫 객체를 검출하면 자동으로 그 객체를 추적으로 전환
        private void TryAutoTrack()
        {
            // ① Scan 모드가 아니면 건너뜀
            if (Mode != ModeNum.Scan) return;

            // ② 이미 추적 중이거나(._currentTrackId) 선택된 객체가 있으면 건너뜀
            if (_currentTrackId.HasValue) return;

            // ③ 화면에 보이는 객체가 하나도 없으면 건너뜀
            if (_dict.Count == 0) return;

            // ④ 우선순위: 가장 높은 Confidence 를 가진 객체 선택
            ObjectInfo target = null;
            double bestConf = -1.0;

            foreach (var obj in _dict.Values)
            {
                if (obj.Confidence > bestConf)
                {
                    bestConf = obj.Confidence;
                    target = obj;
                }
            }

            if (target == null) return;   // 이론상 없음

            // ⑤ SelectedObj 변경(바인딩 갱신) + 추적 명령 전송
            SelectedObj = target;
            SendTrack();                  // 내부에서 _currentTrackId 설정 & Track 명령 전송

            // 스캔 -> 추적 알람
            _alertPlayer.Position = TimeSpan.Zero;
            _alertPlayer.Play();

            // ⑥ 모드도 Track 으로 변경 (UI·장치 동기화)
            Mode = ModeNum.Track;
        }

        public void StartServices()
        {
            _vRx?.Start();
        }
    }
}
