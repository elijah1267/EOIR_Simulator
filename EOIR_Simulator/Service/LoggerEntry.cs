using System;

public sealed class LoggerEntry
{
    public LoggerEntry(DateTime time, string type, string cmdText)
    {
        Time = time;
        Type = type;       // 예: "[CMD]", "[ACK]", "[FIN]" 등
        CmdText = cmdText;
    }

    public DateTime Time { get; }
    public string Type { get; }   // 변경: byte → string
    public string CmdText { get; }

    public override string ToString()
    {
        string timestamp = Time.ToString("HH:mm:ss.fff");
        return $"{timestamp} {Type} {CmdText}";
    }
}
