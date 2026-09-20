Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

// マップ（法線の計算元）用テクスチャ
Texture2D MapTexture : register(t1);
SamplerState MapSampler : register(s1);

cbuffer Constants : register(b0)
{
    float Amount;
    float Blur;
    float Steps;
    float Angle;
};

// 法線ぼかしカーネル。半径とσは固定なので、重みはコンパイル時に定数へ畳み込まれる。
#define NORMAL_BLUR_RADIUS 4
#define NORMAL_BLUR_SIGMA 2.0

static const float3 CHROMA_CENTER = float3(0.0, 0.5, 1.0);

float GetLuminance(float3 col)
{
    return dot(col, float3(0.2126, 0.7152, 0.0722));
}

float2 PixelToUVOffset(float2 pixelOffset, float2 duvdx, float2 duvdy)
{
    return pixelOffset.x * duvdx + pixelOffset.y * duvdy;
}

float MapLuminance(float2 map_uv, float2 pixelOffset, float2 dmap_dx, float2 dmap_dy)
{
    float2 uv = map_uv + PixelToUVOffset(pixelOffset, dmap_dx, dmap_dy);
    return GetLuminance(MapTexture.SampleLevel(MapSampler, uv, 0).rgb);
}

float3 NormalFromGradient(float2 gradient, float angle_val)
{
    float3 normal = normalize(float3(-gradient * 4.0, 1.0));

    float rad = radians(angle_val);
    float c = cos(rad);
    float s = sin(rad);
    normal.xy = float2(normal.x * c - normal.y * s,
                       normal.x * s + normal.y * c);
    return normal;
}

// ぼかし無しの場合の輝度勾配。中心差分をそのまま使う。
float2 SharpGradient(float2 map_uv, float2 dmap_dx, float2 dmap_dy)
{
    const float d = 2.0;
    return float2(
        MapLuminance(map_uv, float2(d, 0.0), dmap_dx, dmap_dy) - MapLuminance(map_uv, float2(-d, 0.0), dmap_dx, dmap_dy),
        MapLuminance(map_uv, float2(0.0, d), dmap_dx, dmap_dy) - MapLuminance(map_uv, float2(0.0, -d), dmap_dx, dmap_dy));
}

// ぼかし有りの場合の輝度勾配。
// ガウシアンでぼかした法線を平均するのではなく、ガウス微分（DoG）で
// 「ぼかした輝度の勾配」を直接求める。1タップあたりのサンプルが4回から1回になる。
// 最後に (4 + Blur) を掛けて、中心差分版（2 * sampleDist）と同じ強度に揃える。
float2 BlurredGradient(float2 map_uv, float2 dmap_dx, float2 dmap_dy)
{
    const int r = NORMAL_BLUR_RADIUS;
    const float twoSigmaSq = 2.0 * NORMAL_BLUR_SIGMA * NORMAL_BLUR_SIGMA;
    float stride = 1.0 + Blur * 0.3;

    float2 sum = float2(0.0, 0.0);
    float norm = 0.0;

    [unroll]
    for (int y = -r; y <= r; y++)
    {
        [unroll]
        for (int x = -r; x <= r; x++)
        {
            float weight = exp(-(x * x + y * y) / twoSigmaSq);
            sum += weight * float2(x, y) * MapLuminance(map_uv, float2(x, y) * stride, dmap_dx, dmap_dy);
            norm += weight * x * x;
        }
    }

    return sum / (norm * stride) * (4.0 + Blur);
}

float4 main(
    float4 pos : SV_POSITION,
    float4 posScene : SCENE_POSITION,
    float4 uv0 : TEXCOORD0,
    float4 uv1 : TEXCOORD1
) : SV_Target
{
    float2 uv = uv0.xy;
    float2 map_uv = uv1.xy;

    float originalAlpha = InputTexture.SampleLevel(InputSampler, uv, 0).a;
    if (originalAlpha <= 0.001)
        return float4(0.0, 0.0, 0.0, 0.0);

    float2 duvdx = ddx(uv);
    float2 duvdy = ddy(uv);
    float2 dmap_dx = ddx(map_uv);
    float2 dmap_dy = ddy(map_uv);

    // [branch] は必須。三項演算子のままだと両方の経路が実行され、
    // 滑らかさ=0 でもぼかし用の81タップを払うことになる。
    // Blur は定数バッファ由来なので分岐は完全にコヒーレントで、コストはない。
    float2 gradient;
    [branch]
    if (Blur <= 0.01)
        gradient = SharpGradient(map_uv, dmap_dx, dmap_dy);
    else
        gradient = BlurredGradient(map_uv, dmap_dx, dmap_dy);
    float3 normal = NormalFromGradient(gradient, Angle);

    int maxSteps = max(3, (int) Steps);
    float3 texColor = float3(0.0, 0.0, 0.0);
    float3 weightSum = float3(0.0, 0.0, 0.0);

    [loop]
    for (int i = 0; i < maxSteps; i++)
    {
        float fi = (float) i / (float) (maxSteps - 1);

        // R/G/B をそれぞれ fi = 0.0 / 0.5 / 1.0 に寄せる三角形の重み
        float3 chroma = saturate(1.0 - abs(fi - CHROMA_CENTER) * 2.0);
        weightSum += chroma;

        float2 displacedUV = uv + PixelToUVOffset(normal.xy * Amount * fi, duvdx, duvdy);
        texColor += chroma * InputTexture.SampleLevel(InputSampler, displacedUV, 0).rgb;
    }

    return saturate(float4(texColor / max(weightSum, 0.001), originalAlpha));
}
