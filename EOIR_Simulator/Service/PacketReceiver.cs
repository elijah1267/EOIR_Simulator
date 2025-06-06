using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using EOIR_Simulator.Model;

namespace EOIR_Simulator.Service
{
    public class PacketReceiver : IDisposable
    {
        //C++ dll 연동
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

        public PacketReceiver(int port)
        {
            _client = new UdpClient(port);
            Debug.WriteLine($"[UDP] bind {port}"); //Debug
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            // GStreamer 송신 파이프라인 초기화
            //InitSender("appsrc name=mysrc ! videoconvert ! x264enc tune=zerolatency bitrate=500 speed-preset=ultrafast ! rtph264pay ! udpsink host=127.0.0.1 port=5005");
            InitSender("appsrc name=mysrc is-live=true block=false format=GST_FORMAT_TIME ! " +
                       "videoconvert ! video/x-raw,format=I420 ! openh264enc bitrate=500000 gop-size=30 ! rtph264pay config-interval=1 pt=96 ! " +
                       "udpsink host=127.0.0.1 port=5005 sync=false async=false"); //bind-address=192.168.1.10
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

        private async Task ReceiveLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    UdpReceiveResult res = await _client.ReceiveAsync();
                    byte[] pkt = res.Buffer;

                    if (pkt.Length < IcdConstants.PAYLOAD_OFFSET) continue;

                    // [0-3] Magic Word
                    uint magic = (uint)(pkt[0] << 24 | pkt[1] << 16 | pkt[2] << 8 | pkt[3]);
                    if (magic != IcdConstants.MAGIC_WORD) continue; // 식별자 불일치

                    // [4-7] Frame ID
                    uint frameId = (uint)(pkt[4] << 24 | pkt[5] << 16 | pkt[6] << 8 | pkt[7]);
                    // [8-9] Packet ID
                    ushort packetId = (ushort)((pkt[8] << 8) | pkt[9]);
                    // [10-11] Total Packets
                    ushort totalPackets = (ushort)((pkt[10] << 8) | pkt[11]);

                    // [12-13] Motor angle
                    byte nx = pkt[12];
                    byte ny = pkt[13];

                    // [14 - ...] Object Info
                    List<ObjectInfo> objects = new List<ObjectInfo>();
                    for (int i = 0; i < 5; i++)
                    {
                        objects.Add(ObjectInfo.FromBytes(pkt, 14 + i * IcdConstants.OBJECTINFO_SIZE));
                    }

                    // [offset 이후] Payload
                    byte[] payload = new byte[pkt.Length - IcdConstants.PAYLOAD_OFFSET];
                    Buffer.BlockCopy(pkt, IcdConstants.PAYLOAD_OFFSET, payload, 0, payload.Length);

                    lock (_lock)
                    {
                        if (!_framePackets.ContainsKey(frameId))
                        {
                            _framePackets[frameId] = new Dictionary<ushort, byte[]>();
                            _expectedCounts[frameId] = totalPackets;
                        }

                        _framePackets[frameId][packetId] = payload;

                        if (_framePackets[frameId].Count >= totalPackets)
                        {
                            using (var ms = new MemoryStream())
                            {
                                for (ushort i = 0; i < totalPackets; i++)
                                {
                                    if (_framePackets[frameId].TryGetValue(i, out var part))
                                        ms.Write(part, 0, part.Length);
                                }

                                byte[] jpeg = ms.ToArray();

                                // SendFrame 비동기 호출 (별도 스레드)
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
                                    Task.Delay(33);
                                });

                                FrameArrived?.Invoke(new FramePacket
                                {
                                    FrameId = frameId,
                                    JpegBytes = jpeg,
                                    Nx = nx,
                                    Ny = ny,
                                    Objects = objects
                                });
                            }

                            _framePackets.Remove(frameId);
                            _expectedCounts.Remove(frameId);
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