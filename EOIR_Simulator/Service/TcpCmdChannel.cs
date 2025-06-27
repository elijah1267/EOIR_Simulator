using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using EOIR_Simulator.Model;

namespace EOIR_Simulator.Service
{
    public class TcpCmdChannel : IDisposable
    {
        private readonly string _ip;
        private readonly int _port;
        private TcpClient _client;
        private NetworkStream _stream;
        private CancellationTokenSource _cts;

        private bool _connected;
        public bool IsConnected => _connected;


        public event Action<bool> StateChanged;
        public event Action<byte, byte> CommandAckReceived; // (cmd_flag, cmd)

        public TcpCmdChannel(string ip, int port)
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

                _connected = true;
                StateChanged?.Invoke(true);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ERR] TCP CMD Connect: " + ex.Message);
                _connected = false;
                StateChanged?.Invoke(false);
                return false;
            }
        }


        public async Task SendCommandAsync(byte cmdFlag, byte cmd, int timeoutMs = 1000)
        {
            if (!IsConnected) return;

            byte[] packet = new byte[4];
            packet[0] = 0xA5;
            packet[1] = 0xA5;
            packet[2] = cmdFlag;
            packet[3] = cmd;

            //Console.Write("[SEND] Packet: ");
            //foreach (var b in packet)
            //    Console.Write($"{b:X2} ");
            //Console.WriteLine();

            LoggerService.LogCmdMsg(DateTime.Now, cmdFlag, cmd);

            try
            {
                await _stream.WriteAsync(packet, 0, packet.Length);
                await _stream.FlushAsync();

                // ACK 수신 대기 (동일 패킷 echo)
                var buf = new byte[4];
                int read = 0;

                using (var cts = new CancellationTokenSource(timeoutMs))
                {
                    while (read < 4 && !cts.IsCancellationRequested)
                    {
                        int r = await _stream.ReadAsync(buf, read, 4 - read, cts.Token);
                        if (r == 0) throw new Exception("Disconnected during ACK read.");
                        read += r;
                    }
                }


                if (buf[0] == 0xA5 && buf[1] == 0xA5 && buf[2] == cmdFlag && buf[3] == cmd)
                {
                    LoggerService.LogCmdAck(DateTime.Now, cmdFlag, cmd);
                    CommandAckReceived?.Invoke(cmdFlag, cmd);
                }
                else
                {
                    //Console.WriteLine("[WARN] ACK mismatch");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ERR] SendCommandAsync: " + ex.Message);
            }
        }

        public void Disconnect()
        {
            try
            {
                _cts?.Cancel();
                _stream?.Close();
                _client?.Close();

                _connected = false;
                StateChanged?.Invoke(false);
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
}
