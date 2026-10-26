using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using Microsoft.Win32;

namespace RefreshSentinel
{
    public sealed class SettingsStore
    {
        public readonly string DirectoryPath;
        public string FilePath { get { return Path.Combine(DirectoryPath, "settings.json"); } }

        public SettingsStore(string directory) { DirectoryPath = directory; }

        public AppSettings Load(out string warning)
        {
            warning = null;
            if (!File.Exists(FilePath)) return new AppSettings();
            try
            {
                using (var input = File.OpenRead(FilePath))
                {
                    if (input.Length > 1024 * 1024) throw new InvalidDataException("Túl nagy beállításfájl.");
                    var settings = (AppSettings)new DataContractJsonSerializer(typeof(AppSettings)).ReadObject(input);
                    if (settings == null) throw new InvalidDataException("Üres beállításfájl.");
                    settings.Validate();
                    return settings;
                }
            }
            catch (Exception ex)
            {
                warning = "A mentett beállítások nem olvashatók. Az automatikus felügyelet kikapcsolva maradt. " + ex.Message;
                return new AppSettings();
            }
        }

        public void Save(AppSettings settings)
        {
            settings.Validate();
            Directory.CreateDirectory(DirectoryPath);
            string temp = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    new DataContractJsonSerializer(typeof(AppSettings)).WriteObject(output, settings);
                    output.Flush(true);
                }
                if (File.Exists(FilePath)) File.Replace(temp, FilePath, FilePath + ".bak");
                else File.Move(temp, FilePath);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }

    public sealed class AppLog
    {
        private readonly string directory;
        private readonly object sync = new object();
        public AppLog(string directoryPath) { directory = directoryPath; }

        public void Write(string message)
        {
            lock (sync)
            {
                try
                {
                    Directory.CreateDirectory(directory);
                    string path = Path.Combine(directory, "events.log");
                    if (File.Exists(path) && new FileInfo(path).Length > 1024 * 1024)
                    {
                        string old = path + ".1";
                        if (File.Exists(old)) File.Delete(old);
                        File.Move(path, old);
                    }
                    File.AppendAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine, Encoding.UTF8);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }

    internal static class StartupRegistration
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        internal static string Command(string executable)
        {
            return "\"" + executable + "\" --tray";
        }

        public static bool IsEnabled(string executable)
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey, false))
                return key != null && String.Equals(key.GetValue("HzGuard") as string, Command(executable), StringComparison.OrdinalIgnoreCase);
        }

        public static void SetEnabled(bool enabled, string executable)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (key == null) throw new IOException("Az automatikus indítás nem állítható be.");
                if (enabled) key.SetValue("HzGuard", Command(executable), RegistryValueKind.String);
                else key.DeleteValue("HzGuard", false);
            }
        }
    }
}
