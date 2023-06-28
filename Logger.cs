using System;

namespace RainmeterSkinInstaller
{
    public static class Logger
    {
        static bool Verbose { get; set; } = false;
        public static void SetVerbose(bool verbose)
        {
            Verbose = verbose;
        }
        public static void LogError(string message)
        {
            if (!Verbose) return;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(message);
            Console.ResetColor();
        }
        public static void LogWarning(string message)
        {
            if (!Verbose) return;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(message);
            Console.ResetColor();
        }
        public static void LogInfo(string message)
        {
            if (!Verbose) return;
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(message);
            Console.ResetColor();
        }
        public static void LogSuccess(string message)
        {
            if (!Verbose) return;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(message);
            Console.ResetColor();
        }
        public static void LogProgress(string message)
        {
            if (!Verbose) return;
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine(message);
            Console.ResetColor();
        }
    }
}
