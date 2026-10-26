using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace RefreshSentinel
{
    internal sealed class TrayApplication : ApplicationContext
    {
        private readonly SettingsStore store;
        private readonly AppLog log;
        private readonly GuardEngine engine;
        private readonly MainForm form;
        private readonly NotifyIcon tray;
        private readonly Icon icon;
        private readonly System.Windows.Forms.Timer timer;
        private readonly EventWaitHandle showSignal;
        private readonly ToolStripMenuItem pause;
        private readonly string executable;
        private AppSettings settings;
        private DateTime nextCheck;
        private bool busy, suspended, sessionLocked, exiting, disposed;
        private bool hiddenHintShown;

        internal TrayApplication(bool startHidden, EventWaitHandle signal)
        {
            showSignal = signal;
            executable = Application.ExecutablePath;
            // Retain the original prototype's storage location to preserve existing preferences.
            string data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HzGuard");
            store = new SettingsStore(data); log = new AppLog(data);
            string warning;
            settings = store.Load(out warning);
            engine = new GuardEngine(new NativeDisplayService(), log.Write);
            using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("RefreshSentinel.App.ico"))
                icon = resource == null ? (Icon)SystemIcons.Application.Clone() : new Icon(resource);
            bool starts = false;
            try { starts = StartupRegistration.IsEnabled(executable); }
            catch (Exception ex) { log.Write("Az automatikus indítás nem olvasható: " + ex.Message); }
            form = new MainForm(settings, starts, icon);
            // A handle is required for power/display events even when starting in the tray.
            IntPtr handle = form.Handle;
            form.SaveRequested = SaveAndCheck;
            form.RefreshRequested = delegate { RunCheck(false); };
            form.OpenLogRequested = OpenLog;
            form.HiddenByUser = HiddenHint;
            var menu = new ContextMenuStrip();
            menu.Items.Add("Beállítások megnyitása", null, delegate { ShowWindow(); });
            menu.Items.Add("Visszaállítás most", null, delegate { RunCheck(true); });
            pause = new ToolStripMenuItem();
            pause.Click += delegate { ToggleGuard(); };
            menu.Items.Add(pause);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Napló megnyitása", null, delegate { OpenLog(); });
            menu.Items.Add("Kilépés", null, delegate { RequestExit(); });
            tray = new NotifyIcon { Icon = icon, Visible = true, ContextMenuStrip = menu };
            tray.DoubleClick += delegate { ShowWindow(); };
            UpdateTray();
            timer = new System.Windows.Forms.Timer { Interval = 1000 };
            timer.Tick += OnTick;
            nextCheck = DateTime.UtcNow.AddSeconds(1);
            SystemEvents.DisplaySettingsChanged += DisplayChanged;
            SystemEvents.PowerModeChanged += PowerChanged;
            SystemEvents.SessionSwitch += SessionChanged;
            timer.Start();
            log.Write("RefreshSentinel 1.0 elindult. Felügyelet: " + (settings.Enabled ? "aktív" : "szünetel"));
            if (warning != null)
            {
                log.Write(warning); form.ShowError(warning); startHidden = false;
            }
            if (!startHidden) ShowWindow();
        }

        private void OnTick(object sender, EventArgs args)
        {
            if (showSignal.WaitOne(0)) ShowWindow();
            if (!exiting && !busy && !suspended && !sessionLocked && DateTime.UtcNow >= nextCheck)
                RunCheck(settings.Enabled);
        }

        private async void RunCheck(bool enforce)
        {
            if (busy || exiting || suspended || sessionLocked) return;
            busy = true;
            nextCheck = DateTime.UtcNow.AddSeconds(settings.IntervalSeconds);
            form.SetBusy(true);
            try
            {
                AppSettings snapshot = settings.Copy();
                var report = await Task.Run(() => engine.Check(snapshot, enforce, DateTime.UtcNow));
                if (!exiting)
                {
                    form.ShowReport(report);
                    if (report.RestoredCount > 0 && snapshot.NotifyOnRestore)
                    {
                        string details = String.Join(Environment.NewLine, report.Statuses.Where(s => s.Restored).Select(s => {
                            var rule = snapshot.Monitors.First(r => String.Equals(r.Id, s.Id, StringComparison.OrdinalIgnoreCase));
                            return rule.Name + ": " + rule.Hertz + " Hz";
                        }));
                        tray.ShowBalloonTip(4000, "Képfrissítés visszaállítva", details, ToolTipIcon.Info);
                    }
                    if (report.Error != null) tray.Text = "RefreshSentinel · lekérdezési hiba; újrapróbálás később";
                    else UpdateTray();
                }
            }
            catch (Exception ex)
            {
                log.Write("Ellenőrzési hiba: " + ex);
                if (!exiting) form.ShowError("Az ellenőrzés sikertelen: " + ex.Message);
            }
            finally
            {
                busy = false;
                // Count the interval from completion so a slow driver cannot create a tight loop.
                nextCheck = DateTime.UtcNow.AddSeconds(settings.IntervalSeconds);
                if (!exiting) form.SetBusy(false);
                else ExitThread();
            }
        }

        private void SaveAndCheck(AppSettings proposed, bool startWithWindows, bool force)
        {
            if (busy || exiting) return;
            bool oldStartup = false, startupChanged = false;
            try
            {
                proposed.Validate();
                oldStartup = StartupRegistration.IsEnabled(executable);
                if (oldStartup != startWithWindows)
                {
                    StartupRegistration.SetEnabled(startWithWindows, executable);
                    startupChanged = true;
                }
                try { store.Save(proposed); }
                catch
                {
                    if (startupChanged) StartupRegistration.SetEnabled(oldStartup, executable);
                    throw;
                }
                settings = proposed.Copy();
                form.SettingsSaved(settings); UpdateTray();
                log.Write("Beállítások mentve. Időköz: " + settings.IntervalSeconds + " mp; felügyelet: " + settings.Enabled);
                RunCheck(force || settings.Enabled);
            }
            catch (Exception ex)
            {
                form.ShowError("A mentés sikertelen: " + ex.Message);
                log.Write("Mentési hiba: " + ex.Message);
            }
        }

        private void ToggleGuard()
        {
            if (busy || exiting) return;
            try
            {
                var next = settings.Copy(); next.Enabled = !next.Enabled;
                store.Save(next); settings = next;
                form.SetAutomatic(next.Enabled); UpdateTray();
                log.Write(next.Enabled ? "Felügyelet folytatva." : "Felügyelet szüneteltetve.");
                RunCheck(next.Enabled);
            }
            catch (Exception ex) { ShowWindow(); form.ShowError("Nem sikerült menteni: " + ex.Message); }
        }

        private void UpdateTray()
        {
            int count = settings.Monitors.Count(r => r.Enabled);
            tray.Text = settings.Enabled ? "RefreshSentinel · " + count + " monitor felügyelete" : "RefreshSentinel · felügyelet szünetel";
            pause.Text = settings.Enabled ? "Felügyelet szüneteltetése" : "Felügyelet folytatása";
            pause.Enabled = !busy;
        }

        private void ShowWindow()
        {
            if (exiting) return;
            form.Show(); form.WindowState = FormWindowState.Normal; form.Activate();
        }

        private void HiddenHint()
        {
            if (hiddenHintShown) return;
            hiddenHintShown = true;
            tray.ShowBalloonTip(3000, "RefreshSentinel a háttérben fut", "A rejtett ikonok között találod. Dupla kattintással megnyithatod; jobb kattintással kiléphetsz.", ToolTipIcon.Info);
        }

        private void OpenLog()
        {
            try
            {
                log.Write("Napló megnyitva.");
                System.Diagnostics.Process.Start("notepad.exe", "\"" + Path.Combine(store.DirectoryPath, "events.log") + "\"");
            }
            catch (Exception ex) { ShowWindow(); form.ShowError("A napló nem nyitható meg: " + ex.Message); }
        }

        private void OnUi(Action action)
        {
            if (disposed || exiting || form.IsDisposed) return;
            try { form.BeginInvoke(new MethodInvoker(delegate { if (!exiting) action(); })); }
            catch (InvalidOperationException) { }
        }

        private void DisplayChanged(object sender, EventArgs args)
        {
            OnUi(delegate { nextCheck = DateTime.UtcNow.AddSeconds(5); });
        }

        private void PowerChanged(object sender, PowerModeChangedEventArgs args)
        {
            OnUi(delegate {
                if (args.Mode == PowerModes.Suspend) suspended = true;
                else if (args.Mode == PowerModes.Resume) { suspended = false; nextCheck = DateTime.UtcNow.AddSeconds(10); }
            });
        }

        private void SessionChanged(object sender, SessionSwitchEventArgs args)
        {
            OnUi(delegate {
                if (args.Reason == SessionSwitchReason.SessionLock) sessionLocked = true;
                else if (args.Reason == SessionSwitchReason.SessionUnlock) { sessionLocked = false; nextCheck = DateTime.UtcNow.AddSeconds(5); }
            });
        }

        private void RequestExit()
        {
            exiting = true; timer.Stop();
            if (!busy) ExitThread();
        }

        protected override void ExitThreadCore()
        {
            if (disposed) return;
            disposed = true;
            SystemEvents.DisplaySettingsChanged -= DisplayChanged;
            SystemEvents.PowerModeChanged -= PowerChanged;
            SystemEvents.SessionSwitch -= SessionChanged;
            timer.Stop(); timer.Dispose();
            tray.Visible = false; tray.ContextMenuStrip.Dispose(); tray.Dispose();
            form.AllowClose = true; form.Close(); form.Dispose(); icon.Dispose();
            log.Write("RefreshSentinel leállt.");
            base.ExitThreadCore();
        }
    }
}
