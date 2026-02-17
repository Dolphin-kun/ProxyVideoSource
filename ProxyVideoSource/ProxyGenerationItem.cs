using YukkuriMovieMaker.Commons;

namespace ProxyVideoSource
{
    public class ProxyGenerationItem : Bindable
    {
        public string FileName { get; }
        public string OriginalPath { get; }

        private double progress;
        public double Progress
        {
            get => progress;
            set
            {
                if (Set(ref progress, value))
                {
                    OnPropertyChanged(nameof(ProgressText));
                }
            }
        }

        public string ProgressText => $"{Progress:F0}%";

        private bool isCompleted;
        public bool IsCompleted
        {
            get => isCompleted;
            set => Set(ref isCompleted, value);
        }

        public ProxyGenerationItem(string originalPath)
        {
            OriginalPath = originalPath;
            FileName = System.IO.Path.GetFileName(originalPath);
            Progress = 0;
            IsCompleted = false;
        }
    }
}
