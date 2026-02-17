using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.FileSource;

namespace ProxyVideoSource
{
    public class ProxyVideoSourceWithScale : IVideoFileSource
    {
        public TimeSpan Duration => wrappedSource.Duration;
        public ID2D1Image Output => output;

        private readonly IVideoFileSource wrappedSource;
        private readonly AffineTransform2D scaleEffect;
        private readonly ID2D1Image output;

        public ProxyVideoSourceWithScale(IVideoFileSource wrappedSource, IGraphicsDevicesAndContext devices, float scale)
        {
            this.wrappedSource = wrappedSource;

            scaleEffect = new AffineTransform2D(devices.DeviceContext);
            scaleEffect.SetInput(0, wrappedSource.Output, true);
            scaleEffect.TransformMatrix = Matrix3x2.CreateScale(1f / scale);

            output = scaleEffect.Output; // EffectでgetしたOutputは必ずDisposeする。Effect側では開放されない。
        }

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
            output.Dispose(); // EffectでgetしたOutputは必ずDisposeする。Effect側では開放されない。
            scaleEffect.SetInput(0, null, true); // Inputは必ずnullに戻す。
            scaleEffect.Dispose();
            wrappedSource?.Dispose();

            GC.SuppressFinalize(this);
        }
    }
}
