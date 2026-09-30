using System;
using System.IO;
using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace DistortChroma
{
    internal class DistortChromaCustomEffect : D2D1CustomShaderEffectBase
    {
        public float Amount { set => SetValue((int)Props.Amount, value); }
        public float Blur { set => SetValue((int)Props.Blur, value); }
        public float Steps { set => SetValue((int)Props.Steps, value); }
        public float Angle { set => SetValue((int)Props.Angle, value); }
        public float HueStart { set => SetValue((int)Props.HueStart, value); }
        public float HueRange { set => SetValue((int)Props.HueRange, value); }
        public float Center { set => SetValue((int)Props.Center, value); }

        public DistortChromaCustomEffect(IGraphicsDevicesAndContext devices) : base(Create<EffectImpl>(devices)) { }

        [StructLayout(LayoutKind.Sequential)]
        struct ConstantBuffer
        {
            public float Amount;
            public float Blur;
            public float Steps;
            public float Angle;
            public float HueStart;
            public float HueRange;
            public float Center;
            // HLSLの定数バッファは16バイト単位のため、32バイトに揃える
            public float Padding0;
        }

        private enum Props { Amount, Blur, Steps, Angle, HueStart, HueRange, Center }

        // ★入力を2つ（描画用 t0, マップ用 t1）にするため 2 を指定
        [CustomEffect(2)]
        private class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            private ConstantBuffer constants;

            protected override void UpdateConstants()
            {
                if (drawInformation != null) drawInformation.SetPixelShaderConstantBuffer(constants);
            }

            public override void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect)
            {
                if (inputRects.Length > 0) outputRect = inputRects[0];
                else outputRect = new RawRect();
                outputOpaqueSubRect = new RawRect();
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                // 歪み量（基準位置によって最大で Amount × max(|Center|, |1 - Center|) ずれる）
                // + 法線ぼかしの広がり（σの約3倍 + 輝度差分の距離）を余白として確保する
                float maxShift = Math.Abs(constants.Amount) * Math.Max(Math.Abs(constants.Center), Math.Abs(1f - constants.Center));
                int margin = (int)(maxShift + Math.Max(constants.Blur, 0f) * 3.0f) + 10;

                var expandedRect = new RawRect(
                    outputRect.Left - margin, outputRect.Top - margin,
                    outputRect.Right + margin, outputRect.Bottom + margin
                );

                // 入力0（自身）と入力1（マップ）の両方に余白付きの範囲を要求する
                if (inputRects.Length > 0) inputRects[0] = expandedRect;
                if (inputRects.Length > 1) inputRects[1] = expandedRect;
            }

            private static byte[] LoadShader()
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                using var stream = assembly.GetManifestResourceStream("DistortChroma.Shaders.DistortChromaShader.cso");
                if (stream == null) throw new FileNotFoundException("DistortChromaShader.cso not found");
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                return ms.ToArray();
            }

            public EffectImpl() : base(LoadShader())
            {
                constants = new ConstantBuffer { Amount = 10f, Blur = 3f, Steps = 10f, Angle = 0f, HueStart = 0f, HueRange = 240f, Center = 0f };
            }

            [CustomEffectProperty(PropertyType.Float, (int)Props.Amount)] public float Amount { get => constants.Amount; set { constants.Amount = value; UpdateConstants(); } }
            [CustomEffectProperty(PropertyType.Float, (int)Props.Blur)] public float Blur { get => constants.Blur; set { constants.Blur = value; UpdateConstants(); } }
            [CustomEffectProperty(PropertyType.Float, (int)Props.Steps)] public float Steps { get => constants.Steps; set { constants.Steps = value; UpdateConstants(); } }
            [CustomEffectProperty(PropertyType.Float, (int)Props.Angle)] public float Angle { get => constants.Angle; set { constants.Angle = value; UpdateConstants(); } }
            [CustomEffectProperty(PropertyType.Float, (int)Props.HueStart)] public float HueStart { get => constants.HueStart; set { constants.HueStart = value; UpdateConstants(); } }
            [CustomEffectProperty(PropertyType.Float, (int)Props.HueRange)] public float HueRange { get => constants.HueRange; set { constants.HueRange = value; UpdateConstants(); } }
            [CustomEffectProperty(PropertyType.Float, (int)Props.Center)] public float Center { get => constants.Center; set { constants.Center = value; UpdateConstants(); } }
        }
    }
}