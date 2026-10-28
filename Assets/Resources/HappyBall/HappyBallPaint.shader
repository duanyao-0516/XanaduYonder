Shader "XanaduYonder/HappyBall Paint"
{
    Properties
    {
        _Color ("Paint", Color) = (0.48,0.94,0.06,1)
        _Opacity ("Opacity", Range(0,1)) = 1
        _Seed ("Shape variation", Float) = 0
    }
    SubShader
    {
        // Detached plate halves fade at queue 3000 without writing depth. Their
        // attached paint must draw afterwards, while opaque floor/ball depth still occludes it.
        Tags { "RenderType"="Transparent" "Queue"="Transparent+5" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back
        Offset -1, -1
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 position : SV_POSITION; float2 uv : TEXCOORD0; };
            fixed4 _Color;
            half _Opacity, _Seed;
            v2f vert(appdata v)
            {
                v2f o; o.position = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = (i.uv - 0.5) * 2;
                float a = atan2(p.y, p.x);
                float r = length(p);
                float edge = .54 + .045 * sin(a * 7 + _Seed) + .035 * sin(a * 13 - _Seed * 2);
                edge += .20 * pow(saturate(sin(a * 11 + _Seed * 3)), 14);
                half coverage = 1 - smoothstep(edge - .02, edge + .01, r);
                // Satellite droplets are part of the same clipped surface mesh.
                for (int n = 0; n < 9; n++)
                {
                    float t = n * 2.39996 + _Seed;
                    float2 center = float2(cos(t), sin(t)) * (.74 + .13 * sin(n * 7.13 + _Seed));
                    float radius = .025 + .024 * (sin(n * 8.7 + _Seed) * .5 + .5);
                    coverage = max(coverage, 1 - smoothstep(radius, radius + .012, length(p - center)));
                }
                clip(coverage - .01);
                return fixed4(_Color.rgb, _Color.a * _Opacity * coverage);
            }
            ENDCG
        }
    }
}
