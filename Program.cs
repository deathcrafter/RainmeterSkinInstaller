using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.IO.Compression;
using CommandLine;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace RainmeterSkinInstaller
{
    public class Options
    {
        [Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.")]
        public bool Verbose { get; set; }

        [Option('s', "skin", Required = false, HelpText = "Path to the skin you want to install.")]
        public string SkinPath { get; set; }

        [Option('k', "keepvars", Required = false, HelpText = "Keep variables from the skin even if merge option is enabled")]
        public bool KeepVars { get; set; }

        [Option('n', "novariables", Required = false, HelpText = "Do not install variables from the skin")]
        public bool NoVariables { get; set; }

        [Option('x', "norestart", Required = false, HelpText = "Don't restart rainmeter after installation")]
        public bool NoRestart { get; set; }
    }
    public class Program
    {
        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                             .WithParsed(InstallSkin);

        }

        static void InstallSkin(Options options)
        {
            Logger.SetVerbose(options.Verbose);

            Console.WriteLine("Rainmeter Skin Installer v1.0.0");
            Logger.LogInfo("Copyright (c) 2023 by deathcrafter");
            Logger.LogInfo("Originally for Droptop Four maintained by Cariboudjan");

            Logger.LogInfo("\n================================================================\n");

            string skinPath = options.SkinPath;

            if (!File.Exists(skinPath))
            {
                Logger.LogError("The specified skin does not exist.");
                return;
            }

            if (!Path.GetExtension(skinPath).Equals(".rmskin"))
            {
                Logger.LogError("The specified skin is not a valid Rainmeter Skin Package.");
                return;
            }

            var (rmProgramPath, rmSettingsPath, rmSkinsPath) = Rainmeter.Installed();
            if (0 == rmSkinsPath.Length)
            {
                Logger.LogError("Rainmeter skins path not avaialble.");
                return;
            }

            ZipArchive archive = ZipFile.OpenRead(skinPath);

            ZipArchiveEntry skinIni = archive.GetEntry("RMSKIN.ini");
            if (skinIni == null)
            {
                Logger.LogError("The specified skin is not a valid RMSKIN file.");
                return;
            }

            string tempPath = JoinPath(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempPath);

            skinIni.ExtractToFile(JoinPath(tempPath, "RMSKIN.ini"), true);

            IniFile skinIniFile = new IniFile(JoinPath(tempPath, "RMSKIN.ini"));

            Dictionary<string, string> skinSettings = skinIniFile.GetSection("rmskin");

            Logger.LogSuccess("Read skin settings");

            string val;
            bool mergeSkins = skinSettings.TryGetValue("MergeSkins", out val) ? val == "1" : false;
            string[] variableFiles = skinSettings.TryGetValue("VariableFiles", out val) ? val.Split('|') : new string[0];
            variableFiles = variableFiles.AsEnumerable().Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToArray(); // remove empty entries and trim whitespace
            bool loadSkin = skinSettings.TryGetValue("LoadType", out val) ? val == "Skin" : false;
            string loadName = skinSettings.TryGetValue("Load", out val) ? val : "";

            // Logger.LogInfo("VariableFiles:\n    - " + string.Join("\n    - ", variableFiles));

            // return;

            string[] skins = archive.Entries.Where(e => IsFileSkin(e.FullName)).Select(e => e.FullName.Replace('/', '\\').Split('\\')[1]).Distinct().ToArray();
            Logger.LogInfo("Found " + skins.Length + " skins in the package.");

            if (Rainmeter.IsRunning())
            {
                Logger.LogInfo("Rainmeter is running. Trying to close Rainmeter.");

                if (!Rainmeter.CloseRainmeter())
                {
                    Logger.LogError("Failed to close Rainmeter.");
                    return;
                }// close Rainmeter to avoid file locks

                Logger.LogSuccess("Closed Rainmeter");
            }

            if (!mergeSkins)
                foreach (string skin in skins)
                {
                    Logger.LogInfo("Trying to backup skin: " + skin);
                    if (!CreateBackup(skin, rmSkinsPath, !mergeSkins))
                    {
                        return;
                    }
                }

            Logger.LogInfo("Starting skin installation...");
            Logger.LogInfo("...");
            Logger.LogProgress("\b\b\b");
            try
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    // install Skins
                    if (IsFileSkin(entry.FullName))
                    {
                        Logger.LogProgress("Extracting skin file: " + entry.FullName.Substring(6));
                        string filePath = JoinPath(rmSkinsPath, entry.FullName.Substring(6));

                        if (!Directory.GetParent(filePath).Exists)
                        {
                            Directory.CreateDirectory(Directory.GetParent(filePath).FullName);
                        }
                        entry.ExtractToFile(filePath, true);

                        // restore variables
                        if ((!mergeSkins || (mergeSkins && options.KeepVars)) && !options.NoVariables)
                        {
                            string varFile = entry.FullName.Substring(6);
                            string backupFile = JoinPath(rmSkinsPath, "@Backup", varFile);
                            if (variableFiles.Contains(varFile) && File.Exists(backupFile))
                            {
                                Logger.LogProgress("Restoring variable file: " + varFile);
                                Dictionary<string, string> oldVars = new IniFile(backupFile).GetSection("Variables");
                                IniFile newVars = new IniFile(filePath);

                                foreach (KeyValuePair<string, string> var in oldVars)
                                {
                                    newVars.Write(var.Key, var.Value, "Variables");
                                }
                            }
                        }
                    }
                    // install Plugins
                    else if (IsFilePlugin(entry.FullName))
                    {
                        Logger.LogProgress("Installing plugin: " + entry.Name);
                        string tempPluginPath = JoinPath(tempPath, entry.Name);
                        string filePath = JoinPath(rmSettingsPath, "Plugins", entry.Name);

                        bool isGreater = true;

                        if (File.Exists(filePath))
                        {
                            entry.ExtractToFile(tempPluginPath, true);
                            isGreater = IsPluginVersionGreator(tempPluginPath, filePath);
                        }

                        if (isGreater)
                        {
                            Logger.LogInfo("Installing Plugin: " + entry.FullName);
                            try
                            {
                                if (!Directory.GetParent(filePath).Exists)
                                {
                                    Directory.CreateDirectory(System.IO.Directory.GetParent(filePath).FullName);
                                }
                                entry.ExtractToFile(filePath, true);
                            }
                            catch (Exception e)
                            {
                                Logger.LogError("Failed to extract Plugin: " + entry.FullName);
                                Logger.LogError(e.Message);
                                Logger.LogError(e.StackTrace);
                                RestoreBackup(skins, rmSkinsPath);
                                return;
                            }
                        }
                    }
                    // install Layouts
                    else if (IsFileLayout(entry.FullName))
                    {
                        Logger.LogProgress("Installing layout file: " + entry.FullName.Substring(8));
                        string filePath = JoinPath(rmSettingsPath, Regex.Replace(entry.FullName, @"^Layouts/", ""));
                        if (!Directory.GetParent(filePath).Exists)
                        {
                            Directory.CreateDirectory(Directory.GetParent(filePath).FullName);
                        }
                        entry.ExtractToFile(filePath, true);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogError("Failed to install skin.");
                Logger.LogError(e.Message);

                Logger.LogInfo("Trying to restore backup.");
                RestoreBackup(skins, rmSkinsPath);
                Logger.LogSuccess("Restored backup.");

                return;
            }
            Logger.LogProgress("");

            Logger.LogSuccess($"Successfully installed skins:\n    - {string.Join("\n    - ", skins)}");

            if (!options.NoRestart)
                Rainmeter.StartRainmeter(loadSkin, loadName);
        }

        static bool IsPluginVersionGreator(string newPlugin, string oldPlugin)
        {
            FileVersionInfo newVersion = FileVersionInfo.GetVersionInfo(newPlugin);
            FileVersionInfo oldVersion = FileVersionInfo.GetVersionInfo(oldPlugin);

            if (newVersion.FileMajorPart < oldVersion.FileMajorPart) return false; else if (newVersion.FileMajorPart > oldVersion.FileMajorPart) return true;
            if (newVersion.FileMinorPart < oldVersion.FileMinorPart) return false; else if (newVersion.FileMinorPart > oldVersion.FileMinorPart) return true;
            if (newVersion.FileBuildPart < oldVersion.FileBuildPart) return false; else if (newVersion.FileBuildPart > oldVersion.FileBuildPart) return true;
            if (newVersion.FilePrivatePart <= oldVersion.FilePrivatePart) return false;

            return true;
        }

        static void RestoreBackup(string[] skins, string skinsPath)
        {
            try
            {
                foreach (string skin in skins)
                {
                    string skinPath = JoinPath(skinsPath, skin);
                    string backupPath = JoinPath(skinsPath, "@Backup", skin);

                    CopyDirRecurse(backupPath, skinPath);
                }
            } catch {
                Logger.LogError("Failed to restore backup.");
            }
        }

        static bool CreateBackup(string skin, string skinsPath, bool move)
        {
            try
            {
                string skinPath = JoinPath(skinsPath, skin);
                string backupPath = JoinPath(skinsPath, "@Backup", skin);

                if (!Directory.Exists(skinPath))
                {
                    Logger.LogSuccess("Skin does not exist. No need to backup.");
                    return true;
                }

                if (Directory.Exists(backupPath))
                {
                    Directory.Delete(backupPath, true);
                }

                if (move) Directory.Move(skinPath, backupPath);
                else CopyDirRecurse(skinPath, backupPath);

                Logger.LogSuccess("Backed up skin: " + skin);
                return true;
            }
            catch (Exception e)
            {
                Logger.LogError("Failed to create backup of skin.");
                Logger.LogError(e.Message);
                return false;
            }
        }
        private static bool IsFileSkin(string file)
        {
            return file.Replace('/', '\\').StartsWith("Skins\\") && !file.Replace('/', '\\').EndsWith("\\");
        }
        private static bool IsFilePlugin(string file)
        {
            return file.Replace('/', '\\').StartsWith("Plugins\\64bit\\") && !file.Replace('/', '\\').EndsWith("\\");
        }
        private static bool IsFileLayout(string file)
        {
            return file.Replace('/', '\\').StartsWith("Layouts\\") && !file.Replace('/', '\\').EndsWith("\\");
        }
        private static string JoinPath(params string[] args)
        {
            return Path.Combine(args);
        }

        private static bool CopyDirRecurse(string src, string dest)
        {
            if (!Directory.Exists(src))
            {
                Logger.LogError("Source directory does not exist.");
                return false;
            }

            try
            {
                foreach (string dirPath in Directory.GetDirectories(src, "*", SearchOption.AllDirectories))
                {
                    Directory.CreateDirectory(Regex.Replace(src, "^" + Regex.Escape(src), dest));

                    foreach (string filePath in Directory.GetFiles(dirPath))
                    {
                        File.Copy(filePath, Regex.Replace(filePath, "^" + Regex.Escape(src), dest), true);
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
