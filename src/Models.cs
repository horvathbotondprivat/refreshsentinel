using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace RefreshSentinel
{
    [DataContract]
    public sealed class AppSettings
    {
        [DataMember] public int Version = 1;
        [DataMember] public bool Enabled;
        [DataMember] public int IntervalSeconds = 30;
        [DataMember] public bool NotifyOnRestore = true;
        [DataMember] public List<MonitorRule> Monitors = new List<MonitorRule>();

        public void Validate()
        {
            if (Version != 1) throw new InvalidOperationException("Ismeretlen beállításverzió.");
            if (IntervalSeconds < 5 || IntervalSeconds > 3600)
                throw new InvalidOperationException("Az ellenőrzési időköz 5 és 3600 másodperc közötti lehet.");
            if (Monitors == null || Monitors.Count > 128)
                throw new InvalidOperationException("Érvénytelen monitorlista.");
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var rule in Monitors)
            {
                if (rule == null || String.IsNullOrWhiteSpace(rule.Id) || !ids.Add(rule.Id))
                    throw new InvalidOperationException("Hiányzó vagy ismétlődő monitorazonosító.");
                if (rule.Hertz < 2 || rule.Hertz > 10000)
                    throw new InvalidOperationException("Érvénytelen képfrissítés.");
            }
        }

        public AppSettings Copy()
        {
            return new AppSettings {
                Version = Version, Enabled = Enabled, IntervalSeconds = IntervalSeconds,
                NotifyOnRestore = NotifyOnRestore,
                Monitors = Monitors.Select(r => r.Copy()).ToList()
            };
        }
    }

    [DataContract]
    public sealed class MonitorRule
    {
        [DataMember] public string Id;
        [DataMember] public string Name;
        [DataMember] public int Hertz;
        [DataMember] public bool Enabled;

        public MonitorRule Copy()
        {
            return new MonitorRule { Id = Id, Name = Name, Hertz = Hertz, Enabled = Enabled };
        }
    }

    public sealed class DisplaySnapshot
    {
        public string Id;
        public string Name;
        public string DeviceName;
        public int Number;
        public int Width;
        public int Height;
        public int CurrentHertz;
        public double SignalHertz;
        public bool Primary;
        public bool Cloned;
        public bool Connected = true;
        public List<int> SupportedHertz = new List<int>();
        public string Resolution { get { return Width + " × " + Height; } }
    }

    public sealed class ApplyResult
    {
        public bool Success;
        public string Message;
        public static ApplyResult Ok(string message) { return new ApplyResult { Success = true, Message = message }; }
        public static ApplyResult Fail(string message) { return new ApplyResult { Success = false, Message = message }; }
    }

    public interface IDisplayService
    {
        List<DisplaySnapshot> GetDisplays();
        ApplyResult Restore(string monitorId, int hertz);
    }

    public sealed class MonitorStatus
    {
        public string Id;
        public string Message;
        public bool Warning;
        public bool Restored;
    }

    public sealed class CheckReport
    {
        public DateTime TimeUtc;
        public List<DisplaySnapshot> Displays = new List<DisplaySnapshot>();
        public List<MonitorStatus> Statuses = new List<MonitorStatus>();
        public string Error;
        public int RestoredCount { get { return Statuses.Count(s => s.Restored); } }
    }
}
