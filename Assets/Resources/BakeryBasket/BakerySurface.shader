Shader "XanaduYonder/Bakery Surface"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Selected ("Selected", Range(0,1)) = 0
        _Cavity ("Cavity shading", Range(0,1)) = 0
        _Gloss ("Gloss", Range(0,1)) = 0.1
        _VertexPaint ("Vertex paint", Float) = 0
        _Grain ("Crumb grain", Range(0,1)) = 0
        _FillLight ("Soft fill light", Range(0,1)) = 0
        _Sprinkles ("Sprinkles", Float) = 0
        [HideInInspector] _BakeryShadowMap ("Shadow depth", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "BakeryLight.cginc"
            struct appdata {
                float4 position : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                fixed4 paint : COLOR;
            };
            struct v2f {
                float4 position : SV_POSITION;
                half3 normal : TEXCOORD0;
                half3 view : TEXCOORD1;
                float3 local : TEXCOORD2;
                float2 uv : TEXCOORD3;
                half shadow : TEXCOORD4;
                float3 shadowPosition : TEXCOORD5;
                fixed4 paint : COLOR;
            };
            fixed4 _Color;
            half _Selected, _Cavity, _Gloss, _VertexPaint, _Grain, _Sprinkles, _FillLight;
            v2f vert(appdata v)
            {
                v2f o;
                o.position = UnityObjectToClipPos(v.position);
                o.normal = UnityObjectToWorldNormal(v.normal);
                float3 world = mul(unity_ObjectToWorld, v.position).xyz;
                o.view = lerp(_WorldSpaceCameraPos - world, UNITY_MATRIX_V[2].xyz, unity_OrthoParams.w);
                o.local = v.position.xyz;
                o.uv = v.uv;
                float height = world.y + 300;
                o.shadow = 1 - _Cavity * (1 - saturate(height / 1.35));
                o.shadowPosition = BakeryShadowPosition(world);
                o.paint = lerp(fixed4(_Color.rgb,_Gloss),v.paint,_VertexPaint);
                return o;
            }
            float Noise(float3 value)
            {
                value = frac(value * float3(.1031,.1030,.0973));
                value += dot(value,value.yxz + 33.33);
                return frac((value.x + value.y) * value.z);
            }
            fixed4 frag(v2f i) : SV_Target
            {
                half3 n = normalize(i.normal);
                half3 view = normalize(i.view);
                const half3 fill = half3(.69,.43,.49);
                half exposure = saturate(dot(n, _BakeryKey));
                half visibility = BakeryVisibility(i.shadowPosition, exposure);
                half ambient = .56 + .135 * saturate(dot(n, fill)) + _FillLight;
                half3 light = ambient * half3(1,.985,.96) + (.49 - _FillLight) * exposure * visibility * half3(1,.985,.95);
                half gloss = i.paint.a;
                half keyAngle = saturate(dot(n, normalize(_BakeryKey + view)));
                half keyReflection = pow(keyAngle, 9 + 14 * gloss) * .12 + pow(keyAngle, 22 + 36 * gloss) * .41;
                half fillReflection = smoothstep(.94, .995, dot(n, normalize(fill + view))) * gloss * .13;
                half rim = 1 - saturate(dot(n, view));
                half hot = gloss * (keyReflection * visibility + fillReflection + rim * rim * .075);
                half3 color = i.paint.rgb;
                if (_Grain > 0)
                {
                    half coarse = Noise(floor(i.local * 28));
                    half fine = Noise(floor(i.local * 151));
                    color *= 1 + (coarse * .45 + fine * .55 - .49) * _Grain * (1 - gloss * .55);
                }
                if (_Sprinkles > .5 && gloss > .55 && i.uv.y > .06 && i.uv.y < .44)
                {
                    float2 card = i.uv * float2(41,15);
                    float2 cell = floor(card);
                    half roll = Noise(float3(cell,2));
                    float2 p = frac(card) - .5;
                    float2 axis = float2(cos(roll*15),sin(roll*15));
                    float along = dot(p,axis)*3;
                    float across = dot(p,float2(-axis.y,axis.x))*11;
                    float stripe = along*along + across*across;
                    half sugar = (1 - smoothstep(.69,1.12,stripe)) * step(.38,roll);
                    half3 candy = roll > .77 ? half3(1,.91,.48) : (roll > .58 ? half3(1,.99,.82) : half3(.42,.76,.72));
                    color = lerp(color,candy,sugar);
                }
                color = lerp(color,fixed3(1,.65,.18),_Selected * .44);
                return fixed4(color * light * i.shadow + half3(1,.985,.95) * hot, 1);
            }
            ENDCG
        }
    }
}
