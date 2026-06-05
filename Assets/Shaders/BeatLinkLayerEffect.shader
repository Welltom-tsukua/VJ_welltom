Shader "BeatLink/LayerEffect"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Mode ("Mode", Float) = 0
        _Intensity ("Intensity", Float) = 1
        _Axis ("Axis", Float) = 0
        _Phase ("Phase", Float) = 0
        _TimeNow ("Time", Float) = 0
        _StrobePhase ("Strobe Phase", Float) = 0
        _ManualHold ("Manual Hold", Float) = 0
        _BeatCount ("Beat Count", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Transparent" }
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Mode;
            float _Intensity;
            float _Axis;
            float _Phase;
            float _TimeNow;
            float _StrobePhase;
            float _ManualHold;
            float _BeatCount;

            fixed4 SampleMain(float2 uv)
            {
                return tex2D(_MainTex, saturate(uv));
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 uv = i.uv;
                fixed4 col = SampleMain(uv);

                if (_Mode < 0.5)
                {
                    return col;
                }

                if (_Mode < 1.5)
                {
                    return fixed4(col.r, 0, 0, col.a);
                }
                if (_Mode < 2.5)
                {
                    return fixed4(0, col.g, 0, col.a);
                }
                if (_Mode < 3.5)
                {
                    return fixed4(0, 0, col.b, col.a);
                }
                if (_Mode < 4.5)
                {
                    return fixed4(1.0 - col.rgb, col.a);
                }
                if (_Mode < 5.5)
                {
                    float2 texel = _MainTex_TexelSize.xy;
                    fixed3 gx = SampleMain(uv + float2(texel.x, 0)).rgb - SampleMain(uv - float2(texel.x, 0)).rgb;
                    fixed3 gy = SampleMain(uv + float2(0, texel.y)).rgb - SampleMain(uv - float2(0, texel.y)).rgb;
                    fixed edge = saturate(length(gx) + length(gy));
                    fixed3 result = lerp(col.rgb, edge.xxx, saturate(_Intensity));
                    return fixed4(result, col.a);
                }
                if (_Mode < 6.5)
                {
                    fixed mono = dot(col.rgb, fixed3(0.299, 0.587, 0.114));
                    return fixed4(mono, mono, mono, col.a);
                }
                if (_Mode < 7.5)
                {
                    float phase = frac(_BeatCount);
                    float impulse = exp(-phase * 9.5);
                    float amount = (0.01 + impulse * 0.11) * saturate(_Intensity);

                    float axisMode = _Axis;
                    if (axisMode > 1.5 && axisMode < 2.5)
                    {
                        axisMode = fmod(floor(_BeatCount), 2.0) < 0.5 ? 0.0 : 1.0;
                    }

                    if (axisMode > 2.5)
                    {
                        float2 centerDelta = uv - float2(0.5, 0.5);
                        float zoom = 1.0 + amount * 2.8;
                        float2 baseUv = centerDelta / zoom + float2(0.5, 0.5);
                        return SampleMain(baseUv);
                    }

                    float2 dir = axisMode > 0.5 ? float2(0, 1) : float2(1, 0);
                    float2 spread = dir * amount;
                    return SampleMain(uv + spread);
                }
                if (_Mode < 8.5)
                {
                    float stripe = floor(uv.y * 42.0 + _TimeNow * 4.0);
                    float randomValue = frac(sin(stripe * 17.13 + _TimeNow * 11.7) * 43758.5453);
                    float gate = step(0.62, frac(stripe * 0.17 + _Phase));
                    float glitchShift = (randomValue - 0.5) * 0.18 * saturate(_Intensity) * gate;
                    float2 glitchUv = uv + float2(glitchShift, 0);
                    fixed4 glitch = SampleMain(glitchUv);
                    glitch.g = SampleMain(glitchUv + float2(_MainTex_TexelSize.x * 2.0, 0)).g;
                    glitch.b = SampleMain(glitchUv - float2(_MainTex_TexelSize.x * 2.0, 0)).b;
                    return glitch;
                }
                if (_Mode < 9.5)
                {
                    float pulse = _ManualHold > 0.5 ? 1.0 : saturate(1.0 - _StrobePhase * 6.0);
                    pulse *= pulse;
                    pulse *= saturate(_Intensity);
                    return fixed4(1.0, 1.0, 1.0, pulse);
                }

                float2 tiled = frac(uv * 2.0);
                return SampleMain(tiled);
            }
            ENDCG
        }
    }
}
