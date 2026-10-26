using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;

namespace RefreshSentinel
{
    internal static class Native
    {
        internal const uint FrequencyField = 0x00400000;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct DevMode
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
            public ushort SpecVersion, DriverVersion, Size, DriverExtra;
            public uint Fields;
            public int PositionX, PositionY;
            public uint Orientation, FixedOutput;
            public short Color, Duplex, YResolution, TTOption, Collate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string FormName;
            public ushort LogPixels;
            public uint BitsPerPel, Width, Height, DisplayFlags, Frequency;
            public uint ICMMethod, ICMIntent, MediaType, DitherType, Reserved1, Reserved2, PanningWidth, PanningHeight;
            public static DevMode Empty() { return new DevMode { Size = (ushort)Marshal.SizeOf(typeof(DevMode)) }; }
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Luid { public uint Low; public int High; }
        [StructLayout(LayoutKind.Sequential)]
        internal struct Rational { public uint Numerator, Denominator; }
        [StructLayout(LayoutKind.Sequential)]
        internal struct SourceInfo { public Luid Adapter; public uint Id, ModeIndex, Status; }
        [StructLayout(LayoutKind.Sequential)]
        internal struct TargetInfo
        {
            public Luid Adapter;
            public uint Id, ModeIndex, OutputTechnology, Rotation, Scaling;
            public Rational Refresh;
            public uint ScanLineOrdering;
            public int Available;
            public uint Status;
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct PathInfo { public SourceInfo Source; public TargetInfo Target; public uint Flags; }
        [StructLayout(LayoutKind.Explicit, Size = 64)]
        internal struct ModeInfo { [FieldOffset(0)] public uint Type; }
        [StructLayout(LayoutKind.Sequential)]
        internal struct DeviceHeader { public uint Type, Size; public Luid Adapter; public uint Id; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct SourceName
        {
            public DeviceHeader Header;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Name;
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct TargetName
        {
            public DeviceHeader Header;
            public uint Flags, OutputTechnology;
            public ushort Manufacturer, Product;
            public uint Connector;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string FriendlyName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DevicePath;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EnumDisplaySettingsEx(string device, int mode, ref DevMode devMode, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern int ChangeDisplaySettingsEx(string device, ref DevMode mode, IntPtr hwnd, uint flags, IntPtr param);
        [DllImport("user32.dll")]
        private static extern int GetDisplayConfigBufferSizes(uint flags, out uint paths, out uint modes);
        [DllImport("user32.dll")]
        private static extern int QueryDisplayConfig(uint flags, ref uint pathCount, [Out] PathInfo[] paths, ref uint modeCount, [Out] ModeInfo[] modes, IntPtr topology);
        [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")]
        internal static extern int GetSourceName(ref SourceName name);
        [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")]
        internal static extern int GetTargetName(ref TargetName name);
        [DllImport("user32.dll")]
        internal static extern bool SetProcessDPIAware();

        internal static PathInfo[] ActivePaths()
        {
            for (int attempt = 0; attempt < 5; attempt++)
            {
                uint pathCount, modeCount;
                int code = GetDisplayConfigBufferSizes(2, out pathCount, out modeCount);
                if (code != 0) throw new Win32Exception(code);
                var paths = new PathInfo[pathCount];
                var modes = new ModeInfo[modeCount];
                code = QueryDisplayConfig(2, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
                if (code == 122) continue; // A hot-plug can invalidate the buffer sizes.
                if (code != 0) throw new Win32Exception(code);
                return paths.Take((int)pathCount).ToArray();
            }
            throw new InvalidOperationException("A kijelzők elrendezése éppen változik; a következő ellenőrzés újrapróbálja.");
        }

        internal static string ErrorText(int code)
        {
            switch (code)
            {
                case 1: return "A módhoz újraindítás szükséges; nem alkalmazható azonnal.";
                case -1: return "A grafikus illesztőprogram elutasította a beállítást.";
                case -2: return "A képfrissítés ebben a megjelenítési módban nem támogatott.";
                case -3: return "A Windows nem tudta menteni a megjelenítési módot.";
                case -4: return "A Windows érvénytelen beállításjelzőt jelzett.";
                case -5: return "A kijelző beállítása időközben megváltozott vagy nem érvényes.";
                case -6: return "A kijelzők jelenlegi összekapcsolása nem támogatja a módot.";
                default: return "Windows kijelzőhiba: " + code;
            }
        }
    }

    public sealed class NativeDisplayService : IDisplayService
    {
        public List<DisplaySnapshot> GetDisplays()
        {
            var paths = Native.ActivePaths().Where(p => p.Target.Available != 0).ToArray();
            var result = new List<DisplaySnapshot>();
            var modeCache = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in paths)
            {
                var source = new Native.SourceName { Header = new Native.DeviceHeader {
                    Type = 1, Size = (uint)Marshal.SizeOf(typeof(Native.SourceName)), Adapter = path.Source.Adapter, Id = path.Source.Id } };
                var target = new Native.TargetName { Header = new Native.DeviceHeader {
                    Type = 2, Size = (uint)Marshal.SizeOf(typeof(Native.TargetName)), Adapter = path.Target.Adapter, Id = path.Target.Id } };
                int code = Native.GetSourceName(ref source);
                if (code != 0) throw new Win32Exception(code, "A kijelző eszközneve nem kérdezhető le.");
                code = Native.GetTargetName(ref target);
                if (code != 0) throw new Win32Exception(code, "A monitor egyedi azonosítója nem kérdezhető le.");
                // Never guess identity from DISPLAY1/2 or a model name: both can be reassigned.
                if (String.IsNullOrWhiteSpace(target.DevicePath))
                    throw new InvalidOperationException("Egy aktív monitorhoz nem érhető el biztonságos eszközazonosító.");
                var current = Native.DevMode.Empty();
                if (!Native.EnumDisplaySettingsEx(source.Name, -1, ref current, 0))
                    throw new InvalidOperationException(source.Name + ": az aktuális mód nem olvasható.");
                List<int> supported;
                if (!modeCache.TryGetValue(source.Name, out supported))
                {
                    supported = EnumerateCompatibleModes(source.Name, current).Select(m => (int)m.Frequency).Distinct().OrderBy(h => h).ToList();
                    modeCache.Add(source.Name, supported);
                }
                int number;
                if (!Int32.TryParse(source.Name.Replace(@"\\.\DISPLAY", ""), out number)) number = result.Count + 1;
                result.Add(new DisplaySnapshot {
                    Id = target.DevicePath, Name = String.IsNullOrWhiteSpace(target.FriendlyName) ? "Monitor " + number : target.FriendlyName,
                    DeviceName = source.Name, Number = number, Width = (int)current.Width, Height = (int)current.Height,
                    CurrentHertz = (int)current.Frequency,
                    SignalHertz = path.Target.Refresh.Denominator == 0 ? 0 : (double)path.Target.Refresh.Numerator / path.Target.Refresh.Denominator,
                    Primary = current.PositionX == 0 && current.PositionY == 0,
                    Cloned = paths.Count(p => p.Source.Adapter.Low == path.Source.Adapter.Low && p.Source.Adapter.High == path.Source.Adapter.High && p.Source.Id == path.Source.Id) > 1,
                    SupportedHertz = new List<int>(supported)
                });
            }
            return result.OrderBy(d => d.Number).ThenBy(d => d.Name).ToList();
        }

        internal static bool IsCompatible(Native.DevMode candidate, Native.DevMode current)
        {
            const uint required = 0x00080000 | 0x00100000 | 0x00400000;
            return (candidate.Fields & required) == required && candidate.Frequency > 1 &&
                candidate.Width == current.Width && candidate.Height == current.Height &&
                candidate.BitsPerPel == current.BitsPerPel && candidate.Orientation == current.Orientation &&
                candidate.DisplayFlags == current.DisplayFlags;
        }

        private static List<Native.DevMode> EnumerateCompatibleModes(string device, Native.DevMode current)
        {
            var modes = new List<Native.DevMode>();
            for (int index = 0; index < 50000; index++)
            {
                var mode = Native.DevMode.Empty();
                // No EDS_RAWMODE: only modes supported by this monitor, in its current orientation.
                if (!Native.EnumDisplaySettingsEx(device, index, ref mode, 0)) break;
                if (IsCompatible(mode, current)) modes.Add(mode);
            }
            return modes;
        }

        internal static Native.DevMode PrepareMode(Native.DevMode candidate, Native.DevMode current)
        {
            candidate.PositionX = current.PositionX;
            candidate.PositionY = current.PositionY;
            candidate.Orientation = current.Orientation;
            candidate.FixedOutput = current.FixedOutput;
            candidate.Fields = 0x00040000 | 0x00080000 | 0x00100000 | 0x00200000 | Native.FrequencyField;
            candidate.Fields |= current.Fields & (0x20u | 0x80u | 0x20000000u); // Position, rotation, fixed output.
            candidate.DriverExtra = 0;
            return candidate;
        }

        public ApplyResult Restore(string monitorId, int hertz)
        {
            var monitors = GetDisplays();
            var matches = monitors.Where(d => String.Equals(d.Id, monitorId, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count != 1) return ApplyResult.Fail("A monitor már nincs csatlakoztatva vagy nem azonosítható egyértelműen.");
            var display = matches[0];
            if (display.Cloned) return ApplyResult.Fail("Tükrözött kijelző nem állítható külön.");
            if (display.CurrentHertz == hertz) return ApplyResult.Ok("Már a kívánt képfrissítés aktív.");
            var current = Native.DevMode.Empty();
            if (!Native.EnumDisplaySettingsEx(display.DeviceName, -1, ref current, 0))
                return ApplyResult.Fail("A monitor aktuális módja nem olvasható.");
            var candidates = EnumerateCompatibleModes(display.DeviceName, current).Where(m => m.Frequency == hertz).ToList();
            if (candidates.Count == 0) return ApplyResult.Fail("A kívánt Hz a jelenlegi felbontásnál nem támogatott.");
            var requested = PrepareMode(candidates[0], current);
            int code = Native.ChangeDisplaySettingsEx(display.DeviceName, ref requested, IntPtr.Zero, 2, IntPtr.Zero); // CDS_TEST
            if (code != 0) return ApplyResult.Fail(Native.ErrorText(code));

            // Recheck identity and layout after validation; a dock/monitor may have disappeared.
            var fresh = GetDisplays();
            if (fresh.Count != monitors.Count || fresh.Any(d => !monitors.Any(old =>
                String.Equals(d.Id, old.Id, StringComparison.OrdinalIgnoreCase) && d.DeviceName == old.DeviceName &&
                d.Width == old.Width && d.Height == old.Height && d.Cloned == old.Cloned)))
                return ApplyResult.Fail("A monitorok elrendezése megváltozott; a következő ellenőrzés újrapróbálja.");
            var latest = Native.DevMode.Empty();
            if (!Native.EnumDisplaySettingsEx(display.DeviceName, -1, ref latest, 0) ||
                latest.Width != current.Width || latest.Height != current.Height || latest.PositionX != current.PositionX ||
                latest.PositionY != current.PositionY || latest.Orientation != current.Orientation ||
                latest.BitsPerPel != current.BitsPerPel || latest.DisplayFlags != current.DisplayFlags || latest.Frequency != current.Frequency)
                return ApplyResult.Fail("A kijelző módja időközben megváltozott; most nincs módosítás.");
            // No registry writes, unsafe modes, resolution changes, or administrator rights.
            code = Native.ChangeDisplaySettingsEx(display.DeviceName, ref requested, IntPtr.Zero, 0, IntPtr.Zero);
            if (code != 0) return ApplyResult.Fail(Native.ErrorText(code));
            var actual = Native.DevMode.Empty();
            if (!Native.EnumDisplaySettingsEx(display.DeviceName, -1, ref actual, 0) || actual.Frequency != hertz ||
                actual.Width != current.Width || actual.Height != current.Height || actual.Orientation != current.Orientation ||
                actual.PositionX != current.PositionX || actual.PositionY != current.PositionY)
                return ApplyResult.Fail("A Windows elfogadta a kérést, de a kívánt mód nem igazolható vissza. Újrapróbálás később.");
            return ApplyResult.Ok(hertz + " Hz visszaállítva.");
        }

        // Used by diagnostics: CDS_TEST validates a mode without changing the display.
        internal static int TestCurrentMode(string device)
        {
            var current = Native.DevMode.Empty();
            if (!Native.EnumDisplaySettingsEx(device, -1, ref current, 0)) throw new InvalidOperationException("Nincs aktuális mód.");
            var requested = PrepareMode(current, current);
            return Native.ChangeDisplaySettingsEx(device, ref requested, IntPtr.Zero, 2, IntPtr.Zero);
        }
    }
}
