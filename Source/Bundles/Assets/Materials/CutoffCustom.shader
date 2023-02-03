Shader "Unlit/CutoffCustom"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        CutoffAngle ("Cutoff Angle", float) = 0
        Distance("Distance", float) = 0
        Polarity("Polarity", float) = -1
        _Color ("Tint", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent"}
        LOD 100
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            const float PI = 3.141592653589793238462;

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float CutoffAngle;
            float Distance;
            float Polarity;
            float4 _Color;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                //return fixed4(abs(i.uv.x), i.uv.y, 0, 1);

                // sample the texture and do alpha clipping.
                fixed4 col = tex2D(_MainTex, i.uv) * _Color;
                clip(col.a - 0.05);

                float2 dir = float2(cos(CutoffAngle + 3.141592653589793238462 * 0.5), sin(CutoffAngle + 3.141592653589793238462 * 0.5));
                float2 a = dir *  10 + 0.5;
                float2 b = dir * -10 + 0.5;

                float2 perp = float2(-dir.y, dir.x);
                a += perp * Distance;
                b += perp * Distance;

                float2 uv = abs(i.uv);
                float side = (uv.x - a.x) * (b.y - a.y) - (uv.y - a.y) * (b.x - a.x);
                clip(Polarity * side);

                return col;
            }
            ENDCG
        }
    }
}
