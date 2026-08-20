// さいころの面。⭐ **塗らない・透けない・裏を描かない。**
//
// ⚠️ `Sprites/Default` では駄目だった（2026-08-20）。あれは
//   ・両面を描く（Cull Off）
//   ・奥行きを書かない（ZWrite Off）
// ので、立方体に使うと**向こう側の面まで見えて**、4面が重なった絵になる。
//
// ⭐ ここで要るのは3つだけ:
//   ・裏面を描かない（Cull Back）
//   ・奥行きを書く（ZWrite On）── 手前の面が奥の面を隠す
//   ・照らさない（面の明るさは色で外から渡す）── ドット絵に階調を出さない
Shader "EggCommand/DieFace"
{
    Properties
    {
        _MainTex ("面の絵", 2D) = "white" {}
        _Color ("明るさ", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Cull Back
        ZWrite On
        Lighting Off
        Fog { Mode Off }

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
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 art = tex2D(_MainTex, i.uv);
                // ⚠️ 透けさせない。⭐ 面はここで塗り切っている（外は カメラの背景が透明）
                return fixed4(art.rgb * _Color.rgb, 1);
            }
            ENDCG
        }
    }

    Fallback "Unlit/Texture"
}
