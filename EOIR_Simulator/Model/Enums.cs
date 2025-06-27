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
            //Console.WriteLine("[ENUM] :  " + mode);
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

    public enum ModeNum : ushort { Scan = 0, Manual = 1, Track = 2 }

    public enum TcpState { Disconnected, Connecting, Connected }

    public enum UdpState { Disconnected, Connected }

    public enum CmdFlag : byte
    {
        ModeNum = 0x0,
        CamNum = 0x1,
        PrepOpt = 0x2,
        MoveMotor = 0x3,
        Track = 0x4,
        InitMotor = 0x5
    }

    public enum CamType : byte
    {
        EO = 0x0,
        IR = 0x1
    }

    [Flags]
    public enum PrepOpt : byte
    {
        None = 0b00000000,
        Contrast = 0b00000001,
        Laplacian = 0b00000010,
        Denoise = 0b00000100,
        Deblur = 0b00001000,
        Clutter = 0b00010000
    }
    public class Enums { }
}