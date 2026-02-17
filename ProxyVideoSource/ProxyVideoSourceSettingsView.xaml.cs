using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace ProxyVideoSource
{
    public partial class ProxyVideoSourceSettingsView : UserControl
    {
        public ProxyVideoSourceSettingsView()
        {
            InitializeComponent();

            CpuCoreSlider.Max = ProxyVideoSourceSettings.MaxCpuCoreCount;
        }

        private void ClearCacheButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var (fileCount, totalSize) = GetCacheInfo();

                if (fileCount == 0)
                {
                    MessageBox.Show(
                        "削除するキャッシュファイルがありません。",
                        "情報",
                        MessageBoxButton.OK);
                    return;
                }

                string sizeText = FormatBytes(totalSize);

                var result = MessageBox.Show(
                    $"プロキシキャッシュを削除しますか？\n\n" +
                    $"ファイル数: {fileCount:N0} 個\n" +
                    $"合計サイズ: {sizeText}\n\n" +
                    $"次回動画読み込み時に再生成されます。",
                    "キャッシュクリア確認",
                    MessageBoxButton.YesNo);

                if (result == MessageBoxResult.Yes)
                {
                    ProxyManager.ClearCache();
                    MessageBox.Show(
                        $"{fileCount:N0} 個のファイル ({sizeText}) を削除しました。",
                        "完了",
                        MessageBoxButton.OK);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"キャッシュクリアに失敗しました: {ex.Message}",
                    "エラー",
                    MessageBoxButton.OK);
            }
        }

        private static (int fileCount, long totalSize) GetCacheInfo()
        {
            try
            {
                var cacheDir = ProxyManager.GetCacheDirectory();
                if (!Directory.Exists(cacheDir))
                {
                    return (0, 0);
                }

                var files = Directory.GetFiles(cacheDir, "*.*", SearchOption.AllDirectories);
                int fileCount = files.Length;
                long totalSize = files.Sum(file => new FileInfo(file).Length);

                return (fileCount, totalSize);
            }
            catch
            {
                return (0, 0);
            }
        }

        private static string FormatBytes(long bytes)
        {
            string[] sizes = ["B", "KB", "MB", "GB", "TB"];
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }
    }
}