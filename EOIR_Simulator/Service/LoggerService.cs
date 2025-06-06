using System;
using System.IO;
using System.Text;
using System.Threading;
using EOIR_Simulator.Model;

namespace EOIR_Simulator.Service
{
    public static class LoggerService
    {
        private static readonly string logDir = "Logs";
        private static readonly string logFile;
        private static readonly object _lock = new object();

        static LoggerService()
        {
            Directory.CreateDirectory(logDir);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            logFile = Path.Combine(logDir, $"log_{timestamp}.csv");

            lock (_lock)
            {
                using (var writer = new StreamWriter(logFile, true, Encoding.UTF8))
                {
                    writer.WriteLine("Type,Time,State,Mode,dx,dy,id");
                    writer.WriteLine("Type,Time,FrameID,id,cls,x,y,w,h,conf");
                }
            }
        }

        public static void LogTcpMsg(int stateCode, ModeNum mode, int dx, int dy, int id)
        {
            string time = DateTime.Now.ToString("HH:mm:ss");
            string state = stateCode.ToStateString();
            string modeStr = mode.ToEng();
            string line = $"[TCP MSG],{time},{state},{modeStr},{dx},{dy},{id}";

            Append(line);
        }

        public static void LogTcpAck(int stateCode, ModeNum mode, int dx = 0, int dy = 0, int id = 0)
        {
            string time = DateTime.Now.ToString("HH:mm:ss");
            string state = stateCode.ToStateString();
            string modeStr = mode.ToEng();
            string line = $"[TCP ACK],{time},{state},{modeStr},{dx},{dy},{id}";

            Append(line);
        }

        public static void LogUdpDetection(string time, int frameId, int id, int cls, int x, int y, int w, int h, int conf)
        {
            string line = $"[UDP MSG],{time},{frameId},{id},{cls},{x},{y},{w},{h},{conf}";
            Append(line);
        }

        private static void Append(string line)
        {
            lock (_lock)
                File.AppendAllText(logFile, line + Environment.NewLine);
        }
    }
}
