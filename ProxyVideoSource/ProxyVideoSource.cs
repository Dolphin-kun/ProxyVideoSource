using Vortice.Direct2D1;
using YukkuriMovieMaker.Plugin.FileSource;

namespace ProxyVideoSource
{
    public class ProxyVideoSource(IVideoFileSource wrappedSource) : IVideoFileSource
    {
        public TimeSpan Duration => wrappedSource.Duration;
        public ID2D1Image Output => wrappedSource.Output;
        
        private readonly IVideoFileSource wrappedSource = wrappedSource;

        public void Update(TimeSpan time)
        {
            wrappedSource.Update(time);
        }

        public int GetFrameIndex(TimeSpan time)
        {
            return wrappedSource.GetFrameIndex(time);
        }

        public void Dispose()
        {
            wrappedSource?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
