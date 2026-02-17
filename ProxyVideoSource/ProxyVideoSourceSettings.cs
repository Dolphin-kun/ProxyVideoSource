using System.Text.Json.Serialization;
using YukkuriMovieMaker.Plugin;

namespace ProxyVideoSource
{
    internal class ProxyVideoSourceSettings : SettingsBase<ProxyVideoSourceSettings>
    {
        public override SettingsCategory Category => SettingsCategory.VideoFileSource;
        public override string Name => "動画プロキシ設定";

        public override bool HasSettingView => true;
        public override object? SettingView => new ProxyVideoSourceSettingsView();

        [JsonIgnore]
        public static int MaxCpuCoreCount => Environment.ProcessorCount;

        // 基本設定
        private bool useProxy = true;
        public bool UseProxy { get => useProxy; set => Set(ref useProxy, value); }

        private int scale = 50;
        public int Scale { get => scale; set => Set(ref scale, value); }

        private int minFileSizeForProxy = 100;
        public int MinFileSizeForProxy { get => minFileSizeForProxy; set => Set(ref minFileSizeForProxy, value); }

        // 自動化設定
        private bool autoGenerate = true;
        public bool AutoGenerate { get => autoGenerate; set => Set(ref autoGenerate, value); }

        private bool clearCacheOnExit = false;
        public bool ClearCacheOnExit { get => clearCacheOnExit; set => Set(ref clearCacheOnExit, value); }

        // 詳細設定
        private bool useAdvancedSetting = false;
        public bool UseAdvancedSetting
        {
            get => useAdvancedSetting;
            set
            {
                if (Set(ref useAdvancedSetting, value))
                {
                    if (!value)
                    {
                        CpuCoreCount = 0;
                    }
                }
            }
        }

        private int cpuCoreCount = 0;
        public int CpuCoreCount
        {
            get => cpuCoreCount;
            set
            {
                int max = Environment.ProcessorCount;

                if (value < 0)
                    value = 0;
                if (value > max)
                    value = max;

                Set(ref cpuCoreCount, value);
            }
        }

        public override void Initialize()
        {
        }
    }
}
