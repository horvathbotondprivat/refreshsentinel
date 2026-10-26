using System;
using System.Collections.Generic;
using System.Linq;

namespace RefreshSentinel
{
    public sealed class GuardEngine
    {
        private readonly IDisplayService displays;
        private readonly Action<string> log;
        private readonly Dictionary<string, DateTime> retryAfter = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> lastMessages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public GuardEngine(IDisplayService service, Action<string> logger)
        {
            displays = service;
            log = logger ?? delegate { };
        }

        // The application serializes all checks. No native display change runs concurrently.
        public CheckReport Check(AppSettings settings, bool enforce, DateTime nowUtc)
        {
            settings.Validate();
            var report = new CheckReport { TimeUtc = nowUtc };
            try { report.Displays = displays.GetDisplays(); }
            catch (Exception ex)
            {
                report.Error = "A monitorok lekérdezése sikertelen: " + ex.Message;
                Record("query", report.Error);
                return report;
            }

            foreach (var rule in settings.Monitors.Where(r => r.Enabled))
            {
                var status = new MonitorStatus { Id = rule.Id };
                var matches = report.Displays.Where(d => String.Equals(d.Id, rule.Id, StringComparison.OrdinalIgnoreCase)).ToList();
                var display = matches.Count == 1 ? matches[0] : null;
                if (display == null || !display.Connected)
                    status.Message = matches.Count > 1 ? "Nem egyértelmű monitorazonosító; nincs módosítás." : "Nincs csatlakoztatva; a beállítás megmarad.";
                else if (display.Cloned)
                {
                    status.Warning = true;
                    status.Message = "Tükrözött kijelző: külön Hz nem állítható. Használd a Kiterjesztés módot.";
                }
                else if (!display.SupportedHertz.Contains(rule.Hertz))
                {
                    status.Warning = true;
                    status.Message = rule.Hertz + " Hz nem érhető el a jelenlegi felbontásnál.";
                }
                else if (display.CurrentHertz == rule.Hertz)
                {
                    status.Message = "Rendben · " + rule.Hertz + " Hz";
                }
                else if (!enforce)
                    status.Message = "Eltérés: " + display.CurrentHertz + " → " + rule.Hertz + " Hz · felügyelet szünetel";
                else
                {
                    DateTime retry;
                    if (retryAfter.TryGetValue(rule.Id, out retry) && nowUtc < retry)
                    {
                        status.Warning = true;
                        status.Message = "Újrapróbálás " + Math.Ceiling((retry - nowUtc).TotalSeconds) + " mp múlva.";
                    }
                    else
                    {
                        ApplyResult result;
                        try { result = displays.Restore(rule.Id, rule.Hertz); }
                        catch (Exception ex) { result = ApplyResult.Fail(ex.Message); }
                        status.Restored = result.Success;
                        status.Warning = !result.Success;
                        status.Message = result.Success ? "Visszaállítva · " + rule.Hertz + " Hz" : result.Message;
                        // Also back off after success if another application keeps changing the mode.
                        retryAfter[rule.Id] = nowUtc.AddSeconds(result.Success ? 15 : 60);
                    }
                }
                report.Statuses.Add(status);
                Record(rule.Id, (rule.Name ?? "Monitor") + ": " + status.Message);
            }
            if (report.RestoredCount > 0)
            {
                try { report.Displays = displays.GetDisplays(); }
                catch (Exception ex) { report.Error = "A visszaellenőrzés nem teljes: " + ex.Message; }
            }
            return report;
        }

        private void Record(string id, string message)
        {
            string previous;
            if (!lastMessages.TryGetValue(id, out previous) || previous != message)
            {
                lastMessages[id] = message;
                // A changing cooldown countdown must not fill the log every five seconds.
                if (!message.Contains("Újrapróbálás")) log(message);
            }
        }
    }
}
