using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace RainmeterSkinInstaller
{
    internal class IniFile
    {
        string Path;
        string EXE = Assembly.GetExecutingAssembly().GetName().Name;

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        static extern long WritePrivateProfileString(string Section, string Key, string Value, string FilePath);

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        static extern int GetPrivateProfileString(string Section, string Key, string Default, StringBuilder RetVal, int Size, string FilePath);

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        static extern int GetPrivateProfileSectionNames(byte[] lpszReturnBuffer, int nSize, string lpFileName);

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        static extern int GetPrivateProfileSection(string lpAppName, char[] buffer, int nSize, string lpFileName);

        public IniFile(string IniPath = null)
        {
            Path = new FileInfo(IniPath ?? EXE + ".ini").FullName;
        }

        public string[] GetSectionNames()
        {
            byte[] buffer = new byte[2048];
            int len = GetPrivateProfileSectionNames(buffer, buffer.Length, Path);
            if (len == 0)
            {
                return new string[0];
            }
            string[] sections = Encoding.Unicode.GetString(buffer, 0, len - 1).Split('\0');
            return sections;
        }

        public Dictionary<string, string> GetSection(string Section)
        {
            char[] buffer = new char[short.MaxValue];
            // buffer.Length = short.MaxValue - 1;
            int len = GetPrivateProfileSection(Section, buffer, short.MaxValue, Path);
            if (len == 0)
            {
                const string msg = "The specified section does not exist.";
                throw new ArgumentException(msg, "section");
            }

            Dictionary<string, string> ret = new Dictionary<string, string>();

            string key = "";
            string value = "";
            int i = 0;
            string curr = "";
            bool wasLastNull = false;
            foreach (var c in buffer)
            {
                if (c == '\0')
                {
                    if (wasLastNull)
                    {
                        break;
                    }
                    wasLastNull = true;
                    ret[key] = value = curr;
                    curr = "";
                }
                else if (c == '=')
                {
                    wasLastNull = false;
                    key = curr;
                    curr = "";
                }
                else
                {
                    wasLastNull = false;
                    curr += c;
                }
                i++;
            }
            
            return ret;
        }

        public string Read(string Key, string Section = null)
        {
            var RetVal = new StringBuilder(255);
            GetPrivateProfileString(Section ?? EXE, Key, "", RetVal, 255, Path);
            return RetVal.ToString();
        }

        public void Write(string Key, string Value, string Section = null)
        {
            WritePrivateProfileString(Section ?? EXE, Key, Value, Path);
        }

        public void DeleteKey(string Key, string Section = null)
        {
            Write(Key, null, Section ?? EXE);
        }

        public void DeleteSection(string Section = null)
        {
            Write(null, null, Section ?? EXE);
        }

        public bool KeyExists(string Key, string Section = null)
        {
            return Read(Key, Section).Length > 0;
        }
    }
}
