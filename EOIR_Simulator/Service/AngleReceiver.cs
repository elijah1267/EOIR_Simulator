using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using EOIR_Simulator.Model;

namespace EOIR_Simulator.Service
{
    public class AngleReceiver : IDisposable
    {
        private readonly int _port;
        private TcpListener _listener;
        private CancellationTokenSource _cts;

        public event Action<byte, byte> AngleReceived;  // angle_x, angle_y

        public AngleReceiver(int port)
        {
            _port = port;
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            _listener = new TcpListener(System.Net.IPAddress.Any, _port);
            _listener.Start();
            //Debug.WriteLine($"[TCP] listening {_port}"); // Debug
            Task.Run(() => AcceptLoopAsync(_cts.Token));
        }

        private async Task AcceptLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    //var client = await _listener.AcceptTcpClientAsync(token).ConfigureAwait(false);
                    var client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    _ = Task.Run(() => ReceiveLoopAsync(client, token));
                }
            }
            catch (ObjectDisposedException) { /* Listener stopped */ }
            catch (Exception ex)
            {
                Console.WriteLine($"[TCP Angle Receiver] 수신 실패: {ex.Message}");
            }
        }

        private async Task ReceiveLoopAsync(TcpClient client, CancellationToken token)
        {
            using (client)
            using (var stream = client.GetStream())
            {
                byte[] buf = new byte[6];
                while (!token.IsCancellationRequested)
                {
                    int read = 0;
                    while (read < 6)
                    {
                        int r = await stream.ReadAsync(buf, read, 6 - read, token).ConfigureAwait(false);
                        if (r == 0) return; // 연결 종료
                        read += r;
                    }

                    if (buf[0] == 0xA5 && buf[1] == 0x5A)
                    {
                        byte angleX = buf[2];
                        byte angleY = buf[3];
                        //Console.WriteLine($"[RECV] angleX = {angleX}, angleY = {angleY}");
                        AngleReceived?.Invoke(angleX, angleY);
                    }
                    else
                    {
                        Console.WriteLine($"[TCP] 잘못된 헤더: {BitConverter.ToString(buf)}");
                    }
                }
            }
        }

        public void Stop()
        {
            _cts?.Cancel();
            _listener?.Stop();
        }

        public void Dispose()
        {
            Stop();
        }
    }
}