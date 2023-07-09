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
            Console.Error.WriteLine(";o; : " + message);
            Console.ResetColor();
        }
        public static void LogWarning(string message)
        {
            if (!Verbose) return;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("'~' : " + message);
            Console.ResetColor();
        }
        public static void LogInfo(string message)
        {
            if (!Verbose) return;
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("'o' : " + message);
            Console.ResetColor();
        }
        public static void LogSuccess(string message)
        {
            if (!Verbose) return;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("'_' : " + message);
            Console.ResetColor();
        }
        public static void LogProgress(string message)
        {
            if (!Verbose) return;
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine(message);
            Console.ResetColor();
        }
    }
}
