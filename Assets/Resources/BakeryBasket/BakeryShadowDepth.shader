Shader "Hidden/XanaduYonder/Bakery Shadow Depth"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct v2f
            {
                float4 position : SV_POSITION;
                float depth : TEXCOORD0;
            };

            v2f vert(float4 vertex : POSITION)
            {
                v2f o;
                o.position = UnityObjectToClipPos(vertex);
                o.depth = (-UnityObjectToViewPos(vertex).z - _ProjectionParams.y) /
                    (_ProjectionParams.z - _ProjectionParams.y);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                return float4(EncodeFloatRG(min(i.depth, .99998)), 0, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
