using System;

namespace EOIR_Simulator.Radar
{
    public class TlvParser
    {
        public DetectionObject Parse(byte[] packet)
        {
            if (packet.Length < 52) return null;

            int idX = 28;
            int numDetectedObj = BitConverter.ToInt32(packet, idX);
            idX += 4;
            int numTLVs = BitConverter.ToInt32(packet, idX);
            idX += 4;
            idX += 4;

            for (int i = 0; i < numTLVs; i++)
            {
                int tlvType = BitConverter.ToInt32(packet, idX);
                idX += 4;
                int tlvLength = BitConverter.ToInt32(packet, idX);
                idX += 4;

                if (tlvType == 1)
                {
                    var points = new (float, float)[numDetectedObj];

                    for (int j = 0; j < numDetectedObj; j++)
                    {
                        float x = BitConverter.ToSingle(packet, idX); idX += 4;
                        float y = BitConverter.ToSingle(packet, idX); idX += 4;
                        idX += 8;
                        points[j] = (x, y);
                    }
                    return new DetectionObject { NumObjects = numDetectedObj, Points = points };
                }
                else
                {
                    idX += tlvLength - 8;
                }
            }
            return null;
        }
    }

    public class DetectionObject
    {
        public int NumObjects { get; set; }
        public (float X, float Y)[] Points { get; set; } = Array.Empty<(float, float)>();
    }
}
