Shader "AgeOfWar/FilteredTextDisplay"
{
    Properties
    {
        [PerRendererData] _MainTex ("Font texture", 2D) = "white" {}
        _EffectTex ("Filtered text", 2D) = "white" {}
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]
        // CanvasRenderer executes every material pass. Only presentation belongs here;
        // the six offscreen filter passes live in TextEffects.shader.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; float2 position:TEXCOORD1; };
            sampler2D _EffectTex;
            float4 _ClipRect;
            v2f vert(appdata input)
            {v2f output;output.vertex=UnityObjectToClipPos(input.vertex);output.position=input.vertex.xy;output.uv=input.uv;output.color=input.color;return output;}
            fixed4 frag(v2f input):SV_Target
            {
                fixed4 pixel=tex2D(_EffectTex,input.uv)*input.color;
                #ifdef UNITY_UI_CLIP_RECT
                pixel.a*=UnityGet2DClipping(input.position,_ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(pixel.a-.001);
                #endif
                pixel.rgb*=pixel.a;return pixel;
            }
            ENDCG
        }
    }
}
