Shader "BeatLink/LayerComposite"
{
    Properties
    {
        _MainTex ("Base", 2D) = "black" {}
        _OverlayTex ("Overlay", 2D) = "white" {}
        _Opacity ("Opacity", Float) = 1
        _BlendMode ("Blend Mode", Float) = 0
        _HueShift ("Hue Shift", Float) = 0
        _ColorMode ("Color Mode", Float) = 0
        _InvertAmount ("Invert Amount", Float) = 0
        _MonochromeAmount ("Monochrome Amount", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
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
            sampler2D _OverlayTex;
            float _Opacity;
            float _BlendMode;
            float _HueShift;
            float _ColorMode;
            float _InvertAmount;
            float _MonochromeAmount;

            fixed3 rgb2hsv(fixed3 c)
            {
                fixed4 K = fixed4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
                fixed4 p = lerp(fixed4(c.bg, K.wz), fixed4(c.gb, K.xy), step(c.b, c.g));
                fixed4 q = lerp(fixed4(p.xyw, c.r), fixed4(c.r, p.yzx), step(p.x, c.r));
                fixed d = q.x - min(q.w, q.y);
                fixed e = 1.0e-10;
                return fixed3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
            }

            fixed3 hsv2rgb(fixed3 c)
            {
                fixed4 K = fixed4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                fixed3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
                return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                fixed4 baseCol = tex2D(_MainTex, i.uv);
                fixed4 overlay = tex2D(_OverlayTex, i.uv);
                fixed3 overlayHsv = rgb2hsv(saturate(overlay.rgb));
                overlayHsv.x = frac(overlayHsv.x + _HueShift);
                overlay.rgb = hsv2rgb(overlayHsv);
                fixed luma = dot(overlay.rgb, fixed3(0.299, 0.587, 0.114));
                fixed invertAmount = saturate(_InvertAmount);
                fixed monochromeAmount = saturate(_MonochromeAmount);
                overlay.rgb = lerp(overlay.rgb, 1.0 - overlay.rgb, invertAmount);
                luma = dot(overlay.rgb, fixed3(0.299, 0.587, 0.114));
                if (_ColorMode > 1.5 && _ColorMode < 2.5)
                {
                    fixed edge = saturate((abs(ddx(luma)) + abs(ddy(luma))) * 24.0);
                    overlay.rgb = edge.xxx;
                }
                overlay.rgb = lerp(overlay.rgb, luma.xxx, monochromeAmount);
                fixed opacity = saturate(_Opacity);
                fixed overlayAlpha = saturate(overlay.a * opacity);

                if (_BlendMode < 0.5)
                {
                    fixed3 rgb = lerp(baseCol.rgb, overlay.rgb, overlayAlpha);
                    fixed alpha = saturate(baseCol.a + overlayAlpha * (1.0 - baseCol.a));
                    return fixed4(rgb, alpha);
                }
                if (_BlendMode < 1.5)
                {
                    fixed luminance = dot(overlay.rgb, fixed3(0.299, 0.587, 0.114));
                    fixed addAlpha = (1.0 - luminance) * overlayAlpha;
                    return fixed4(saturate(baseCol.rgb + overlay.rgb * addAlpha), saturate(baseCol.a + addAlpha));
                }
                if (_BlendMode < 2.5)
                {
                    return fixed4(saturate(baseCol.rgb + overlay.rgb * overlayAlpha), saturate(baseCol.a + overlayAlpha));
                }

                fixed maskLuma = saturate(dot(overlay.rgb, fixed3(0.299, 0.587, 0.114)));
                fixed3 maskRgb = lerp(baseCol.rgb, overlay.rgb, maskLuma);
                fixed maskAlpha = lerp(baseCol.a, overlay.a, maskLuma);
                return fixed4(maskRgb, maskAlpha);
            }
            ENDCG
        }
    }
}
