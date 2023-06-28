using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace RainmeterSkinInstaller
{
	public static class Rainmeter
	{
		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

		[DllImport("user32.dll", EntryPoint = "PostMessageW", CharSet = CharSet.Unicode)]
		static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		static extern long GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

		[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
		static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

		[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
		static extern bool CloseHandle(IntPtr hObject);

		[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
		static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

		[DllImport("kernel32.dll", SetLastError = true)]
		static extern bool GetExitCodeProcess(IntPtr Handle, out uint Wait);

		const string RAINMETER_CLASS = "DummyRainWClass";
		const string RAINMETER_WINDOW = "Rainmeter control window";
		static string PROGRAM_PATH = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Rainmeter", "Rainmeter.exe");
		static string SETTINGS_PATH = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Rainmeter");

		public static (string, string, string) Installed()
		{
			Logger.LogInfo("Checking if Rainmeter is installed.");
			Logger.LogInfo($"Rainmeter program path: {PROGRAM_PATH}");
			Logger.LogInfo($"Rainmeter settings path: {SETTINGS_PATH}");

			if (!File.Exists(PROGRAM_PATH))
			{
				Logger.LogError("Rainmeter is not installed.");
				return ("", "", "");
			}

			string settingsPath = Path.Combine(SETTINGS_PATH, "Rainmeter.ini");
			if (!File.Exists(settingsPath))
			{
				Logger.LogError("Rainmeter has not been run for the first time.");
				return ("", "", "");
			}

			IniFile ini = new IniFile(settingsPath);
			string skinPath = ini.Read("SkinPath", "Rainmeter");
			if (Directory.CreateDirectory(skinPath).Exists)
			{
				Logger.LogSuccess("Rainmeter is installed.");
			}

			return (PROGRAM_PATH, SETTINGS_PATH, skinPath);
		}

		public static bool IsRunning()
		{
			return FindWindow(RAINMETER_CLASS, RAINMETER_WINDOW) != IntPtr.Zero;
		}

		public static bool CloseRainmeter()
		{
			if (IsRunning())
			{
				Logger.LogInfo("Closing Rainmeter.");
				Process.Start(PROGRAM_PATH, "[!Quit]");
			}

			int i = 0;
			while (IsRunning())
			{
				Thread.Sleep(100);
				if (i++ > 100) // 10 seconds
				{
                    return false;
                }
				if (i > 50) // try killing the process using taskkill after 5 seconds
				{
					Logger.LogWarning("Rainmeter is not closing. Attempting to kill the process.");
					Process.Start("taskkill", "/f /im Rainmeter.exe");
				}
			}
			return true;
		}

		public static void StartRainmeter(bool loadSkin, string loadName)
		{
			if (!File.Exists(PROGRAM_PATH))
			{
				throw new FileNotFoundException("Rainmeter.exe not found.", PROGRAM_PATH);
			}

			if (Program.IsElevated)
			{
				SystemUtility.ExecuteProcessUnElevated(PROGRAM_PATH); // try to start unelevated

				int j = 0;
				while (!IsRunning()) // wait for Rainmeter to start
				{
					Thread.Sleep(100);
					if (++j == 50) // 5 seconds
					{
						Process.Start(PROGRAM_PATH); // try to start elevated
						Logger.LogWarning("Rainmeter could not be started unelevated.");
						Logger.LogWarning("Trying to start Rainmeter elevated.");
						Logger.LogWarning("Even if this succeeds, please restart Rainmeter manually.");
					}
					if (j > 100) // 10 seconds
					{
                        throw new TimeoutException("Rainmeter did not start.");
                    }
				}
			}
			else
			{
                Process.Start(PROGRAM_PATH); // try to start normally

                int i = 0;
                while (!IsRunning()) // wait for Rainmeter to start
                {
                    Thread.Sleep(100);
                    if (i++ > 100) // 10 seconds
                    {
                        throw new TimeoutException("Rainmeter did not start.");
                    }
                }
            }

			// send the command to load the skin/layout
			if (!string.IsNullOrEmpty(loadName))
				if (loadSkin)
				{
					LoadSkin(loadName);
				}
				else
				{
					Process.Start(PROGRAM_PATH, "[!LoadLayout \"" + loadName + "\"]");
				}
		}

		public static void LoadSkin(string skin)
		{
			string file = skin.Substring(skin.LastIndexOf('\\') + 1);
			string config = skin.Substring(0, skin.LastIndexOf('\\'));

			Process.Start(PROGRAM_PATH, "[!ActivateConfig \"" + config + "\" \"" + file + "\"]");
		}
	}
}
