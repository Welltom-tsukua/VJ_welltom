Shader "BeatLink/LayerMaskComposite"
{
    Properties
    {
        _MainTex ("Lower Composite", 2D) = "black" {}
        _UpperTex ("Upper Composite", 2D) = "black" {}
        _MaskTex ("Mask", 2D) = "black" {}
        _MaskOpacity ("Mask Opacity", Float) = 1
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
            sampler2D _UpperTex;
            sampler2D _MaskTex;
            float _MaskOpacity;

            fixed4 frag(v2f_img i) : SV_Target
            {
                fixed4 lower = tex2D(_MainTex, i.uv);
                fixed4 upper = tex2D(_UpperTex, i.uv);
                fixed4 mask = tex2D(_MaskTex, i.uv);
                fixed maskLuma = saturate(dot(mask.rgb, fixed3(0.299, 0.587, 0.114)));
                maskLuma = lerp(1.0, maskLuma, saturate(_MaskOpacity));
                return lerp(lower, upper, maskLuma);
            }
            ENDCG
        }
    }
}
