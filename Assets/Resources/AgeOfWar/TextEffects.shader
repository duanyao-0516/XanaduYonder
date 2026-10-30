Shader "AgeOfWar/OriginalTextEffects"
{
    Properties
    {
        [PerRendererData] _MainTex ("Text", 2D) = "white" {}
        _EffectTex ("Filtered text", 2D) = "white" {}
        _Output ("UI output", Float) = 0
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
        Cull Off ZWrite Off ZTest Always
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        ColorMask [_ColorMask]
        CGINCLUDE
        #include "UnityCG.cginc"
        #include "UnityUI.cginc"
        sampler2D _MainTex, _MaskTex, _EffectTex;
        float4 _MainTex_TexelSize, _Bounds, _Offset, _Step, _EffectColor;
        float4 _ColorRow0, _ColorRow1, _ColorRow2, _ColorRow3, _ColorOffset;
        float _Inner, _Strength, _Knockout, _CompositeSource, _Output;
        float4 _ClipRect;
        int _Kernel;
        struct text_vertex { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
        struct text_fragment { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; float2 position:TEXCOORD1; };
        text_fragment textVert(text_vertex input)
        {
            text_fragment output;
            if(_Output>0.5)output.vertex=UnityObjectToClipPos(input.vertex);
            else
            {
                output.vertex=float4((input.vertex.xy-_Bounds.xy)/_Bounds.zw*2-1,0,1);
                #if UNITY_UV_STARTS_AT_TOP
                output.vertex.y=-output.vertex.y;
                #endif
            }
            output.uv=input.uv;output.color=input.color;output.position=input.vertex.xy;return output;
        }
        float4 sourceFrag(text_fragment input):SV_Target
        {
            if(_Output>0.5)
            {
                float4 pixel=tex2D(_EffectTex,input.uv)*input.color;
                #ifdef UNITY_UI_CLIP_RECT
                pixel.a*=UnityGet2DClipping(input.position,_ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(pixel.a-0.001);
                #endif
                pixel.rgb*=pixel.a;return pixel;
            }
            float a=tex2D(_MainTex,input.uv).a*input.color.a;
            return float4(input.color.rgb*a,a);
        }
        float4 samplePixels(float2 uv)
        {
            if(uv.x<0||uv.x>1||uv.y<0||uv.y>1)return 0;
            return tex2D(_MainTex,uv);
        }
        float4 maskFrag(v2f_img input):SV_Target
        {
            float a=samplePixels(input.uv-_Offset.xy).a;
            if(_Inner>0.5)a=1-a;
            a*=_EffectColor.a;return float4(_EffectColor.rgb*a,a);
        }
        float4 blurFrag(v2f_img input):SV_Target
        {
            float4 sum=0;
            int halfWidth=_Kernel/2;
            for(int index=0;index<_Kernel;index++)sum+=samplePixels(input.uv+(index-halfWidth)*_Step.xy);
            return sum/max(1,_Kernel);
        }
        float4 compositeFrag(v2f_img input):SV_Target
        {
            float4 src=tex2D(_MainTex,input.uv),mask=tex2D(_MaskTex,input.uv);
            float a=saturate(mask.a*_Strength);
            float3 rgb=mask.a>0?mask.rgb/mask.a:0;
            float4 effect=float4(rgb*a,a);
            if(_Inner>0.5)
            {
                if(_Knockout>0.5||_CompositeSource<0.5)return effect*src.a;
                return effect*src.a+src*(1-effect.a);
            }
            if(_Knockout>0.5)return effect*(1-src.a);
            if(_CompositeSource<0.5)return effect;
            return src+effect*(1-src.a);
        }
        float4 matrixFrag(v2f_img input):SV_Target
        {
            float4 pixel=tex2D(_MainTex,input.uv);
            if(pixel.a>0)pixel.rgb/=pixel.a;
            pixel=saturate(float4(dot(pixel,_ColorRow0),dot(pixel,_ColorRow1),dot(pixel,_ColorRow2),dot(pixel,_ColorRow3))+_ColorOffset);
            pixel.rgb*=pixel.a;return pixel;
        }
        float4 straightFrag(v2f_img input):SV_Target
        {
            float4 pixel=tex2D(_MainTex,input.uv);
            if(pixel.a>0)pixel.rgb/=pixel.a;
            return pixel;
        }
        ENDCG
        Pass
        {
            Blend One OneMinusSrcAlpha
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex textVert
            #pragma fragment sourceFrag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment maskFrag
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment blurFrag
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment compositeFrag
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment matrixFrag
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment straightFrag
            ENDCG
        }
    }
}
