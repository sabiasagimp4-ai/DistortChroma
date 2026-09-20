using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Brush;

namespace DistortChroma
{
    internal class DistortChromaEffectProcessor : IVideoEffectProcessor, IDisposable
    {
        private readonly DistortChromaEffect item;
        private readonly IGraphicsDevicesAndContext devices;

        private ID2D1Image? input;
        private DistortChromaCustomEffect? distortEffect;
        private IBrushSource? brushSource;
        private ID2D1CommandList? mapCommandList;

        public ID2D1Image Output { get; private set; } = null!;

        public DistortChromaEffectProcessor(IGraphicsDevicesAndContext devices, DistortChromaEffect item)
        {
            this.devices = devices;
            this.item = item;
        }

        public void SetInput(ID2D1Image? input) => this.input = input;

        public void ClearInput() => this.input = null;

        public DrawDescription Update(EffectDescription effectDescription)
        {
            Output?.Dispose();
            Output = null!;

            var source = input;
            if (source is null)
                return effectDescription.DrawDescription;

            try
            {
                distortEffect ??= new DistortChromaCustomEffect(devices);
            }
            catch
            {
                Dispose();
                return effectDescription.DrawDescription;
            }

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            distortEffect.Amount = (float)item.Amount.GetValue(frame, length, fps);
            distortEffect.Blur = (float)item.Blur.GetValue(frame, length, fps);
            distortEffect.Steps = (float)item.Steps.GetValue(frame, length, fps);
            distortEffect.Angle = (float)item.Angle.GetValue(frame, length, fps);

            distortEffect.SetInput(0, source, true);
            distortEffect.SetInput(1, item.SourceMode == DistortChromaMapSource.Other ? RenderMap(effectDescription, source) : source, true);

            Output = distortEffect.Output;
            return effectDescription.DrawDescription;
        }

        // 「別の画像・シーン」用のマップを描き起こす。ブラシは1フレームごとに変化しうるので毎回描き直す。
        private ID2D1CommandList RenderMap(EffectDescription effectDescription, ID2D1Image source)
        {
            brushSource ??= item.Brush.CreateBrush(devices);
            brushSource.Update((TimelineItemSourceDescription)effectDescription);

            var deviceContext = devices.DeviceContext;
            // 余白付きの範囲ではなく元の画像サイズに描画を限定する。
            // これにより歪み強度をいくら上げても、マップ側のレンダリング負荷は増えない。
            var bounds = deviceContext.GetImageLocalBounds(source);

            mapCommandList?.Dispose();
            mapCommandList = deviceContext.CreateCommandList();

            deviceContext.Target = mapCommandList;
            deviceContext.BeginDraw();
            deviceContext.Clear(null);
            deviceContext.FillRectangle(bounds, brushSource.Brush);
            deviceContext.EndDraw();
            deviceContext.Target = null;
            mapCommandList.Close();

            return mapCommandList;
        }

        public void Dispose()
        {
            distortEffect?.SetInput(0, null, true);
            distortEffect?.SetInput(1, null, true);
            distortEffect?.Dispose();
            distortEffect = null;

            mapCommandList?.Dispose();
            mapCommandList = null;
            brushSource?.Dispose();
            brushSource = null;

            Output?.Dispose();
            Output = null!;
            input = null;
        }
    }
}
