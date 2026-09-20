using System.ComponentModel.DataAnnotations;
using System.Reflection;
using YukkuriMovieMaker.Brush;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin;
using YukkuriMovieMaker.Plugin.Brush;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.Project;

namespace DistortChroma
{
    public enum DistortChromaMapSource
    {
        [Display(Name = "アイテム自身")]
        Self,
        [Display(Name = "別の画像・シーン")]
        Other
    }

    [VideoEffect("DistortChroma", ["加工"], ["distort", "chroma", "歪み", "色収差", "色ズレ", "ブラー"])]
    internal class DistortChromaEffect : VideoEffectBase, IFileItem, IResourceItem
    {
        private DistortChromaMapSource sourceMode = DistortChromaMapSource.Self;

        public override string Label => "DistortChroma";

        [Display(GroupName = "マップ", Name = "ソース", Description = "歪みの方向を計算するためのソースを選択します。")]
        [EnumComboBox]
        public DistortChromaMapSource SourceMode
        {
            get => sourceMode;
            set => Set(ref sourceMode, value);
        }

        [Display(GroupName = "マップ", Name = "マップ画像", Description = "「別の画像・シーン」選択時に歪みのソースとして使用される画像です。", AutoGenerateField = true)]
        public Brush Brush { get; } = CreateBitmapBrush();

        // BitmapBrushPlugin は公開型ではないため、Brush.Create<T>() をリフレクション経由で呼ぶ。
        // 型とメソッドの解決結果はプロセス内で変わらないので、一度だけ行って使い回す。
        // PublicationOnly: 失敗を握り込まないため。プラグイン列挙が間に合わず例外になっても、
        // 次のインスタンス生成でやり直せる。
        private static readonly Lazy<MethodInfo> BitmapBrushFactory = new(() =>
        {
            var pluginType = PluginLoader.Plugins
                .OfType<IBrushPlugin>()
                .FirstOrDefault(p => p.GetType().Name == "BitmapBrushPlugin")?.GetType()
                ?? PluginLoader.Plugins.OfType<IBrushPlugin>().First().GetType();

            var createMethod = typeof(Brush).GetMethods().First(m => m.Name == "Create" && m.IsGenericMethod);
            return createMethod.MakeGenericMethod(pluginType);
        }, LazyThreadSafetyMode.PublicationOnly);

        private static Brush CreateBitmapBrush() => (Brush)BitmapBrushFactory.Value.Invoke(null, null)!;

        [Display(GroupName = "基本", Name = "歪み強度", Description = "エフェクトの強さ（ピクセル単位）を指定します。")]
        [AnimationSlider("F1", "px", -100, 100)]
        public Animation Amount { get; } = new Animation(10f, -5000, 5000);

        [Display(GroupName = "基本", Name = "滑らかさ", Description = "歪みの滑らかさ（ブラー強度）です。")]
        [AnimationSlider("F1", "px", 0, 10)]
        public Animation Blur { get; } = new Animation(3f, 0, 100);

        [Display(GroupName = "基本", Name = "品質", Description = "色を分ける段階数です。高いほど滑らかになります。")]
        [AnimationSlider("F0", "段", 3, 32)]
        public Animation Steps { get; } = new Animation(16, 3, 64);

        [Display(GroupName = "基本", Name = "角度", Description = "歪みの回転角度です。")]
        [AnimationSlider("F1", "度", -360, 360)]
        public Animation Angle { get; } = new Animation(0f, -360, 360);

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
            => new DistortChromaEffectProcessor(devices, this);

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Amount, Blur, Steps, Angle, Brush];

        // --- パッケージング・ファイルパス一括置換への対応 ---
        public override IEnumerable<string> GetFiles() => base.GetFiles().Concat(Brush.GetFiles());

        public override void ReplaceFile(string from, string to)
        {
            base.ReplaceFile(from, to);
            Brush.ReplaceFile(from, to);
        }

        public override IEnumerable<TimelineResource> GetResources() => base.GetResources().Concat(Brush.GetResources());
    }
}
