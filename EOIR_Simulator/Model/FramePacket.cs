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
        public byte[] JpegBytes { get; set; }
        public byte Nx { get; set; }
        public byte Ny { get; set; }
        public List<ObjectInfo> Objects { get; set; }
    }
}
