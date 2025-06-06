using System;
using System.Windows.Input;
using System.Timers;
using System.Windows.Media;
using EOIR_Simulator.Service;
using EOIR_Simulator.Utils;

namespace EOIR_Simulator.ViewModel
{
    public sealed class ConnectionVM : ObservableObject
    {
        private readonly CommandSender _tcp;
        private readonly PacketReceiver _vrx;
        private readonly Action<bool> _setDeviceState;

        private string _tcpStatus = "TCP : Disconnected";
        public string TcpStatus
        {
            get => _tcpStatus;
            private set
            {
                _tcpStatus = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(TcpStateColor));
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

        public Brush TcpStateColor => _tcp.IsConnected ? Brushes.LimeGreen : Brushes.Red;
        public Brush TpuStateColor => !_tcp.IsConnected ? Brushes.Gray : (TpuConnected ? Brushes.LimeGreen : Brushes.Red);
        public Brush CamStateColor => !_tcp.IsConnected ? Brushes.Gray : (CamConnected ? Brushes.LimeGreen : Brushes.Red);
        public Brush UdpStateColor => UdpStatus.Contains("Connected") ? Brushes.LimeGreen : Brushes.Red;

        public string ConnectButtonText => _tcp.IsConnected ? "Disconnect" : "Connect";
        public ICommand ConnectCommand { get; }

        private DateTime _lastUdp = DateTime.MinValue;
        private readonly Timer _timer = new Timer(500);

        public ConnectionVM(CommandSender tcp, PacketReceiver vrx, Action<bool> setDeviceState)
        {
            _tcp = tcp;
            _vrx = vrx;
            _setDeviceState = setDeviceState;

            _tcp.StateChanged += s =>
            {
                TcpStatus = "TCP : " + s;
                RaisePropertyChanged(nameof(ConnectButtonText));
                RaisePropertyChanged(nameof(TcpStateColor));
                RaisePropertyChanged(nameof(TpuStateColor));
                RaisePropertyChanged(nameof(CamStateColor));
            };

            _tcp.StateReceived += (state, tpu, cam) =>
            {
                TpuConnected = tpu;
                CamConnected = cam;
            };

            _vrx.FrameArrived += _ =>
            {
                _lastUdp = DateTime.UtcNow;
                UdpStatus = "UDP : Connected";
            };

            _timer.Elapsed += (s, e) =>
            {
                var gap = DateTime.UtcNow - _lastUdp;
                if (gap.TotalSeconds > 1.5 && UdpStatus != "UDP : Disconnected")
                    UdpStatus = "UDP : Disconnected";
            };
            _timer.Start();

            ConnectCommand = new RelayCommand(async _ =>
            {
                if (_tcp.IsConnected)
                {
                    _tcp.Disconnect();
                    _lastUdp = DateTime.MinValue;
                    UdpStatus = "UDP : Disconnected";

                    TpuConnected = false;
                    CamConnected = false;

                    _setDeviceState(false);
                }
                else
                {
                    await _tcp.ConnectAsync();
                    _setDeviceState(true);
                }

                RaisePropertyChanged(nameof(ConnectButtonText));
                RaisePropertyChanged(nameof(TcpStateColor));
                RaisePropertyChanged(nameof(TpuStateColor));
                RaisePropertyChanged(nameof(CamStateColor));
            });
        }
    }
}
