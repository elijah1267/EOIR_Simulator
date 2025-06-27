using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using EOIR_Simulator.Model;

namespace EOIR_Simulator.Service
{
    public static class LoggerService
    {
        private static readonly string logDir_META = "META Logs";
        private static readonly string logDir_CMD = "CMD Logs";
        private static readonly string logDir_STATE = "STATE Logs";

        private static readonly string logFile_meta;
        private static readonly string logFile_cmd;
        private static readonly string logFile_state;

        private static readonly object _lock = new object();

        static LoggerService()
        {
            Directory.CreateDirectory(logDir_META);
            Directory.CreateDirectory(logDir_CMD);
            Directory.CreateDirectory(logDir_STATE);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            logFile_meta = Path.Combine(logDir_META, $"EOIR_META_{timestamp}.csv");
            logFile_cmd = Path.Combine(logDir_CMD, $"EOIR_CMD_{timestamp}.csv");
            logFile_state = Path.Combine(logDir_STATE, $"EOIR_STATE_{timestamp}.csv");

            lock (_lock)
            {
                using (var writer = new StreamWriter(logFile_meta, true, Encoding.UTF8))
                {
                    //writer.WriteLine("Time,FrameID,cls,id,x,y,w,h,conf");
                }

                using (var writer = new StreamWriter(logFile_state, true, Encoding.UTF8))
                {
                    //writer.WriteLine("Time, state, mode, motor_nx, motor_ny, TPU, CAM, SD");
                }

                using (var writer = new StreamWriter(logFile_cmd, true, Encoding.UTF8))
                {
                    //writer.WriteLine("Type, Time, cmd_flag, cmd");
                }

            }
        }

        public static void LogUdpDetections(DateTime timestamp, int frameId, List<ObjectInfo> objects)
        {
            string ts = timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fff");
            string header = $"{ts},{frameId}";

            var entries = new List<string>();

            foreach (var obj in objects)
            {
                int conf = (int)(obj.Confidence*100);
                if (obj.Class != 255)
                    entries.Add($"{obj.Class},{obj.TrackingId},{obj.X},{obj.Y},{obj.W},{obj.H},{conf+"%"}");
            }

            // 개수 제한 없이 필요한 만큼만 출력 (빈자리 없음)
            string line;

            if (entries.Count == 0)
            {
                // 객체가 없으면 헤더만
                line = header;
            }
            else
            {
                // 객체가 하나 이상이면 객체별로 줄바꿈 정렬
                line = header + "," + string.Join("\n,,", entries);
            }

            lock (_lock)
                File.AppendAllText(logFile_meta, line + Environment.NewLine);
        }


        public static void LogState(DateTime timestamp, int state, ModeNum mode, byte nx, byte ny, bool tpu, bool cam, bool sdInserted)
        {
            string ts = timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fff");

            string stateStr = state.ToStateString();

            string modeStr = mode.ToString();
            string line = $"{ts},{stateStr},{modeStr},{nx},{ny},{tpu},{cam},{(sdInserted ? 1 : 0)}";

            lock (_lock)
                File.AppendAllText(logFile_state, line + Environment.NewLine);
        }
        public static void LogCmdMsg(DateTime timestamp, byte cmdFlag, byte cmd)
        {
            string time = timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fff");

            string flagStr = Enum.IsDefined(typeof(CmdFlag), cmdFlag)
                ? ((CmdFlag)cmdFlag).ToString()
                : $"UNKNOWN({cmdFlag})";

            string line = $"[CMD],{time},{flagStr},{cmd}";

            lock (_lock)
                File.AppendAllText(logFile_cmd, line + Environment.NewLine);
        }


        public static void LogCmdAck(DateTime timestamp, byte cmdFlag, byte cmd)
        {
            string time = timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fff");

            string flagStr = Enum.IsDefined(typeof(CmdFlag), cmdFlag)
                ? ((CmdFlag)cmdFlag).ToString()
                : $"UNKNOWN({cmdFlag})";

            string line = $"[ACK],{time},{flagStr},{cmd}";

            lock (_lock)
                File.AppendAllText(logFile_cmd, line + Environment.NewLine);
        }



    }
}
