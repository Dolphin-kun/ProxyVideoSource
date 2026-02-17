using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using YukkuriMovieMaker.Commons;

namespace ProxyVideoSource
{
    public static class ProxyManager
    {
        private static readonly ConcurrentDictionary<string, string> originalToProxy = new();
        private static readonly ConcurrentDictionary<string, string> proxyToOriginal = new();
        private static readonly ConcurrentDictionary<string, Task<string>> proxyGenerationTasks = new();

        private static readonly ConcurrentDictionary<(string path, float scale), string> proxyPathCache = new();
        private static readonly ConcurrentDictionary<string, long> fileSizeCache = new();
        private static readonly ConcurrentDictionary<string, bool> proxyExistsCache = new();

        public static event Action<string, string>? ProxyCompleted;

        private static readonly string proxyCacheFolder = Path.Combine(AppDirectories.TemporaryDirectory, "ProxyVideoCache");
        private static readonly string ffmpegPath = Path.Combine(AppDirectories.UserDirectory, "resources", "ffmpeg", "ffmpeg.exe");

        static ProxyManager()
        {
            Directory.CreateDirectory(proxyCacheFolder);
        }

        public static (string resolvedPath, bool isProxy) ResolvePathForEditing(string originalPath, float scale)
        {
            if (originalToProxy.TryGetValue(originalPath, out var cachedProxy))
            {
                if (CheckProxyFileExists(cachedProxy))
                    return (cachedProxy, true);
            }

            var proxyPath = GetCachedProxyPath(originalPath, scale);

            if (CheckProxyFileExists(proxyPath))
            {
                originalToProxy[originalPath] = proxyPath;
                proxyToOriginal[proxyPath] = originalPath;
                return (proxyPath, true);
            }

            StartProxyGeneration(originalPath, proxyPath, scale);
            return (originalPath, false);
        }

        public static bool IsFileLargeEnough(string filePath, double minSizeMB)
        {
            var size = fileSizeCache.GetOrAdd(filePath, path =>
            {
                try
                {
                    return new FileInfo(path).Length;
                }
                catch
                {
                    return 0;
                }
            });

            return size / 1024.0 / 1024.0 >= minSizeMB;
        }

        public static string GetOrCreateProxyPath(string originalPath, float scale = 0.5f)
        {
            if (originalToProxy.TryGetValue(originalPath, out var cachedProxy))
            {
                if (CheckProxyFileExists(cachedProxy))
                    return cachedProxy;
            }

            var proxyPath = GetCachedProxyPath(originalPath, scale);

            if (CheckProxyFileExists(proxyPath))
            {
                originalToProxy[originalPath] = proxyPath;
                proxyToOriginal[proxyPath] = originalPath;
                return proxyPath;
            }

            StartProxyGeneration(originalPath, proxyPath, scale);
            return originalPath;
        }

        private static void StartProxyGeneration(string originalPath, string proxyPath, float scale)
        {
            proxyGenerationTasks.GetOrAdd(originalPath, _ =>
            {
                return Task.Run(async () =>
                {
                    try
                    {
                        await CreateProxyVideoAsync(originalPath, proxyPath, scale);
                        originalToProxy[originalPath] = proxyPath;
                        proxyToOriginal[proxyPath] = originalPath;

                        proxyExistsCache[proxyPath] = true;

                        ProxyCompleted?.Invoke(originalPath, proxyPath);

                        return proxyPath;
                    }
                    catch (Exception)
                    {
                        return originalPath;
                    }
                    finally
                    {
                        proxyGenerationTasks.TryRemove(originalPath, out var _);
                    }
                });
            });
        }

        public static bool ProxyExists(string originalPath, float scale)
        {
            var proxyPath = GetCachedProxyPath(originalPath, scale);
            return CheckProxyFileExists(proxyPath);
        }

        public static bool IsProxyPath(string path)
        {
            return proxyToOriginal.ContainsKey(path);
        }

        public static string GetOriginalPath(string proxyPath)
        {
            if (proxyToOriginal.TryGetValue(proxyPath, out var original))
                return original;
            return proxyPath;
        }

