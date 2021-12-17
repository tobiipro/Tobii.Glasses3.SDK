using System;

namespace G3SDK
{
    public class LogHelper
    {
        public static void LogWebSockMsg(string source, string dest, string msg)
        {
            if (msg.Length > 1000)
                msg = $"[ImageData {msg.Length} bytes] {msg.Substring(0,20)}...";
            msg = $"{source} => {dest}: {msg}";
            LogMsg(msg);
        }

        public static void LogMsg(string msg)
        {
            Console.WriteLine($"{DateTime.Now:T} {msg}");
        }
    }
}