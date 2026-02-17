using System.Windows;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin;
using YukkuriMovieMaker.Plugin.FileSource;

namespace ProxyVideoSource
{
    internal class ProxyVideoSourcePlugin : IVideoFileSourcePlugin
    {
        public string Name => "動画プロキシプラグイン";

        public ProxyVideoSourcePlugin()
        {
            if(Application.Current != null)
            {
                Application.Current.Exit += OnApplicationExit;
            }
        }

        public IVideoFileSource? CreateVideoFileSource(IGraphicsDevicesAndContext devices, string filePath)
        {
            string actualPath = filePath;
            bool isProxy = false;
            float proxyScale = 1.0f;

            var settings = ProxyVideoSourceSettings.Default;
            bool isExporting = ExportDetector.IsExporting();

            // プロキシパスの場合は元動画に差し替え
            if (ProxyManager.IsProxyPath(filePath))
            {
                actualPath = ProxyManager.GetOriginalPath(filePath);
            }
            // 【出力中】元ファイルをそのまま使用
            else if (isExporting)
            {
                actualPath = filePath;
            }
            // 【編集時】プロキシを使用
            else if (settings.UseProxy && ProxyManager.IsFileLargeEnough(filePath, settings.MinFileSizeForProxy))
            {
                proxyScale = settings.Scale / 100f;
                var (resolvedPath, resolved) = ProxyManager.ResolvePathForEditing(filePath, proxyScale);
                actualPath = resolvedPath;
                isProxy = resolved;
            }
            // 自動生成のみ
            else if (settings.AutoGenerate && ProxyManager.IsFileLargeEnough(filePath, settings.MinFileSizeForProxy))
            {
                proxyScale = settings.Scale / 100f;
                if (!ProxyManager.ProxyExists(filePath, proxyScale))
                {
                    ProxyManager.GetOrCreateProxyPath(filePath, proxyScale);
                }
            }

            // 他のプラグインでオリジナルの動画ファイルを読み込む
            foreach (var plugin in PluginLoader.VideoFileSourcePlugins)
            {
                if (plugin == this)
                    continue;

                var source = plugin.CreateVideoFileSource(devices, actualPath);
                if (source != null)
                {
                    if (isProxy && proxyScale < 1.0f)
                    {
                        return new ProxyVideoSourceWithScale(source, devices, proxyScale);
                    }

                    return new ProxyVideoSource(source);
                }
            }

            return null;
        }


        private void OnApplicationExit(object? sender, ExitEventArgs e)
        {
            try
            {
                var settings = ProxyVideoSourceSettings.Default;
                if (settings.ClearCacheOnExit)
                {
                    ProxyManager.ClearCache();
                }
            }
            catch (Exception) { }
        }
    }
}