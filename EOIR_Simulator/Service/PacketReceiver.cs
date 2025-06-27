using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text; // ✅ 추가: timestamp 문자열 디코딩용


using System.Runtime.InteropServices;


using System.Threading;
using System.Threading.Tasks;
using EOIR_Simulator.Model;
using EOIR_Simulator.ViewModel;

namespace EOIR_Simulator.Service
{
    public class PacketReceiver : IDisposable
    {
        //C++ 연동
        [DllImport("FrameSender.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void InitSender(string pipeline);

        [DllImport("FrameSender.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SendFrame(byte[] jpegData, int length);

        [DllImport("FrameSender.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void CloseSender();


        private readonly UdpClient _client;
        private readonly object _lock = new object();
        private CancellationTokenSource _cts;

        private readonly Dictionary<uint, Dictionary<ushort, byte[]>> _framePackets = new Dictionary<uint, Dictionary<ushort, byte[]>>();
        private readonly Dictionary<uint, int> _expectedCounts = new Dictionary<uint, int>();

        public event Action<FramePacket> FrameArrived;
        private DateTime _lastFrameTime = DateTime.MinValue;

        private VideoVM _video;

        public void BindTo(VideoVM video)
        {
            _video = video;
        }

        public PacketReceiver(int port)
        {
            //_client = new UdpClient(port);
            _client = new UdpClient(new IPEndPoint(IPAddress.Any, port));
            // _client.Client.ReceiveBufferSize = 2 * 1024 * 1024; // DAN 추가
            //Debug.WriteLine($"[UDP] bind {port}");
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            InitSender("appsrc name=mysrc is-live=true block=false format=GST_FORMAT_TIME ! " +
                       "videoconvert ! video/x-raw,format=I420 ! openh264enc bitrate=500000 gop-size=30 ! rtph264pay config-interval=1 pt=96 ! " +
                       "udpsink host=192.168.1.9 port=5005 sync=false async=false");
            //Debug.WriteLine("[PacketReceiver] 수신 루프 시작됨");
            Task.Run(() => ReceiveLoop(_cts.Token));
        }

        public void Stop()
        {
            _cts?.Cancel();
        }

        public void Dispose()
        {
            Stop();
            CloseSender(); // GStreamer 파이프라인 종료
            _client?.Dispose();
        }

        private const int HEADER_SIZE = 12;
        private const int OBJECTINFO_SIZE = 14;
        private const int META_SIZE = OBJECTINFO_SIZE * 5;
        //private const int PAYLOAD_OFFSET = HEADER_SIZE + META_SIZE;
        private const int TIMESTAMP_SIZE = 23;                // 문자열 길이
        private const int PAYLOAD_OFFSET = HEADER_SIZE + META_SIZE + TIMESTAMP_SIZE;

        private async Task ReceiveLoop(CancellationToken token)
        {
            //Debug.WriteLine("[ReceiveLoop] Waiting for UDP...");
            while (!token.IsCancellationRequested)
            {
                try
                {
                    DateTime t0 = DateTime.Now;
                    UdpReceiveResult res = await _client.ReceiveAsync();
                    DateTime t1 = DateTime.Now;

                    byte[] pkt = res.Buffer;
                    if (pkt.Length < PAYLOAD_OFFSET) continue;

                    uint magic = (uint)(pkt[0] << 24 | pkt[1] << 16 | pkt[2] << 8 | pkt[3]);
                    if (magic != 0xDEADBEEF) continue;

                    uint frameId = (uint)(pkt[4] << 24 | pkt[5] << 16 | pkt[6] << 8 | pkt[7]);
                    ushort packetId = (ushort)((pkt[8] << 8) | pkt[9]);
                    ushort totalPackets = (ushort)((pkt[10] << 8) | pkt[11]);
                    List<ObjectInfo> objects = new List<ObjectInfo>();

                    for (int i = 0; i < 5; i++)
                    {
                        ObjectInfo obj = ObjectInfo.FromBytes(pkt, 12 + i * OBJECTINFO_SIZE);
                        if (obj.Class != 255)
                            objects.Add(obj);
                    }

                    if (packetId == 0 && objects.Count > 0)
                    {
                        DateTime now = DateTime.Now;
                        LoggerService.LogUdpDetections(now, (int)frameId, objects);
                    }

                    // ✅ 추가: timestamp 파싱 ("2025-06-22T21:47:22.837")
                    int timestampOffset = HEADER_SIZE + META_SIZE;
                    string timestamp = Encoding.ASCII.GetString(pkt, timestampOffset, TIMESTAMP_SIZE);

                    int jpegOffset = timestampOffset + TIMESTAMP_SIZE;
                    byte[] payload = new byte[pkt.Length - jpegOffset];
                    Buffer.BlockCopy(pkt, jpegOffset, payload, 0, payload.Length);

                    lock (_lock)
                    {
                        if (!_framePackets.ContainsKey(frameId))
                        {
                            _framePackets[frameId] = new Dictionary<ushort, byte[]>();
                            _expectedCounts[frameId] = totalPackets;
                        }

                        _framePackets[frameId][packetId] = payload;

                        if (_framePackets[frameId].Count == totalPackets)
                        {
                            DateTime t2 = DateTime.Now;
                            using (var ms = new MemoryStream())
                            {
                                bool allPacketsPresent = true;
                                for (ushort i = 0; i < totalPackets; i++)
                                {
                                    if (!_framePackets[frameId].TryGetValue(i, out var part))
                                    {
                                        allPacketsPresent = false;
                                        break;
                                    }
                                    ms.Write(part, 0, part.Length);
                                }

                                if (!allPacketsPresent)
                                {
                                    //Debug.WriteLine($"[WARN] Frame {frameId} dropped due to missing packets");
                                    _framePackets.Remove(frameId);
                                    _expectedCounts.Remove(frameId);
                                    continue;
                                }

                                byte[] jpeg = ms.ToArray();
                                DateTime t3 = DateTime.Now;

                               

                                Task.Run(() =>
                                {
                                    try
                                    {
                                        //Debug.WriteLine($"[SendFrame] 호출 - {jpeg.Length} bytes");
                                        SendFrame(jpeg, jpeg.Length);
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.WriteLine($"[SendFrame ERROR] {ex.Message}");
                                    }
                                });

                                FrameArrived?.Invoke(new FramePacket
                                {
                                    FrameId = frameId,
                                    PacketId = packetId,
                                    JpegBytes = jpeg,
                                    Objects = objects,
                                    TimeStamp = timestamp,
                                    RecvStartUtc = t0,
                                    RecvDoneUtc = t1,
                                    AssembleUtc = t3
                                });



                                _framePackets.Remove(frameId);
                                _expectedCounts.Remove(frameId);
                            }
                        }
                    }
                }
                catch (ObjectDisposedException) { break; }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ERR] 수신 오류: {ex.Message}");
                }
            }
        }

    }
}

