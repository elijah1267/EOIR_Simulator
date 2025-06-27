using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using EOIR_Simulator.Model;

namespace EOIR_Simulator.Service
{
    public class TcpStateChannel : IDisposable
    {
        private readonly string _ip;
        private readonly int _port;
        private TcpClient _client;
        private NetworkStream _stream;
        private CancellationTokenSource _cts;

        public event Action<StatePacket> StateReceived;
        public event Action<byte, byte> AngleReceived;

        public bool IsConnected => _client?.Connected == true;

        public TcpStateChannel(string ip, int port)
        {
            _ip = ip;
            _port = port;
        }

        public async Task<bool> ConnectAsync()
        {
            try
            {
                _client = new TcpClient();
                await _client.ConnectAsync(_ip, _port);
                _stream = _client.GetStream();
                _cts = new CancellationTokenSource();
                _ = StartReceivingAsync(_cts.Token);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ERR] TCP STATE Connect: " + ex.Message);
                MessageBox.Show("대상 컴퓨터에서 연결을 거부했습니다.", "TCP 연결 실패", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private async Task StartReceivingAsync(CancellationToken token)
        {
            byte[] buf = new byte[14];

            try
            {
                while (!token.IsCancellationRequested)
                {
                    int read = 0;
                    while (read < 14)
                    {
                        int r = await _stream.ReadAsync(buf, read, 14 - read, token);
                        if (r == 0) throw new Exception("Disconnected during state read.");
                        read += r;
                    }

                    if (buf[0] != 0xA5 || buf[1] != 0xA5)
                        continue;

                    float cpuTemp = BitConverter.ToSingle(buf, 9);

                    var pkt = new StatePacket
                    {
                        State = buf[2],
                        Mode = (ModeNum)buf[3],
                        Nx = buf[4],
                        Ny = buf[5],
                        Tpu = buf[6] != 0,
                        Cam = buf[7] != 0,
                        SdInserted = buf[8] != 0,
                        CpuTemp = cpuTemp
                    };

                    StateReceived?.Invoke(pkt);
                    AngleReceived?.Invoke(pkt.Nx, pkt.Ny);

                    //Console.WriteLine($"[STATE] State={pkt.State}, Mode={pkt.Mode}, Nx={pkt.Nx}, Ny={pkt.Ny}, " +
                    //                  $"TPU={(pkt.Tpu ? 1 : 0)}, CAM={(pkt.Cam ? 1 : 0)}, " +
                    //                  $"SD={(pkt.SdInserted ? 1 : 0)}, CPU={pkt.CpuTemp:F1}°C\"");


                    DateTime now = DateTime.Now;
                    LoggerService.LogState(now, pkt.State, pkt.Mode, pkt.Nx, pkt.Ny, pkt.Tpu, pkt.Cam, pkt.SdInserted);

                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ERR] TcpStateChannel: " + ex.Message);

                // 연결 끊김 시 상태를 IDLE 로 알림
                var idlePkt = new StatePacket
                {
                    State = 1, // IDLE
                    Mode = ModeNum.Manual,
                    Nx = 0,
                    Ny = 0,
                    Tpu = false,
                    Cam = false,
                    SdInserted = false,
                    CpuTemp = 0f
                };

                StateReceived?.Invoke(idlePkt);
                AngleReceived?.Invoke(idlePkt.Nx, idlePkt.Ny);
                Disconnect();
            }
        }

        public void Disconnect()
        {
            try
            {
                _cts?.Cancel();
                _stream?.Close();
                _client?.Close();
            }
            catch { }
        }

        public void Dispose()
        {
            Disconnect();
            _stream?.Dispose();
            _client?.Close();
        }
    }

    public class StatePacket
    {
        public int State { get; set; }
        public ModeNum Mode { get; set; }
        public byte Nx { get; set; }
        public byte Ny { get; set; }
        public bool Tpu { get; set; }
        public bool Cam { get; set; }
        public bool SdInserted { get; set; }
        public float CpuTemp { get; set; }
    }
}
