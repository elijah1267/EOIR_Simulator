using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using EOIR_Simulator.Model;

namespace EOIR_Simulator.Service
{
    public class CommandSender : IDisposable
    {
        private readonly string _ip;
        private readonly int _port;
        private TcpClient _client;
        private NetworkStream _stream;

        private int _connecting;
        private CancellationTokenSource _cts;

        private ModeNum _lastSentMode;
        private ModeNum _lastReceivedMode = ModeNum.Manual;
        private DateTime _lastSentTime;
        private bool _awaitingAck = false;

        public TcpState State { get; private set; } = TcpState.Disconnected;
        public bool IsConnected => State == TcpState.Connected;

        public event Action<TcpState> StateChanged;
        public event Action<int, bool, bool> StateReceived;
        public event Action<TcpCommandLog> CommandSent;

        public CommandSender(string ip, int port)
        {
            _ip = ip;
            _port = port;
        }

        private async Task<bool> EnsureConnectedAsync()
        {
            if (State == TcpState.Connected && _client?.Connected == true)
                return true;

            if (Interlocked.Exchange(ref _connecting, 1) == 1)
                return false;

            try
            {
                _client = new TcpClient();
                await _client.ConnectAsync(_ip, _port);
                _stream = _client.GetStream();

                State = TcpState.Connected;
                StateChanged?.Invoke(State);

                _cts = new CancellationTokenSource();
                StartReceivingStateLoop(_cts.Token);

                return true;
            }
            catch
            {
                State = TcpState.Disconnected;
                StateChanged?.Invoke(State);
                return false;
            }
            finally
            {
                Interlocked.Exchange(ref _connecting, 0);
            }
        }

        public Task<bool> ConnectAsync() => EnsureConnectedAsync();

        public async Task SendAsync(ModeNum mode, sbyte dx, sbyte dy)
        {
            if (!await EnsureConnectedAsync().ConfigureAwait(false))
                return;

            try
            {
                byte[] buf = new byte[6];
                buf[0] = 0xA5; buf[1] = 0xA5;
                ushort m = (ushort)mode;
                buf[2] = (byte)m; buf[3] = (byte)(m >> 8);
                buf[4] = unchecked((byte)dx);
                buf[5] = unchecked((byte)dy);

                await _stream.WriteAsync(buf, 0, buf.Length).ConfigureAwait(false);
                await _stream.FlushAsync().ConfigureAwait(false);

                _lastSentMode = mode;
                _lastSentTime = DateTime.Now;
                _awaitingAck = true;

                LoggerService.LogTcpMsg(2, mode, dx, dy, 0);
                CommandSent?.Invoke(new TcpCommandLog(_lastSentTime, mode, dx, dy, 0, isAck: false));

                while (true)
                {
                    if (_lastReceivedMode == mode)
                    {
                        CommandSent?.Invoke(new TcpCommandLog(DateTime.Now, mode, 0, 0, 0, isAck: true));
                        break;
                    }

                    // 계속 반복해서 출력 (짧은 간격으로)
                    CommandSent?.Invoke(new TcpCommandLog(DateTime.Now, _lastReceivedMode, 0, 0, 0, isAck: true));
                    await Task.Delay(100); // 너무 빨리 도는 거 방지
                }
            }
            catch
            {
                Disconnect();
            }
        }

        private async void StartReceivingStateLoop(CancellationToken token)
        {
            try
            {
                byte[] buf = new byte[3];
                while (!token.IsCancellationRequested)
                {
                    int read = 0;
                    while (read < 3)
                    {
                        int r = await _stream.ReadAsync(buf, read, 3 - read, token);
                        if (r == 0) throw new Exception("TCP closed by remote.");
                        read += r;
                    }

                    if (buf[0] != 0xA5 || buf[1] != 0xA5)
                    {
                        Console.WriteLine("[WARN] Invalid magic word in received packet.");
                        continue;
                    }

                    byte payload = buf[2];
                    int mode = (payload >> 4) & 0b11;
                    _lastReceivedMode = (ModeNum)mode;

                    int state = (payload >> 2) & 0b11;
                    bool tpu = (payload & 0b10) != 0;
                    bool cam = (payload & 0b01) != 0;


                    StateReceived?.Invoke(state, tpu, cam);
                    Console.WriteLine($"[TCP] State Received - Mode: {mode}, State: {state}, TPU: {tpu}, CAM: {cam}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ERR] TCP receive error: " + ex.Message);
                Disconnect();
            }
        }

        public void Disconnect()
        {
            if (State == TcpState.Connected)
            {
                try
                {
                    _cts?.Cancel();
                    _stream?.Close();
                    _client?.Close();
                }
                finally
                {
                    State = TcpState.Disconnected;
                    StateChanged?.Invoke(State);
                }
            }
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _stream?.Dispose();
            _client?.Close();
        }
    }
}
