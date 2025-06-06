using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EOIR_Simulator.Model
{
    public static class ModeNumExtensions
    {
        public static string ToEng(this ModeNum mode)
        {
            switch (mode)
            {
                case ModeNum.Scan: return "scan";
                case ModeNum.Manual: return "manual";
                case ModeNum.Track: return "tracking";
                default: return $"unknown({(int)mode})";
            }
        }
    }

    public static class StateCodeExtensions
    {
        public static string ToStateString(this int state)
        {
            switch (state)
            {
                case 0: return "CHECKING";
                case 1: return "IDLE";
                case 2: return "RUNNING";
                default: return $"UNKNOWN({state})";
            }
        }
    }

    public enum ModeNum : ushort { Manual = 1, Scan = 0, Track = 2 }

    public enum TcpState { Disconnected, Connecting, Connected }

    public enum UdpState { Disconnected, Connected }

    public enum SimState { Idle, Operating }
    public class Enums
    {
    }
}
