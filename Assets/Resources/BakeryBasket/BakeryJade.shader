Shader "XanaduYonder/Bakery Jade"
{
    Properties
    {
        _Selected ("Selected", Range(0,1)) = 0
        [HideInInspector] _BakeryShadowMap ("Shadow depth", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            ZWrite On
            ZTest LEqual
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "BakeryLight.cginc"

            struct appdata
            {
                float4 position : POSITION;
                half3 normal : NORMAL;
                half4 paint : COLOR;
                half2 optics : TEXCOORD1;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                half3 normal : TEXCOORD0;
                half3 view : TEXCOORD1;
                half2 optics : TEXCOORD2;
                float3 shadowPosition : TEXCOORD3;
                half4 paint : COLOR;
            };

            half _Selected;

            v2f vert(appdata v)
            {
                v2f o;
                float3 world = mul(unity_ObjectToWorld, v.position).xyz;
                o.position = UnityObjectToClipPos(v.position);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.view = lerp(_WorldSpaceCameraPos - world, UNITY_MATRIX_V[2].xyz, unity_OrthoParams.w);
                o.optics = v.optics;
                o.shadowPosition = BakeryShadowPosition(world);
                o.paint = v.paint;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                const half3 fill = half3(.76, .35, .48);
                const half3 rimLight = half3(.63, .54, .56);
                half3 n = normalize(i.normal);
                half3 view = normalize(i.view);
                half gloss = i.paint.a;
                half metal = i.optics.y;
                half transmission = i.optics.x * (1 - metal);
                half facing = saturate(dot(n, view));
                half grazing = 1 - facing;
                half rim = grazing * grazing;

                half3 tint = i.paint.rgb;
                half peak = max(tint.r, max(tint.g, tint.b));
                half3 hue = tint / max(peak, .20);
                half3 filtered = hue * hue;
                half3 body = lerp(tint, tint * hue, transmission * .67);
                half exposure = saturate(dot(n, _BakeryKey));
                half visibility = BakeryVisibility(i.shadowPosition, exposure);
                half light = .45 + .49 * exposure * visibility + .15 * saturate(dot(n, fill));
                half3 surface = body * light * (1 - metal * .25);

                // Color in the thin grazing region comes from the stone's pigment.
                half incoming = .56 + .44 * saturate(dot(n, rimLight) * .5 + .5);
                half through = transmission * incoming * (.11 + rim * (.61 + .27 * rim));
                half3 translucent = filtered * (.41 + .52 * peak);
                surface = lerp(surface, translucent, through);

                half keyFacing = saturate(dot(n, normalize(_BakeryKey + view)));
                half keySpec = pow(keyFacing, 12 + 66 * gloss) + .16 * pow(keyFacing, 7 + 15 * gloss);
                half fillSpec = smoothstep(.949, .997, dot(n, normalize(fill + view)));
                half reflection = gloss * (.69 * keySpec * visibility + .15 * fillSpec * gloss + .075 * rim);
                half3 reflectionTint = lerp(half3(1, .985, .955), hue, metal);
                half metalReflection = metal * (.10 + .19 * saturate(n.y) + .16 * rim);
                half3 color = saturate(surface + reflection * reflectionTint + metalReflection * hue);
                return half4(lerp(color, half3(1, .65, .18), _Selected * .44), 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
