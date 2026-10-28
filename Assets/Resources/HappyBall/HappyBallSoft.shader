Shader "XanaduYonder/HappyBall Soft"
{
    Properties
    {
        _Color ("Color", Color) = (0.16,0.18,0.15,0.25)
        _Opacity ("Opacity", Range(0,1)) = 1
        _Ring ("Ring", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 position : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            fixed4 _Color;
            half _Opacity, _Ring;
            v2f vert(appdata v)
            {
                v2f o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                half r = length((i.uv - 0.5) * 2);
                half edge = 1 - smoothstep(0.38, 1, r);
                half ring = smoothstep(0.55, 0.72, r) * (1 - smoothstep(0.76, 0.98, r));
                return fixed4(_Color.rgb * i.color.rgb, _Color.a * i.color.a * _Opacity * lerp(edge, ring, _Ring));
            }
            ENDCG
        }
    }
}
