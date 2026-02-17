using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using YukkuriMovieMaker.Resources.Localization;

namespace ProxyVideoSource
{
    public class ExportDetector
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private static bool? lastExportState = null;
        private static readonly Stopwatch cacheTimer = Stopwatch.StartNew();
        private static long lastCheckTicks = 0;
        private static string lastWindowTitle = "";

        private const long CacheIntervalMs = 2000;

        public static bool IsExporting()
        {
            var elapsed = cacheTimer.ElapsedMilliseconds;

            if ((elapsed - lastCheckTicks) < CacheIntervalMs && lastExportState.HasValue)
            {
                return lastExportState.Value;
            }

            lastCheckTicks = elapsed;
            bool isExporting = CheckExportWindow();

            if (!lastExportState.HasValue || lastExportState.Value != isExporting)
            {
                lastExportState = isExporting;
            }

            return isExporting;
        }

        private static bool CheckExportWindow()
        {
            try
            {
                var currentProcess = Process.GetCurrentProcess();
                var currentProcessId = (uint)currentProcess.Id;

                bool foundExportWindow = false;

                EnumWindows((hWnd, lParam) =>
                {
                    _ = GetWindowThreadProcessId(hWnd, out uint windowProcessId);

                    if (windowProcessId == currentProcessId)
                    {
                        const int nChars = 256;
                        StringBuilder buff = new(nChars);
                        int length = GetWindowText(hWnd, buff, nChars);

                        if (length > 0)
                        {
                            var title = buff.ToString();

                            if (title != lastWindowTitle && !string.IsNullOrWhiteSpace(title))
                            {
                                lastWindowTitle = title;
                            }

                            var isOutputWindow = title == Texts.OutputProgressWindowTitle ||
                                                title == Texts.VideoExportWindowTitle;

                            if (isOutputWindow)
                            {
                                foundExportWindow = true;
                                return false;
                            }
                        }
                    }

                    return true;
                }, IntPtr.Zero);

                return foundExportWindow;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ExportDetector] Exception: {ex.Message}");
            }

            return false;
        }

        public static void Reset()
        {
            lastExportState = null;
            lastWindowTitle = "";
        }
    }
}