        public static string GetCacheDirectory()
        {
            return proxyCacheFolder;
        }

        private static string GetCachedProxyPath(string originalPath, float scale)
        {
            return proxyPathCache.GetOrAdd((originalPath, scale), key => GenerateProxyPath(key.path, key.scale));
        }

        private static bool CheckProxyFileExists(string proxyPath)
        {
            if (proxyExistsCache.TryGetValue(proxyPath, out var exists) && exists)
                return true;

            exists = File.Exists(proxyPath);
            if (exists)
                proxyExistsCache[proxyPath] = true;

            return exists;
        }

        private static string GenerateProxyPath(string originalPath, float scale)
        {
            var originalFileName = Path.GetFileNameWithoutExtension(originalPath);
            var ext = Path.GetExtension(originalPath);
            var scaleStr = ((int)(scale * 100)).ToString();

            var proxyFileName = $"{originalFileName}_proxy{scaleStr}{ext}";

            if (proxyFileName.Length > 200)
            {
                var hash = ComputeFileHash(originalPath);
                proxyFileName = $"{hash}_proxy{scaleStr}{ext}";
            }

            return Path.Combine(proxyCacheFolder, proxyFileName);
        }

        private static string ComputeFileHash(string filePath)
        {
            var bytes = Encoding.UTF8.GetBytes(filePath);
            var hash = MD5.HashData(bytes);
            return Convert.ToHexString(hash)[..16];
        }

        private static async Task CreateProxyVideoAsync(string inputPath, string outputPath, float scale)
        {
            if (!File.Exists(ffmpegPath))
            {
                MessageBox.Show(
                    $"ffmpegが見つかりませんでした。\n\n" +
                    $"一度動画を出力してffmpegをインストールしてください。\n",
                    "エラー",
                    MessageBoxButton.OK);
            }

            var tempOutput = outputPath + ".temp.mp4";

            try
            {
                var scaleFilter = $"scale=iw*{scale}:ih*{scale}";
                var settings = ProxyVideoSourceSettings.Default;

                var args = $"-i \"{inputPath}\" " +
                           $"-vf \"{scaleFilter}\" " +
                           $"-c:v libopenh264 " +
                           $"-g 30 " +
                           $"-c:a copy " +
                           $"-threads {settings.CpuCoreCount} " +
                           $"-y \"{tempOutput}\"";

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = args,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    }
                };

                var errorBuilder = new StringBuilder();
                var outputBuilder = new StringBuilder();

                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        outputBuilder.AppendLine(e.Data);
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        errorBuilder.AppendLine(e.Data);
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await Task.Run(() => process.WaitForExit());

                var exitCode = process.ExitCode;

                if (exitCode == 0 && File.Exists(tempOutput))
                {
                    File.Move(tempOutput, outputPath);
                }
                else
                {
                    var errorOutput = errorBuilder.ToString(); 
                    var errorMessage = $"ffmpeg failed with exit code {exitCode}"; 
                    if (!string.IsNullOrEmpty(errorOutput)) 
                    { 
                        var lines = errorOutput.Split('\n');
                        var lastLines = string.Join("\n", lines.TakeLast(15));
                        errorMessage += $"\nError output:\n{lastLines}"; 
                    }
                    throw new Exception(errorMessage);
                }
            }
            catch (Exception)
            {
                if (File.Exists(tempOutput))
                {
                    try { File.Delete(tempOutput); } catch { }
                }
                throw;
            }
        }


        public static void ClearCache()
        {
            originalToProxy.Clear();
            proxyToOriginal.Clear();
            proxyGenerationTasks.Clear();
            proxyPathCache.Clear();
            fileSizeCache.Clear();
            proxyExistsCache.Clear();

            if (Directory.Exists(proxyCacheFolder))
            {
                foreach (var file in Directory.GetFiles(proxyCacheFolder))
                {
                    try { File.Delete(file); } catch { }
                }
            }
        }
    }
}