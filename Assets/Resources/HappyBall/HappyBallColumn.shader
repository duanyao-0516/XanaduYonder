Shader "XanaduYonder/HappyBall Column"
{
    Properties
    {
        _Color ("Ivory", Color) = (1,1,1,1)
        _Shine ("Shine", Range(0,1)) = 0.045
        _Emission ("Emission", Range(0,1)) = 0.1
        _Ambient ("Ambient", Range(0,0.9)) = 0.76
        _ShadowColor ("Ball shadow tint", Color) = (0.11,0.13,0.10,1)
        // X: local center height; Y: inverse local half-height; Z: center angle; W: opacity.
        _BallShadow ("Ball shadow in column coordinates", Vector) = (0,1,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        ZWrite On
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
                float3 local : TEXCOORD2;
                fixed4 color : COLOR;
            };
            fixed4 _Color, _ShadowColor;
            half _Shine, _Emission, _Ambient;
            float4 _BallShadow;
            v2f vert(appdata v)
            {
                v2f o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.local = v.vertex.xyz;
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
                half3 color = i.color.rgb * (shade + _Emission) + specular;

                // The soft ellipse shades this opaque surface itself. There is no second
                // near-coplanar mesh, transparent sort, depth bias, or world-Y subtraction.
                // Keep the local interpolation and ellipse arithmetic full precision on mobile.
                float angle = atan2(i.local.x, -i.local.z);
                float2 offset = float2((angle - _BallShadow.z) / .34,
                    (i.local.y - _BallShadow.x) * _BallShadow.y);
                float coverage = 1 - smoothstep(.38, 1, length(offset));
                color = lerp(color, _ShadowColor.rgb, coverage * _BallShadow.w);
                return fixed4(color, 1);
            }
            ENDCG
        }
    }
}
