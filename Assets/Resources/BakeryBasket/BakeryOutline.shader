Shader "XanaduYonder/Bakery Outline"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+1" }
        Pass
        {
            Cull Front
            ZWrite Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 position : POSITION; float3 normal : NORMAL; };
            float4 vert(appdata v) : SV_POSITION
            {
                float3 world = mul(unity_ObjectToWorld, v.position).xyz;
                world += UnityObjectToWorldNormal(v.normal) * 0.027;
                return mul(UNITY_MATRIX_VP, float4(world, 1));
            }
            fixed4 frag() : SV_Target { return fixed4(1, 0.63, 0.12, 1); }
            ENDCG
        }
    }
}
