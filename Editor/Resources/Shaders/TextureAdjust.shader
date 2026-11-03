Shader "Hidden/EAUploader/TextureAdjust"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }

    SubShader
    {
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
            float _Hue;
            float _Saturation;
            float _Brightness;
            float _Contrast;
            float4 _Tint;
            float _IsSRGB;

            // HSV conversion
            float3 RGBToHSV(float3 c)
            {
                float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
                float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
                float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
                float d = q.x - min(q.w, q.y);
                float e = 1.0e-10;
                return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
            }

            float3 HSVToRGB(float3 c)
            {
                float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
                return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
            }

            float4 frag(v2f_img i) : SV_Target
            {
                float4 col = tex2D(_MainTex, i.uv);
                float3 rgb = col.rgb;

                #ifndef UNITY_COLORSPACE_GAMMA
                if (_IsSRGB > 0.5) rgb = LinearToGammaSpace(rgb);
                #endif

                float3 hsv = RGBToHSV(saturate(rgb));
                hsv.x = frac(hsv.x + _Hue);
                hsv.y = saturate(hsv.y * _Saturation);
                hsv.z = hsv.z * _Brightness;
                rgb = HSVToRGB(hsv);
                rgb = (rgb - 0.5) * _Contrast + 0.5;
                rgb = saturate(rgb * _Tint.rgb);

                #ifndef UNITY_COLORSPACE_GAMMA
                if (_IsSRGB > 0.5) rgb = GammaToLinearSpace(rgb);
                #endif

                return float4(rgb, col.a);
            }
            ENDCG
        }
    }
}
