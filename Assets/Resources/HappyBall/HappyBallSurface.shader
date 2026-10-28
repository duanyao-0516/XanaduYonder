Shader "XanaduYonder/HappyBall Surface"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _Opacity ("Opacity", Range(0,1)) = 1
        _Shine ("Shine", Range(0,1)) = 0.06
        _Emission ("Emission", Range(0,1)) = 0
        _Ambient ("Ambient", Range(0,0.9)) = 0.54
        _EdgeLift ("Top edge highlight", Range(0,0.1)) = 0
        _ContactShade ("Inner contact shade", Range(0,1)) = 0
        [HideInInspector] _SrcBlend ("Source blend", Float) = 1
        [HideInInspector] _DstBlend ("Destination blend", Float) = 0
        [HideInInspector] _ZWrite ("Depth write", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite [_ZWrite]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; fixed4 color : COLOR; };
            struct v2f
            {
                float4 position : SV_POSITION;
                half3 normal : TEXCOORD0;
                float3 world : TEXCOORD1;
                half radius : TEXCOORD2;
                fixed4 color : COLOR;
            };
            fixed4 _Color;
            half _Opacity, _Shine, _Emission, _ContactShade, _Ambient, _EdgeLift;
            v2f vert(appdata v)
            {
                v2f o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.radius = length(v.vertex.xz);
                o.color = v.color * _Color;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                half3 normal = normalize(i.normal);
                half3 light = normalize(half3(-0.45, 0.8, -0.6));
                half diffuse = saturate(dot(normal, light));
                half shade = _Ambient + (0.93 - _Ambient) * diffuse + 0.07 * saturate(normal.y);
                half3 view = normalize(_WorldSpaceCameraPos.xyz - i.world);
                half specular = pow(saturate(dot(normal, normalize(light + view))), 42) * _Shine;
                half contact = 1 - _ContactShade * (1 - smoothstep(1.0, 1.23, i.radius)) * saturate(normal.y);
                half edge = smoothstep(1.96, 2.0, i.radius) * saturate(normal.y) * _EdgeLift;
                half3 color = i.color.rgb * (shade * contact + _Emission) + specular + edge;
                return fixed4(color, i.color.a * _Opacity);
            }
            ENDCG
        }
    }
}
