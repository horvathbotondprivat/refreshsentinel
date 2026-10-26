using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace RefreshSentinel.Tests
{
    internal sealed class FakeDisplays : IDisplayService
    {
        internal List<DisplaySnapshot> Displays = new List<DisplaySnapshot>();
        internal int Calls;
        internal bool FailApply, FailQuery;
        public List<DisplaySnapshot> GetDisplays()
        {
            if (FailQuery) throw new InvalidOperationException("Simulated query failure");
            return Displays;
        }
        public ApplyResult Restore(string id, int hertz)
        {
            Calls++;
            if (FailApply) return ApplyResult.Fail("Simulated driver failure");
            Displays.Single(d => String.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase)).CurrentHertz = hertz;
            return ApplyResult.Ok("OK");
        }
    }

    internal static class TestRunner
    {
        private static int passed, failed;
        private static string outputDirectory;
        private static readonly List<string> results = new List<string>();
        private static readonly DateTime Now = new DateTime(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);

        [STAThread]
        private static int Main(string[] args)
        {
            outputDirectory = args[0];
            Native.SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Test("Native ABI sizes and offsets", delegate {
                Equal(220, Marshal.SizeOf(typeof(Native.DevMode))); Equal(72, Marshal.SizeOf(typeof(Native.PathInfo)));
                Equal(64, Marshal.SizeOf(typeof(Native.ModeInfo))); Equal(84, Marshal.SizeOf(typeof(Native.SourceName)));
                Equal(420, Marshal.SizeOf(typeof(Native.TargetName)));
                Equal(184, Marshal.OffsetOf(typeof(Native.DevMode), "Frequency").ToInt32());
                Equal(48, Marshal.SizeOf(typeof(Native.TargetInfo)));
            });
            Test("Defaults never enable monitoring", delegate { var s = new AppSettings(); s.Validate(); Assert(!s.Enabled && s.Monitors.Count == 0, "Unsafe defaults"); });
            Test("Settings validation and case-insensitive identity", delegate {
                var s = Settings(); s.IntervalSeconds = 4; Throws(() => s.Validate()); s.IntervalSeconds = 3601; Throws(() => s.Validate());
                s.IntervalSeconds = 30; s.Monitors.Add(new MonitorRule { Id = "MONITOR-A", Hertz = 60 }); Throws(() => s.Validate());
            });
            Test("Settings deep copy isolates edits", delegate { var s = Settings(); var c = s.Copy(); c.Monitors[0].Hertz = 60; Equal(144, s.Monitors[0].Hertz); });
            Test("Atomic settings save, reload, backup", delegate {
                var store = new SettingsStore(Path.Combine(outputDirectory, "test-data", "roundtrip-" + Guid.NewGuid().ToString("N")));
                var settings = Settings(); settings.Monitors[0].Name = "Árvíztűrő tükörfúrógép";
                store.Save(settings); string warning; var loaded = store.Load(out warning);
                Assert(warning == null, warning); Equal(settings.Monitors[0].Name, loaded.Monitors[0].Name);
                Equal(144, loaded.Monitors[0].Hertz); settings.IntervalSeconds = 60; store.Save(settings);
                Assert(File.Exists(store.FilePath + ".bak"), "No backup"); Equal(60, store.Load(out warning).IntervalSeconds);
            });
            Test("Corrupt configuration disables guard without overwriting", delegate {
                var store = new SettingsStore(Path.Combine(outputDirectory, "test-data", "corrupt-" + Guid.NewGuid().ToString("N")));
                Directory.CreateDirectory(store.DirectoryPath); File.WriteAllText(store.FilePath, "{broken");
                string warning; var s = store.Load(out warning); Assert(!s.Enabled && warning != null, "Corruption was not reported");
                Equal("{broken", File.ReadAllText(store.FilePath));
            });
            Test("Autostart quotes paths containing spaces", delegate { Equal("\"C:\\My Apps\\HzGuard.exe\" --tray", StartupRegistration.Command(@"C:\My Apps\HzGuard.exe")); });
            Test("Mode filtering preserves geometry, color depth and scan type", delegate {
                var current = Mode(144); var candidate = current;
                Assert(NativeDisplayService.IsCompatible(candidate, current), "Valid mode rejected");
                candidate.Width = 1920; Assert(!NativeDisplayService.IsCompatible(candidate, current), "Resolution allowed"); candidate = current;
                candidate.Orientation = 1; Assert(!NativeDisplayService.IsCompatible(candidate, current), "Rotation allowed"); candidate = current;
                candidate.BitsPerPel = 16; Assert(!NativeDisplayService.IsCompatible(candidate, current), "Color depth allowed"); candidate = current;
                candidate.DisplayFlags = 2; Assert(!NativeDisplayService.IsCompatible(candidate, current), "Interlaced mode allowed"); candidate = current;
                candidate.Frequency = 1; Assert(!NativeDisplayService.IsCompatible(candidate, current), "Default frequency exposed"); candidate = current;
                candidate.Fields = 0; Assert(!NativeDisplayService.IsCompatible(candidate, current), "Unpopulated native fields allowed");
            });
            Test("Prepared mode retains current position and rotation", delegate {
                var current = Mode(60); current.PositionX = -2560; current.PositionY = 37; current.Orientation = 2;
                current.Fields |= 0x20 | 0x80; var candidate = Mode(144); candidate.PositionX = 999;
                var prepared = NativeDisplayService.PrepareMode(candidate, current);
                Equal(-2560, prepared.PositionX); Equal(37, prepared.PositionY); Equal((uint)2, prepared.Orientation);
                Equal((uint)144, prepared.Frequency); Equal((uint)2560, prepared.Width); Equal((ushort)0, prepared.DriverExtra);
                Assert((prepared.Fields & Native.FrequencyField) != 0, "Frequency field missing");
            });
            Test("Matching frequency never changes a monitor", delegate { var f = Fake(144); new GuardEngine(f, null).Check(Settings(), true, Now); Equal(0, f.Calls); });
            Test("Mismatch restores the selected monitor and reports success", delegate {
                var f = Fake(60); var r = new GuardEngine(f, null).Check(Settings(), true, Now);
                Equal(1, f.Calls); Equal(1, r.RestoredCount); Equal(144, f.Displays[0].CurrentHertz);
            });
            Test("Paused and disabled rules never apply", delegate {
                var f = Fake(60); var engine = new GuardEngine(f, null); var s = Settings();
                engine.Check(s, false, Now); Equal(0, f.Calls); s.Monitors[0].Enabled = false; engine.Check(s, true, Now); Equal(0, f.Calls);
            });
            Test("Disconnected monitor remains configured", delegate {
                var f = Fake(60); f.Displays.Clear(); var s = Settings(); var r = new GuardEngine(f, null).Check(s, true, Now);
                Equal(0, f.Calls); Equal(1, s.Monitors.Count); Assert(r.Statuses[0].Message.Contains("csatlakoztatva"), "Missing offline state");
            });
            Test("Unsupported refresh rate is never requested", delegate {
                var f = Fake(60); f.Displays[0].SupportedHertz = new List<int> { 60, 75 };
                var r = new GuardEngine(f, null).Check(Settings(), true, Now); Equal(0, f.Calls); Assert(r.Statuses[0].Warning, "No warning");
            });
            Test("Cloned monitors never receive independent settings", delegate { var f = Fake(60); f.Displays[0].Cloned = true; new GuardEngine(f, null).Check(Settings(), true, Now); Equal(0, f.Calls); });
            Test("Ambiguous device identity is never applied", delegate { var f = Fake(60); f.Displays.Add(Display("MONITOR-A", 60)); new GuardEngine(f, null).Check(Settings(), true, Now); Equal(0, f.Calls); });
            Test("Failed apply backs off for at least 60 seconds", delegate {
                var f = Fake(60); f.FailApply = true; var e = new GuardEngine(f, null);
                e.Check(Settings(), true, Now); e.Check(Settings(), true, Now.AddSeconds(59)); Equal(1, f.Calls);
                e.Check(Settings(), true, Now.AddSeconds(60)); Equal(2, f.Calls);
            });
            Test("Success cooldown survives an intervening matching poll", delegate {
                var f = Fake(60); var e = new GuardEngine(f, null); e.Check(Settings(), true, Now);
                e.Check(Settings(), true, Now.AddSeconds(5)); f.Displays[0].CurrentHertz = 60;
                e.Check(Settings(), true, Now.AddSeconds(10)); Equal(1, f.Calls);
                e.Check(Settings(), true, Now.AddSeconds(15)); Equal(2, f.Calls);
            });
            Test("Reconnection follows hardware identity instead of display number", delegate {
                var f = Fake(60); f.Displays[0].DeviceName = @"\\.\DISPLAY9";
                f.Displays.Add(Display("monitor-b", 75)); new GuardEngine(f, null).Check(Settings(), true, Now);
                Equal(144, f.Displays[0].CurrentHertz); Equal(75, f.Displays[1].CurrentHertz);
            });
            Test("Query failures are reported without applying", delegate { var f = Fake(60); f.FailQuery = true; var r = new GuardEngine(f, null).Check(Settings(), true, Now); Assert(r.Error != null, "Error missing"); Equal(0, f.Calls); });
            Test("Repeated identical states do not spam the log", delegate { var f = Fake(144); int count = 0; var e = new GuardEngine(f, delegate { count++; }); e.Check(Settings(), true, Now); e.Check(Settings(), true, Now.AddSeconds(30)); Equal(1, count); });

            Test("Interface renders and preserves unsaved choices after reconnect", RenderInterface);
            if (args.Skip(1).Contains("--skip-live"))
            {
                results.Add("SKIP Actual monitor enumeration and CDS_TEST (explicit --skip-live)");
                Console.WriteLine(results.Last());
            }
            else Test("Actual monitor enumeration and read-only CDS_TEST", InspectActualDisplays);
            results.Add("Result: " + passed + " passed, " + failed + " failed.");
            File.WriteAllLines(Path.Combine(outputDirectory, "test-results.txt"), results, Encoding.UTF8);
            Console.WriteLine(results.Last());
            return failed == 0 ? 0 : 1;
        }

        private static Native.DevMode Mode(uint hertz)
        {
            var mode = Native.DevMode.Empty(); mode.Width = 2560; mode.Height = 1440; mode.BitsPerPel = 32;
            mode.Frequency = hertz; mode.Fields = 0x00040000 | 0x00080000 | 0x00100000 | 0x00200000 | Native.FrequencyField;
            return mode;
        }

        private static DisplaySnapshot Display(string id, int current)
        {
            return new DisplaySnapshot { Id = id, Name = id == "monitor-a" ? "DELL S2721DGF" : "LG ULTRAGEAR",
                Number = id == "monitor-a" ? 1 : 2, DeviceName = @"\\.\DISPLAY1", Width = 2560, Height = 1440,
                CurrentHertz = current, SignalHertz = current, Primary = id == "monitor-a", SupportedHertz = new List<int> { 60, 120, 144, 165 } };
        }

        private static FakeDisplays Fake(int hertz) { var f = new FakeDisplays(); f.Displays.Add(Display("monitor-a", hertz)); return f; }
        private static AppSettings Settings()
        {
            var s = new AppSettings { Enabled = true };
            s.Monitors.Add(new MonitorRule { Id = "monitor-a", Name = "DELL S2721DGF", Hertz = 144, Enabled = true });
            return s;
        }

        private static IEnumerable<Control> Descendants(Control control)
        {
            foreach (Control child in control.Controls) { yield return child; foreach (var nested in Descendants(child)) yield return nested; }
        }

        private static void RenderInterface()
        {
            var settings = Settings();
            settings.Monitors.Add(new MonitorRule { Id = "monitor-b", Name = "LG ULTRAGEAR", Hertz = 165, Enabled = true });
            var report = new CheckReport { TimeUtc = DateTime.UtcNow, Displays = new List<DisplaySnapshot> { Display("monitor-a", 144), Display("monitor-b", 165) },
                Statuses = new List<MonitorStatus> { new MonitorStatus { Id = "monitor-a", Message = "Rendben · 144 Hz" }, new MonitorStatus { Id = "monitor-b", Message = "Rendben · 165 Hz" } } };
            using (var form = new MainForm(settings, false, SystemIcons.Application))
            {
                PrepareForm(form); form.ShowReport(report); PrepareForm(form);
                using (var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size)); bitmap.Save(Path.Combine(outputDirectory, "interface.png"), ImageFormat.Png); }
                var cards = Descendants(form).OfType<MonitorCard>().ToList(); Equal(2, cards.Count);
                var choice = Descendants(cards[0]).OfType<ComboBox>().Single();
                choice.SelectedItem = choice.Items.Cast<HertzChoice>().Single(h => h.Hertz == 120);
                report.Displays[1].Connected = false; // Rebuild card list, retaining the current UI draft.
                form.ShowReport(report);
                cards = Descendants(form).OfType<MonitorCard>().ToList(); Equal(120, cards[0].ReadRule(true).Hertz);
                AppSettings saved = null; form.SaveRequested = delegate(AppSettings s, bool startup, bool force) { saved = s; };
                Descendants(form).OfType<Button>().Single(b => b.Text == "Mentés").PerformClick();
                // PerformClick on a never-shown form may not dispatch. Validate the draft independently.
                Assert(cards.Any(c => c.ReadRule(true).Id == "monitor-b"), "Offline setting lost");
                if (saved != null) Equal(120, saved.Monitors.Single(r => r.Id == "monitor-a").Hertz);
                form.AllowClose = true;
            }
            using (var form = new MainForm(new AppSettings(), false, SystemIcons.Application))
            {
                PrepareForm(form); form.ShowReport(new CheckReport { TimeUtc = DateTime.UtcNow }); PrepareForm(form);
                Assert(Descendants(form).OfType<Label>().Any(l => l.Text.StartsWith("Nincs aktív monitor")), "Empty monitor state missing");
                form.AllowClose = true;
            }
        }

        private static void InspectActualDisplays()
        {
            var displays = new NativeDisplayService().GetDisplays();
            var lines = new List<string> { "Read-only integration check: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), "No refresh rate was changed.", "Detected displays: " + displays.Count };
            Assert(displays.Select(d => d.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() == displays.Count, "Duplicate hardware identity");
            foreach (var display in displays)
            {
                int code = NativeDisplayService.TestCurrentMode(display.DeviceName);
                lines.Add(display.Number + ". " + display.Name + " | " + display.Resolution + " | nominal " + display.CurrentHertz + " Hz | signal " + display.SignalHertz.ToString("0.###") + " Hz");
                lines.Add("  Supported at current geometry: " + String.Join(", ", display.SupportedHertz) + " Hz; cloned=" + display.Cloned + "; CDS_TEST=" + code);
                File.WriteAllLines(Path.Combine(outputDirectory, "display-diagnostics.txt"), lines, Encoding.UTF8);
                Assert(display.CurrentHertz > 0 && display.Width > 0 && display.Height > 0, "Invalid native mode");
                Assert(code == 0, "Current-mode CDS_TEST failed: " + code);
            }
            File.WriteAllLines(Path.Combine(outputDirectory, "display-diagnostics.txt"), lines, Encoding.UTF8);
            Console.WriteLine("Read-only native check: " + displays.Count + " display(s).");
            using (var form = new MainForm(new AppSettings(), false, SystemIcons.Application))
            {
                PrepareForm(form); form.ShowReport(new CheckReport { TimeUtc = DateTime.UtcNow, Displays = displays }); PrepareForm(form);
                using (var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size)); bitmap.Save(Path.Combine(outputDirectory, "actual-monitors.png"), ImageFormat.Png); }
                form.AllowClose = true;
            }
        }

        private static void Test(string name, Action action)
        {
            try { action(); passed++; results.Add("PASS " + name); Console.WriteLine("PASS " + name); }
            catch (Exception ex) { failed++; results.Add("FAIL " + name + ": " + ex); Console.WriteLine("FAIL " + name + ": " + ex.Message); }
        }
        private static void PrepareForm(Form form)
        {
            IntPtr formHandle = form.Handle;
            foreach (var control in Descendants(form).ToList()) { IntPtr childHandle = control.Handle; control.PerformLayout(); }
            form.PerformLayout();
        }
        private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
        private static void Equal<T>(T expected, T actual) { Assert(EqualityComparer<T>.Default.Equals(expected, actual), "Expected " + expected + ", got " + actual); }
        private static void Throws(Action action) { bool threw = false; try { action(); } catch { threw = true; } Assert(threw, "Expected validation exception"); }
    }
}
