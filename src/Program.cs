using System;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace RefreshSentinel
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            string user = System.Security.Principal.WindowsIdentity.GetCurrent().User.Value;
            // Keep the first prototype's mutex so both names cannot control the same screens at once.
            string name = @"Local\HzGuard.v1." + user;
            bool first;
            using (var mutex = new Mutex(true, name, out first))
            {
                if (!first)
                {
                    try { using (var signal = EventWaitHandle.OpenExisting(name + ".show")) signal.Set(); }
                    catch (WaitHandleCannotBeOpenedException) { }
                    return;
                }
                try
                {
                    using (var signal = new EventWaitHandle(false, EventResetMode.AutoReset, name + ".show"))
                    {
                        Native.SetProcessDPIAware();
                        Application.EnableVisualStyles();
                        Application.SetCompatibleTextRenderingDefault(false);
                        using (var context = new TrayApplication(args.Any(a => a.Equals("--tray", StringComparison.OrdinalIgnoreCase)), signal))
                            Application.Run(context);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("A RefreshSentinel nem indítható el.\r\n\r\n" + ex.Message, "RefreshSentinel", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally { mutex.ReleaseMutex(); }
            }
        }
    }
}
