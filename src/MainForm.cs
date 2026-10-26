using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace RefreshSentinel
{
    internal static class Theme
    {
        public static readonly Color Background = Color.FromArgb(18, 21, 29);
        public static readonly Color Surface = Color.FromArgb(27, 32, 43);
        public static readonly Color Field = Color.FromArgb(38, 44, 58);
        public static readonly Color Text = Color.FromArgb(239, 242, 249);
        public static readonly Color Muted = Color.FromArgb(158, 170, 191);
        public static readonly Color Accent = Color.FromArgb(152, 135, 255);
        public static readonly Color Green = Color.FromArgb(107, 224, 178);
        public static readonly Color Amber = Color.FromArgb(255, 199, 119);

        public static Label Label(string text, float size, Color color, FontStyle style)
        {
            return new Label { Text = text, AutoSize = true, ForeColor = color,
                Font = new Font("Segoe UI", size, style), Margin = new Padding(0, 0, 0, 6) };
        }

        public static Button Button(string text, bool primary)
        {
            var button = new Button { Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(100, 36), Padding = new Padding(14, 5, 14, 5), FlatStyle = FlatStyle.Flat,
                BackColor = primary ? Accent : Field, ForeColor = primary ? Background : Text,
                Cursor = Cursors.Hand, Margin = new Padding(0, 0, 10, 0), UseVisualStyleBackColor = false };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }
    }

    internal sealed class HertzChoice
    {
        public int Hertz;
        public bool Available;
        public override string ToString() { return Hertz + " Hz" + (Available ? "" : " · nem elérhető"); }
    }

    internal sealed class MonitorCard : Panel
    {
        public readonly string MonitorId;
        public readonly string MonitorName;
        private readonly CheckBox enabled;
        private readonly ComboBox target;
        private readonly Label current;
        private readonly Label status;
        private readonly Label description;

        public MonitorCard(DisplaySnapshot display, MonitorRule rule, Action changed)
        {
            MonitorId = display.Id;
            MonitorName = display.Name;
            BackColor = Theme.Surface;
            Height = 164;
            Margin = new Padding(0, 0, 0, 12);
            Padding = new Padding(18, 14, 18, 10);
            var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3,
                BackColor = BackColor, Margin = Padding.Empty };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 31));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 65));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var name = Theme.Label(display.Number.ToString("00") + "  " + display.Name, 12, Theme.Text, FontStyle.Bold);
            name.AutoSize = false; name.Dock = DockStyle.Fill; name.AutoEllipsis = true;
            enabled = new CheckBox { Text = "Monitor felügyelete", AutoSize = true, ForeColor = Theme.Text,
                Anchor = AnchorStyles.Top | AnchorStyles.Right, Checked = rule != null && rule.Enabled, Margin = Padding.Empty };
            grid.Controls.Add(name, 0, 0); grid.Controls.Add(enabled, 1, 0);

            var left = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
                WrapContents = false, Margin = Padding.Empty };
            current = Theme.Label("", 23, Theme.Text, FontStyle.Bold);
            current.Margin = Padding.Empty;
            description = Theme.Label("", 9, Theme.Muted, FontStyle.Regular);
            left.Controls.Add(current); left.Controls.Add(description);
            grid.Controls.Add(left, 0, 1);

            var right = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
                WrapContents = false, Margin = new Padding(0, 1, 0, 0) };
            right.Controls.Add(Theme.Label("KÍVÁNT KÉPFRISSÍTÉS", 8, Theme.Muted, FontStyle.Bold));
            target = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat,
                BackColor = Theme.Field, ForeColor = Theme.Text, Font = new Font("Segoe UI", 11), Width = 220,
                IntegralHeight = true, DropDownWidth = 250, Margin = Padding.Empty };
            int desired = rule != null ? rule.Hertz : display.CurrentHertz;
            var choices = display.SupportedHertz.ToList();
            if (desired > 1 && !choices.Contains(desired)) choices.Add(desired);
            foreach (int hertz in choices.OrderBy(h => h))
                target.Items.Add(new HertzChoice { Hertz = hertz, Available = display.Connected && display.SupportedHertz.Contains(hertz) });
            target.SelectedItem = target.Items.Cast<HertzChoice>().FirstOrDefault(h => h.Hertz == desired);
            if (target.SelectedIndex < 0 && target.Items.Count > 0) target.SelectedIndex = 0;
            target.Enabled = display.Connected && !display.Cloned && target.Items.Count > 0;
            right.Controls.Add(target);
            grid.Controls.Add(right, 1, 1);
            status = Theme.Label("", 9, Theme.Muted, FontStyle.Regular);
            status.AutoSize = false; status.Dock = DockStyle.Fill; status.AutoEllipsis = true;
            grid.Controls.Add(status, 0, 2); grid.SetColumnSpan(status, 2);
            Controls.Add(grid);
            UpdateDisplay(display);
            enabled.CheckedChanged += delegate { changed(); };
            target.SelectedIndexChanged += delegate { changed(); };
        }

        public MonitorRule ReadRule(bool requireChoice)
        {
            var choice = target.SelectedItem as HertzChoice;
            if (choice == null && enabled.Checked && requireChoice) throw new InvalidOperationException(MonitorName + ": válassz támogatott képfrissítést.");
            return choice == null ? null : new MonitorRule { Id = MonitorId, Name = MonitorName, Hertz = choice.Hertz, Enabled = enabled.Checked };
        }

        public void UpdateDisplay(DisplaySnapshot display)
        {
            current.Text = display.Connected ? (display.CurrentHertz > 1 ? display.CurrentHertz + " Hz" : "Alapérték") : "Leválasztva";
            description.Text = display.Connected ? display.Resolution + (display.Primary ? " · elsődleges" : "") : "A mentett célérték megmarad";
            if (display.Cloned) SetStatus("Tükrözött kijelző · külön beállításhoz válts Kiterjesztés módra.", true);
            else SetStatus(display.Connected ? "Válaszd ki a célértéket, majd mentsd a beállításokat." : "Újracsatlakoztatásra vár.", false);
        }

        public void SetStatus(string text, bool warning)
        {
            status.Text = text;
            status.ForeColor = warning ? Theme.Amber : Theme.Green;
        }
    }

    internal sealed class MainForm : Form
    {
        private readonly FlowLayoutPanel monitors;
        private readonly CheckBox automatic, startup, notify;
        private readonly NumericUpDown interval;
        private readonly Label activeStatus, lastCheck, message, count;
        private readonly Button save, restore, refresh;
        private readonly List<MonitorCard> cards = new List<MonitorCard>();
        private AppSettings saved;
        private string topology;
        private readonly ToolTip errorTip = new ToolTip();
        private bool loading;
        private bool dirty;
        internal bool AllowClose;
        internal Action<AppSettings, bool, bool> SaveRequested;
        internal Action RefreshRequested;
        internal Action OpenLogRequested;
        internal Action HiddenByUser;

        public MainForm(AppSettings settings, bool startsWithWindows, Icon icon)
        {
            saved = settings.Copy();
            Text = "RefreshSentinel · Monitorfelügyelet";
            Icon = icon;
            BackColor = Theme.Background; ForeColor = Theme.Text;
            Font = new Font("Segoe UI", 10);
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96, 96);
            ClientSize = new Size(880, 720); MinimumSize = new Size(800, 650);
            StartPosition = FormStartPosition.CenterScreen;
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Padding = new Padding(26, 22, 26, 16) };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 87));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 149));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 29));
            Controls.Add(root);

            var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 67));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
            var brand = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = Padding.Empty };
            brand.Controls.Add(Theme.Label("RefreshSentinel", 26, Theme.Text, FontStyle.Bold));
            brand.Controls.Add(Theme.Label("A képfrissítésed maradjon a helyén.", 10, Theme.Muted, FontStyle.Regular));
            activeStatus = Theme.Label("SZÜNETEL", 9, Theme.Amber, FontStyle.Bold);
            activeStatus.Anchor = AnchorStyles.Right | AnchorStyles.Top; activeStatus.Margin = new Padding(0, 15, 0, 0);
            header.Controls.Add(brand, 0, 0); header.Controls.Add(activeStatus, 1, 0); root.Controls.Add(header, 0, 0);

            var monitorHeading = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
            monitorHeading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            monitorHeading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            count = Theme.Label("MONITOROK BETÖLTÉSE…", 9, Theme.Muted, FontStyle.Bold);
            lastCheck = Theme.Label("", 9, Theme.Muted, FontStyle.Regular); lastCheck.Anchor = AnchorStyles.Right;
            monitorHeading.Controls.Add(count, 0, 0); monitorHeading.Controls.Add(lastCheck, 1, 0); root.Controls.Add(monitorHeading, 0, 1);
            monitors = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = Padding.Empty };
            monitors.SizeChanged += delegate { FitCards(); };
            root.Controls.Add(monitors, 0, 2);

            var options = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0, 12, 0, 0) };
            var schedule = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 9) };
            automatic = new CheckBox { Text = "Automatikus ellenőrzés", AutoSize = true, ForeColor = Theme.Text, Margin = new Padding(0, 5, 25, 0), Checked = settings.Enabled };
            var every = Theme.Label("Időköz:", 10, Theme.Muted, FontStyle.Regular); every.Margin = new Padding(0, 5, 8, 0);
            interval = new NumericUpDown { Minimum = 5, Maximum = 3600, Value = settings.IntervalSeconds, Width = 78,
                BackColor = Theme.Field, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(0, 2, 7, 0) };
            var seconds = Theme.Label("másodperc", 10, Theme.Muted, FontStyle.Regular); seconds.Margin = new Padding(0, 5, 0, 0);
            schedule.Controls.Add(automatic); schedule.Controls.Add(every); schedule.Controls.Add(interval); schedule.Controls.Add(seconds);
            startup = new CheckBox { Text = "Induljon a Windowszal, a tálcára rejtve", AutoSize = true, Checked = startsWithWindows, Margin = new Padding(0, 0, 0, 7) };
            notify = new CheckBox { Text = "Értesítés sikeres visszaállításkor", AutoSize = true, Checked = settings.NotifyOnRestore, Margin = new Padding(0, 0, 0, 7) };
            options.Controls.Add(schedule); options.Controls.Add(startup); options.Controls.Add(notify);
            options.Controls.Add(Theme.Label("A Windows által jelzett névleges Hz-értékeket felügyeli. A felbontást nem módosítja.", 9, Theme.Muted, FontStyle.Regular));
            root.Controls.Add(options, 0, 3);

            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = new Padding(0, 5, 0, 0) };
            save = Theme.Button("Mentés", true); restore = Theme.Button("Visszaállítás most", false); refresh = Theme.Button("Lista frissítése", false);
            var log = Theme.Button("Napló", false);
            actions.Controls.Add(save); actions.Controls.Add(restore); actions.Controls.Add(refresh); actions.Controls.Add(log); root.Controls.Add(actions, 0, 4);
            message = Theme.Label("Az ablak bezárása után a tálcán fut tovább.", 9, Theme.Muted, FontStyle.Regular);
            message.AutoSize = false; message.Dock = DockStyle.Fill; message.AutoEllipsis = true; root.Controls.Add(message, 0, 5);

            automatic.CheckedChanged += Changed; startup.CheckedChanged += Changed; notify.CheckedChanged += Changed; interval.ValueChanged += Changed;
            save.Click += delegate { RequestSave(false); }; restore.Click += delegate { RequestSave(true); };
            refresh.Click += delegate { if (RefreshRequested != null) RefreshRequested(); };
            log.Click += delegate { if (OpenLogRequested != null) OpenLogRequested(); };
            FormClosing += delegate(object sender, FormClosingEventArgs e) {
                if (!AllowClose && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); if (HiddenByUser != null) HiddenByUser(); }
            };
            Resize += delegate { if (WindowState == FormWindowState.Minimized) { Hide(); if (HiddenByUser != null) HiddenByUser(); } };
            UpdateEnabled(settings.Enabled);
        }

        private void Changed(object sender, EventArgs args) { MarkDirty(); }
        private void MarkDirty()
        {
            if (loading) return;
            dirty = true;
            message.Text = "Módosítások mentésre várnak. A Mentés gombbal lépnek életbe.";
            message.ForeColor = Theme.Amber;
        }

        private void RequestSave(bool force)
        {
            try
            {
                var next = saved.Copy();
                next.Enabled = automatic.Checked; next.IntervalSeconds = (int)interval.Value; next.NotifyOnRestore = notify.Checked;
                var drafts = cards.Select(c => c.ReadRule(true)).Where(r => r != null).ToList();
                foreach (var rule in drafts)
                {
                    next.Monitors.RemoveAll(r => String.Equals(r.Id, rule.Id, StringComparison.OrdinalIgnoreCase));
                    next.Monitors.Add(rule);
                }
                next.Validate();
                if (SaveRequested != null) SaveRequested(next, startup.Checked, force);
            }
            catch (Exception ex) { ShowError(ex.Message); }
        }

        internal void SettingsSaved(AppSettings settings)
        {
            saved = settings.Copy(); dirty = false;
            message.Text = "Mentve. Az ablak bezárása után a tálcán fut tovább."; message.ForeColor = Theme.Muted;
            UpdateEnabled(settings.Enabled);
        }

        internal void SetAutomatic(bool enabled)
        {
            saved.Enabled = enabled;
            loading = true; automatic.Checked = enabled; loading = false;
            UpdateEnabled(enabled);
        }

        private void UpdateEnabled(bool enabled)
        {
            activeStatus.Text = enabled ? "●  FELÜGYELET AKTÍV" : "●  FELÜGYELET SZÜNETEL";
            activeStatus.ForeColor = enabled ? Theme.Green : Theme.Amber;
        }

        internal void SetBusy(bool busy)
        {
            save.Enabled = restore.Enabled = refresh.Enabled = !busy;
            if (busy) lastCheck.Text = "Ellenőrzés…";
        }

        internal void ShowError(string error)
        {
            message.Text = error; message.ForeColor = Theme.Amber;
            errorTip.SetToolTip(message, error);
        }

        internal void ShowReport(CheckReport report)
        {
            lastCheck.Text = "Ellenőrizve: " + report.TimeUtc.ToLocalTime().ToString("HH:mm:ss");
            if (report.Error != null) { ShowError(report.Error); return; }
            var views = report.Displays.ToList();
            var drafts = new Dictionary<string, MonitorRule>(StringComparer.OrdinalIgnoreCase);
            foreach (var rule in saved.Monitors) drafts[rule.Id] = rule;
            foreach (var card in cards)
            {
                var draft = card.ReadRule(false);
                if (draft != null) drafts[draft.Id] = draft;
            }
            foreach (var rule in drafts.Values.Where(r => !views.Any(d => String.Equals(d.Id, r.Id, StringComparison.OrdinalIgnoreCase))))
                views.Add(new DisplaySnapshot { Id = rule.Id, Name = rule.Name, CurrentHertz = rule.Hertz, Connected = false });
            string signature = String.Join("|", views.Select(d => d.Id + ":" + d.Resolution + ":" + d.Connected + ":" + d.Cloned + ":" + String.Join(",", d.SupportedHertz)));
            if (signature != topology)
            {
                loading = true;
                monitors.SuspendLayout();
                foreach (var card in cards) card.Dispose();
                cards.Clear(); monitors.Controls.Clear();
                foreach (var display in views)
                {
                    MonitorRule rule; drafts.TryGetValue(display.Id, out rule);
                    var card = new MonitorCard(display, rule, MarkDirty); cards.Add(card); monitors.Controls.Add(card);
                }
                if (views.Count == 0)
                {
                    var empty = Theme.Label("Nincs aktív monitor. Csatlakoztass egy kijelzőt, majd frissítsd a listát.", 11, Theme.Muted, FontStyle.Regular);
                    empty.MaximumSize = new Size(650, 0); monitors.Controls.Add(empty);
                }
                topology = signature; FitCards(); monitors.ResumeLayout(); loading = false;
            }
            count.Text = report.Displays.Count + " CSATLAKOZTATOTT MONITOR";
            foreach (var card in cards)
            {
                var display = views.FirstOrDefault(d => String.Equals(d.Id, card.MonitorId, StringComparison.OrdinalIgnoreCase));
                if (display != null) card.UpdateDisplay(display);
                var status = report.Statuses.FirstOrDefault(s => String.Equals(s.Id, card.MonitorId, StringComparison.OrdinalIgnoreCase));
                if (status != null) card.SetStatus(status.Message, status.Warning);
            }
            if (!dirty && report.RestoredCount > 0)
            {
                message.Text = report.RestoredCount + " monitor képfrissítése visszaállítva."; message.ForeColor = Theme.Green;
            }
        }

        private void FitCards()
        {
            foreach (var card in cards) card.Width = Math.Max(500, monitors.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 3);
        }
    }
}
