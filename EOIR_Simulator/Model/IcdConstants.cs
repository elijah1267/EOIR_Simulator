using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EOIR_Simulator.Model
{
    public static class IcdConstants
    {
        public const uint MAGIC_WORD = 0xDEADBEEF;
        public const int HEADER_SIZE = 12;
        public const int OBJECTINFO_SIZE = 14;
        public const int META_SIZE = 2 + OBJECTINFO_SIZE * 5;  // nx, ny + 5 * ObjectInfo
        public const int PAYLOAD_OFFSET = HEADER_SIZE + META_SIZE;
        public const int UDP_PORT = 5000;

        public const string TCP_IP = "192.168.1.3";
        public const int TCP_PORT = 9999;
    }
}
