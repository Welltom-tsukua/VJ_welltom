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
        _Scale ("Scale", Float) = 1
        _ScaleX ("Scale X", Float) = 1
        _ScaleY ("Scale Y", Float) = 1
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
            float _Scale;
            float _ScaleX;
            float _ScaleY;

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
                float scaleX = max(_ScaleX, 0.01);
                float scaleY = max(_ScaleY, 0.01);
                float2 overlayUv = float2((i.uv.x - 0.5) / scaleX + 0.5, (i.uv.y - 0.5) / scaleY + 0.5);
                fixed4 overlay = tex2D(_OverlayTex, saturate(overlayUv));
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
                fixed alphaOut = saturate(baseCol.a + overlayAlpha * (1.0 - baseCol.a));

                if (_BlendMode < 0.5)
                {
                    fixed3 rgb = lerp(baseCol.rgb, overlay.rgb, overlayAlpha);
                    return fixed4(rgb, alphaOut);
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
                if (_BlendMode < 3.5)
                {
                    fixed maskLuma = saturate(dot(overlay.rgb, fixed3(0.299, 0.587, 0.114)) * overlayAlpha);
                    fixed3 maskRgb = lerp(baseCol.rgb, overlay.rgb, maskLuma);
                    fixed maskAlpha = lerp(baseCol.a, overlay.a, maskLuma);
                    return fixed4(maskRgb, maskAlpha);
                }
                if (_BlendMode < 4.5)
                {
                    fixed3 screenRgb = 1.0 - ((1.0 - baseCol.rgb) * (1.0 - overlay.rgb));
                    return fixed4(lerp(baseCol.rgb, screenRgb, overlayAlpha), alphaOut);
                }
                if (_BlendMode < 5.5)
                {
                    fixed3 multiplyRgb = baseCol.rgb * overlay.rgb;
                    return fixed4(lerp(baseCol.rgb, multiplyRgb, overlayAlpha), alphaOut);
                }
                if (_BlendMode < 6.5)
                {
                    fixed3 lightenRgb = max(baseCol.rgb, overlay.rgb);
                    return fixed4(lerp(baseCol.rgb, lightenRgb, overlayAlpha), alphaOut);
                }
                if (_BlendMode < 7.5)
                {
                    fixed3 darkenRgb = min(baseCol.rgb, overlay.rgb);
                    return fixed4(lerp(baseCol.rgb, darkenRgb, overlayAlpha), alphaOut);
                }
                if (_BlendMode < 8.5)
                {
                    fixed3 differenceRgb = abs(baseCol.rgb - overlay.rgb);
                    return fixed4(lerp(baseCol.rgb, differenceRgb, overlayAlpha), alphaOut);
                }
                if (_BlendMode < 9.5)
                {
                    fixed3 low = 2.0 * baseCol.rgb * overlay.rgb;
                    fixed3 high = 1.0 - 2.0 * (1.0 - baseCol.rgb) * (1.0 - overlay.rgb);
                    fixed3 overlayRgb = lerp(low, high, step(0.5, baseCol.rgb));
                    return fixed4(lerp(baseCol.rgb, saturate(overlayRgb), overlayAlpha), alphaOut);
                }
                if (_BlendMode < 10.5)
                {
                    fixed3 subtractRgb = saturate(baseCol.rgb - overlay.rgb);
                    return fixed4(lerp(baseCol.rgb, subtractRgb, overlayAlpha), alphaOut);
                }
                return fixed4(lerp(baseCol.rgb, overlay.rgb, overlayAlpha), alphaOut);
            }
            ENDCG
        }
    }
}
