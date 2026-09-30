using DistortChroma;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Brush;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin;
using YukkuriMovieMaker.Plugin.Brush;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.Project;
using YukkuriMovieMaker.Resources.Localization;

namespace DistortChroma
{
    public enum DistortChromaMapSource
    {
        [Display(Name = "アイテム自身")]
        Self,
        [Display(Name = "別の画像・シーン")]
        Other
    }

    public enum DistortChromaMapChannel
    {
        [Display(Name = "輝度")]
        Luminance,
        [Display(Name = "不透明度")]
        Alpha
    }

    [VideoEffect("DistortChroma", ["加工"], ["distort", "chroma", "歪み", "色収差", "色ズレ", "ブラー"])]
    internal class DistortChromaEffect : VideoEffectBase, IFileItem, IResourceItem
    {
        private DistortChromaMapSource sourceMode = DistortChromaMapSource.Self;
        private DistortChromaMapChannel mapChannel = DistortChromaMapChannel.Luminance;

        public override string Label => "DistortChroma";

        [Display(GroupName = "マップ", Name = "ソース", Description = "歪みの方向を計算するためのソースを選択します。")]
        [EnumComboBox]
        public DistortChromaMapSource SourceMode
        {
            get => sourceMode;
            set => Set(ref sourceMode, value);
        }

        [Display(GroupName = "マップ", Name = "参照値", Description = "歪みの方向を何の変化から計算するかを選択します。「不透明度」にすると、模様や色の違いに影響されず、形の輪郭だけに沿って色ズレします。")]
        [EnumComboBox]
        public DistortChromaMapChannel MapChannel
        {
            get => mapChannel;
            set => Set(ref mapChannel, value);
        }

        [Display(GroupName = "マップ", Name = "マップ画像", Description = "「別の画像・シーン」選択時に歪みのソースとして使用される画像です。", AutoGenerateField = true)]
        public Brush Brush { get; } = CreateBitmapBrush();

        // BitmapBrushPluginを裏側から初期化するためのヘルパーメソッド
        private static Brush CreateBitmapBrush()
        {
            // YMM4が読み込んでいる全プラグインの中から "BitmapBrushPlugin" の「型(Type)」を探す
            var pluginType = PluginLoader.Plugins
                .OfType<IBrushPlugin>()
                .FirstOrDefault(p => p.GetType().Name == "BitmapBrushPlugin")?.GetType()
                ?? PluginLoader.Plugins.OfType<IBrushPlugin>().First().GetType();

            // Brush.Create<T>() メソッドをプログラムの裏側から見つけ出し、見つけた型を当てはめて実行する
            var createMethod = typeof(Brush).GetMethods().First(m => m.Name == "Create" && m.IsGenericMethod);
            var genericMethod = createMethod.MakeGenericMethod(pluginType);

            // 実行結果（初期化されたBrush）を返す
            return (Brush)genericMethod.Invoke(null, null)!;
        }

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
        public Animation Angle { get; } = new Animation(0f, -36000, 36000);

        [Display(GroupName = "基本", Name = "基準位置", Description = "色ズレの基準位置です。0%で元の位置から片側へずれ、50%で元の位置を中心に両側へ広がる（全体の位置がずれない）ようになります。")]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Center { get; } = new Animation(0f, -100, 200);

        [Display(GroupName = "色", Name = "色相", Description = "収差の開始色（歪みが0の側の色）を色相で指定します。0で赤、120で緑、240で青です。")]
        [AnimationSlider("F1", "度", 0, 360)]
        public Animation HueStart { get; } = new Animation(0f, -3600, 3600);

        [Display(GroupName = "色", Name = "色相範囲", Description = "開始色から歪み最大側の色までの色相の幅です。240で従来通り（赤→緑→青）、360で虹色一周、マイナスで逆回りになります。")]
        [AnimationSlider("F1", "度", -360, 360)]
        public Animation HueRange { get; } = new Animation(240f, -3600, 3600);

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new DistortChromaEffectProcessor(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Amount, Blur, Steps, Angle, Center, HueStart, HueRange, Brush];

        // --- パッケージング・ファイルパス一括置換への対応 ---
        public override IEnumerable<string> GetFiles()
        {
            foreach (var file in base.GetFiles()) yield return file;
            if (Brush != null)
                foreach (var file in Brush.GetFiles()) yield return file;
        }

        public override void ReplaceFile(string from, string to)
        {
            base.ReplaceFile(from, to);
            Brush?.ReplaceFile(from, to);
        }

        public override IEnumerable<TimelineResource> GetResources()
        {
            foreach (var res in base.GetResources()) yield return res;
            if (Brush != null)
                foreach (var res in Brush.GetResources()) yield return res;
        }
    }
}