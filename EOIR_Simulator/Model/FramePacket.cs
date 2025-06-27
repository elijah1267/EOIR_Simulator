using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EOIR_Simulator.Model
{
    public class FramePacket
    {
        public uint FrameId { get; set; }
        public uint PacketId { get; set; }
        public byte[] JpegBytes { get; set; }
        public List<ObjectInfo> Objects { get; set; }
        public string TimeStamp { get; set; }
        public DateTime RecvStartUtc { get; set; }   // UDP 수신 대기 직후
        public DateTime RecvDoneUtc { get; set; }   // 첫 패킷 수신 완료
        public DateTime AssembleUtc { get; set; }   // JPEG 조립 완료
    }
}
