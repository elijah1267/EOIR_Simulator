using System;

namespace EOIR_Simulator.Model
{
    public class ObjectInfo
    {
        public ushort Class { get; set; }
        public ushort X { get; set; }
        public ushort Y { get; set; }
        public ushort W { get; set; }
        public ushort H { get; set; }
        public float Confidence { get; set; }

        public static ObjectInfo FromBytes(byte[] data, int offset)
        {
            return new ObjectInfo
            {
                Class = BitConverter.ToUInt16(data, offset),
                X = BitConverter.ToUInt16(data, offset + 2),
                Y = BitConverter.ToUInt16(data, offset + 4),
                W = BitConverter.ToUInt16(data, offset + 6),
                H = BitConverter.ToUInt16(data, offset + 8),
                Confidence = BitConverter.ToSingle(data, offset + 10)
            };
        }
    }
}
