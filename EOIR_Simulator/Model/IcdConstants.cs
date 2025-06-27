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
        public const int HEADER_SIZE = 53;
        public const int OBJ_COUNT_MAX = 5;
        public const int OBJ_SIZE = 9;
        public const int UDP_PORT = 5000;
    }
}
