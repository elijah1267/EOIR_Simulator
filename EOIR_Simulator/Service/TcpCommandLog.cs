using EOIR_Simulator.Model;
using System;

public sealed class TcpCommandLog
{
    public TcpCommandLog(DateTime time,
                         ModeNum mode,
                         sbyte dx,
                         sbyte dy,
                         ushort id = 0,
                         bool isAck = false)  // ★ 추가
    {
        Time = time;
        Mode = mode;
        Dx = dx;
        Dy = dy;
        Id = id;
        IsAck = isAck;
    }

    public DateTime Time { get; }
    public ModeNum Mode { get; }
    public sbyte Dx { get; }
    public sbyte Dy { get; }
    public ushort Id { get; }
    public bool IsAck { get; }

    public override string ToString()
    {

        string modeStr;
        switch (Mode)
        {
            case ModeNum.Scan: modeStr = "스캔"; break;
            case ModeNum.Manual: modeStr = "수동"; break;
            case ModeNum.Track: modeStr = "추적"; break;
            default: modeStr = $"알수없음({(int)Mode})"; break;
        };

        string prefix = IsAck ? "[ACK] " : "[MSG] ";

        return string.Format("{0:HH:mm:ss.fff} {1}{2, -6} {3,4} {4,4} {5,4}",
                             Time, prefix, modeStr, Dx, Dy, Id);
    }
}
