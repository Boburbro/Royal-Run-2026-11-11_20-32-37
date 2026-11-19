// Royal Run osmoni: gradient, yumshoq bulutlar, quyosh shu'lasi va tunda yulduzlar.
// Resources papkasida turgani uchun build'ga avtomatik kiradi (Shader.Find ishlaydi).
Shader "RoyalRun/Sky"
{
    Properties
    {
        _Top ("Top", Color) = (0.25, 0.55, 0.95, 1)
        _Horizon ("Horizon", Color) = (0.78, 0.9, 1, 1)
        _Ground ("Ground", Color) = (0.86, 0.8, 0.7, 1)
        _SunDir ("Sun Dir", Vector) = (0.3, 0.15, 1, 0)
        _SunColor ("Sun Color", Color) = (1, 0.9, 0.7, 1)
        _Clouds ("Clouds", Float) = 1
        _Stars ("Stars", Float) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Top, _Horizon, _Ground, _SunColor;
                float4 _SunDir;
                half _Clouds, _Stars;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.dir = i.positionOS.xyz;
                return o;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i), b = hash21(i + float2(1, 0)), c = hash21(i + float2(0, 1)), d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float y = d.y;
                half3 col;
                if (y > 0)
                    col = lerp(_Horizon.rgb, _Top.rgb, pow(saturate(y), 0.55));
                else
                    col = lerp(_Horizon.rgb, _Ground.rgb, saturate(-y * 6.0));

                // quyosh shu'lasi
                float s = saturate(dot(d, normalize(_SunDir.xyz)));
                col += _SunColor.rgb * (pow(s, 900.0) * 1.5 + pow(s, 12.0) * 0.35);

                // bulutlar: osmon tekisligiga proyeksiya qilingan shovqin
                if (y > 0.0)
                {
                    float2 uv = d.xz / (y + 0.12) * 1.6;
                    uv.x += _Time.x * 0.6;
                    float n = vnoise(uv) * 0.55 + vnoise(uv * 2.1 + 3.7) * 0.3 + vnoise(uv * 4.3 + 7.1) * 0.15;
                    float cl = smoothstep(0.52, 0.78, n) * saturate(y * 5.0) * _Clouds;
                    half3 cloudCol = lerp(_Horizon.rgb, half3(1, 1, 1), 0.65) + _SunColor.rgb * 0.08;
                    col = lerp(col, cloudCol, cl * 0.8);

                    // yulduzlar (tunda)
                    float2 sp = d.xz / (y + 0.3) * 90.0;
                    float st = step(0.985, hash21(floor(sp))) * saturate(y * 4.0) * (1.0 - cl);
                    col += st * _Stars * (0.6 + 0.4 * sin(_Time.y * 3.0 + hash21(floor(sp) + 1.0) * 6.28));
                }
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
