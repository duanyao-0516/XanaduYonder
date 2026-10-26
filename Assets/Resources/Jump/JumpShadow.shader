Shader "XanaduYonder/Jump Shadow"
{
    Properties
    {
        _Color ("Color", Color) = (0.18, 0.23, 0.25, 0.16)
        _Round ("Round", Range(0, 1)) = 1
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
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 position : SV_POSITION; float2 uv : TEXCOORD0; };
            fixed4 _Color;
            float _Round;
            v2f vert(appdata v)
            {
                v2f o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = abs(i.uv - 0.5) * 2;
                float edge = lerp(max(p.x, p.y), length(p), _Round);
                return fixed4(_Color.rgb, _Color.a * (1 - smoothstep(0.52, 1, edge)));
            }
            ENDCG
        }
    }
}
