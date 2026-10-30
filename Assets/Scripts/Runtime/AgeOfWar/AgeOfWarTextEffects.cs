using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace XanaduYonder.Runtime.AgeOfWar
{
    /// <summary>
    /// Renders the complete text alpha mask before applying the source SWF filter list.
    /// Blur is a separable box filter, repeated passes times, as in the offline SWF renderer.
    /// Source pixels remain at original stage resolution; the host stage supplies display scaling.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class AgeOfWarTextEffects : BaseMeshEffect, IMaterialModifier
    {
        [SerializeField] private AgeOfWarFilter[] filters;
        [SerializeField] private float letterSpacing;
        [SerializeField] private Material originalMaterial;
        private Material material,displayMaterial,modifiedMaterial;
        private Mesh sourceMesh;
        private RenderTexture source,ping,pong,maskA,maskB,result;
        private List<UIVertex> vertices;
        private bool rendering;

        internal static void Initialize(Text text,AgeOfWarTextField field)
        {
            if(text==null)throw new ArgumentNullException("text");
            if(field==null)throw new ArgumentNullException("field");
            float spacing=field.letterSpacing;
            if((field.filters==null||field.filters.Length==0)&&spacing==0)return;
            var effect=text.GetComponent<AgeOfWarTextEffects>();
            if(effect==null)effect=text.gameObject.AddComponent<AgeOfWarTextEffects>();
            effect.filters=field.filters;
            effect.letterSpacing=spacing;
            if(effect.filters!=null&&effect.filters.Length>0)
            {
                effect.EnsureObjects();
                if(effect.originalMaterial==null)effect.originalMaterial=text.material;
                text.material=effect.displayMaterial;
            }
            text.SetVerticesDirty();
        }

        internal static string PlainText(string initial)
        {
            string value=initial??"";
            if(Regex.IsMatch(value,@"<\s*(?:p|font|textformat|br)\b",RegexOptions.IgnoreCase))
            {
                value=Regex.Replace(value,@"</p>\s*<p\b[^>]*>","\n",RegexOptions.IgnoreCase);
                value=Regex.Replace(value,@"<br\s*/?>","\n",RegexOptions.IgnoreCase);
                value=Regex.Replace(value,@"<[^>]*>","");
                value=WebUtility.HtmlDecode(value);
            }
            return value.Replace("\r\n","\n").Replace('\r','\n');
        }

        private void ApplyLetterSpacing(VertexHelper helper)
        {
            if(letterSpacing==0)return;
            var text=graphic as Text;var lines=text==null?null:text.cachedTextGenerator.lines;
            int line=0,start=0,quads=helper.currentVertCount/4;
            for(int glyph=0;glyph<quads;glyph++)
            {
                if(lines!=null)while(line+1<lines.Count&&glyph>=lines[line+1].startCharIdx){line++;start=lines[line].startCharIdx;}
                float shift=(glyph-start)*letterSpacing;
                for(int corner=0;corner<4;corner++)
                {
                    var vertex=new UIVertex();int index=glyph*4+corner;helper.PopulateUIVertex(ref vertex,index);
                    vertex.position.x+=shift;helper.SetUIVertex(vertex,index);
                }
            }
        }

        public override void ModifyMesh(VertexHelper helper)
        {
            if(!IsActive()||rendering)return;
            if(helper.currentVertCount==0)return;
            ApplyLetterSpacing(helper);
            if(filters==null||filters.Length==0)return;
            rendering=true;
            var previous=RenderTexture.active;
            try
            {
                EnsureObjects();
                if(vertices==null)vertices=new List<UIVertex>();
                vertices.Clear();helper.GetUIVertexStream(vertices);
                float left=float.PositiveInfinity,bottom=float.PositiveInfinity,right=float.NegativeInfinity,top=float.NegativeInfinity;
                foreach(var vertex in vertices)
                {
                    left=Mathf.Min(left,vertex.position.x);right=Mathf.Max(right,vertex.position.x);
                    bottom=Mathf.Min(bottom,vertex.position.y);top=Mathf.Max(top,vertex.position.y);
                }
                float padding=2;
                foreach(var filter in filters)
                {
                    Validate(filter);
                    padding+=Mathf.Ceil(Mathf.Max(filter.blurX,filter.blurY)*.5f)*Mathf.Max(0,filter.passes)+Mathf.Abs(filter.distance);
                }
                left=Mathf.Floor(left-padding);bottom=Mathf.Floor(bottom-padding);
                right=Mathf.Ceil(right+padding);top=Mathf.Ceil(top+padding);
                int width=Mathf.Max(1,Mathf.CeilToInt(right-left)),height=Mathf.Max(1,Mathf.CeilToInt(top-bottom));
                EnsureTextures(width,height);
                sourceMesh.Clear();helper.FillMesh(sourceMesh);
                material.SetTexture("_MainTex",graphic.mainTexture);
                material.SetVector("_Bounds",new Vector4(left,bottom,width,height));
                var command=new CommandBuffer {name="Original SWF text filters"};
                try
                {
                    command.SetRenderTarget(source);command.ClearRenderTarget(false,true,Color.clear);
                    command.DrawMesh(sourceMesh,Matrix4x4.identity,material,0,0);
                    Graphics.ExecuteCommandBuffer(command);
                }
                finally {command.Release();}
                RenderTexture current=source;
                foreach(var filter in filters)
                {
                    RenderTexture destination=current==ping?pong:ping;
                    if(filter.type=="COLORMATRIXFILTER")
                    {
                        var values=filter.matrix;
                        for(int row=0;row<4;row++)material.SetVector("_ColorRow"+row,new Vector4(values[row*5],values[row*5+1],values[row*5+2],values[row*5+3]));
                        material.SetVector("_ColorOffset",new Vector4(values[4],values[9],values[14],values[19])/255);
                        Graphics.Blit(current,destination,material,4);
                    }
                    else if(filter.type=="BLURFILTER")
                    {
                        Graphics.Blit(current,maskA);Blur(filter,width,height);Graphics.Blit(maskA,destination);
                    }
                    else
                    {
                        bool inner=filter.type=="GLOWFILTER"?filter.innerGlow:filter.innerShadow;
                        string sourceColor=filter.type=="GLOWFILTER"?filter.glowColor:filter.dropShadowColor;
                        Color color;if(!ColorUtility.TryParseHtmlString(sourceColor,out color))throw new InvalidOperationException("Invalid SWF filter color: "+sourceColor);
                        float distance=filter.type=="GLOWFILTER"?0:filter.distance;
                        material.SetVector("_Offset",new Vector4(Mathf.Cos(filter.angle)*distance/width,-Mathf.Sin(filter.angle)*distance/height,0,0));
                        material.SetFloat("_Inner",inner?1:0);material.SetColor("_EffectColor",color);
                        Graphics.Blit(current,maskA,material,1);Blur(filter,width,height);
                        material.SetTexture("_MaskTex",maskA);material.SetFloat("_Strength",filter.strength);
                        material.SetFloat("_Knockout",filter.knockout?1:0);material.SetFloat("_CompositeSource",filter.compositeSource?1:0);
                        Graphics.Blit(current,destination,material,3);
                    }
                    current=destination;
                }
                // Filtering uses premultiplied alpha. UI/Default expects straight RGB.
                Graphics.Blit(current,result,material,5);
                displayMaterial.SetTexture("_EffectTex",result);
                if(modifiedMaterial!=null)modifiedMaterial.SetTexture("_EffectTex",result);
                helper.Clear();
                helper.AddVert(new Vector3(left,bottom,0),Color.white,new Vector2(0,0));
                helper.AddVert(new Vector3(left,top,0),Color.white,new Vector2(0,1));
                helper.AddVert(new Vector3(right,top,0),Color.white,new Vector2(1,1));
                helper.AddVert(new Vector3(right,bottom,0),Color.white,new Vector2(1,0));
                helper.AddTriangle(0,1,2);helper.AddTriangle(2,3,0);
            }
            finally {RenderTexture.active=previous;rendering=false;}
        }

        private static void Validate(AgeOfWarFilter filter)
        {
            if(filter==null)throw new InvalidOperationException("Missing SWF text filter.");
            if(filter.type!="GLOWFILTER"&&filter.type!="DROPSHADOWFILTER"&&filter.type!="BLURFILTER"&&filter.type!="COLORMATRIXFILTER")
                throw new InvalidOperationException("Unsupported original text filter: "+filter.type);
            if(filter.type=="COLORMATRIXFILTER"&&(filter.matrix==null||filter.matrix.Length!=20))throw new InvalidOperationException("Invalid original text color matrix.");
        }
        private void Blur(AgeOfWarFilter filter,int width,int height)
        {
            if(filter.blurX<=0&&filter.blurY<=0)return;
            // SWF widths are kernel sample counts, not radii. Zero is a one-pixel identity pass.
            int kernelX=Mathf.Max(1,(int)filter.blurX),kernelY=Mathf.Max(1,(int)filter.blurY);
            for(int pass=0;pass<filter.passes;pass++)
            {
                material.SetInt("_Kernel",kernelX);material.SetVector("_Step",new Vector4(1f/width,0,0,0));Graphics.Blit(maskA,maskB,material,2);
                material.SetInt("_Kernel",kernelY);material.SetVector("_Step",new Vector4(0,-1f/height,0,0));Graphics.Blit(maskB,maskA,material,2);
            }
        }
        private void EnsureObjects()
        {
            if(material==null)
            {
                var shader=Resources.Load<Shader>("AgeOfWar/TextEffects");
                if(shader==null||!shader.isSupported)throw new InvalidOperationException("Original text filter shader is unavailable.");
                material=new Material(shader){name="Original SWF text filters",hideFlags=HideFlags.HideAndDontSave};
                var displayShader=Resources.Load<Shader>("AgeOfWar/TextEffectsDisplay");
                if(displayShader==null||!displayShader.isSupported)throw new InvalidOperationException("Original text display shader is unavailable.");
                displayMaterial=new Material(displayShader){name="Original filtered UI text",hideFlags=HideFlags.HideAndDontSave};

            }
            if(sourceMesh==null)sourceMesh=new Mesh{name="Original text mask",hideFlags=HideFlags.HideAndDontSave};
        }
        private void EnsureTextures(int width,int height)
        {
            if(source!=null&&source.width==width&&source.height==height)return;
            ReleaseTextures();
            source=Create(width,height);ping=Create(width,height);pong=Create(width,height);maskA=Create(width,height);maskB=Create(width,height);result=Create(width,height);
        }
        private static RenderTexture Create(int width,int height)
        {
            var texture=new RenderTexture(width,height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear)
            {name="Original text filter pixels",hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            texture.Create();return texture;
        }
        private static void Release(ref RenderTexture texture)
        {if(texture==null)return;texture.Release();UnityEngine.Object.Destroy(texture);texture=null;}
        private void ReleaseTextures()
        {Release(ref source);Release(ref ping);Release(ref pong);Release(ref maskA);Release(ref maskB);Release(ref result);}
        public Material GetModifiedMaterial(Material baseMaterial)
        {
            if(!IsActive()||displayMaterial==null)return baseMaterial;
            modifiedMaterial=baseMaterial;modifiedMaterial.SetTexture("_EffectTex",result);return modifiedMaterial;
        }
        protected override void OnEnable()
        {
            base.OnEnable();
            // Unity snapshot clones copy only serialized configuration. Each instance owns its render resources.
            if(filters!=null&&filters.Length>0)
            {
                EnsureObjects();if(graphic!=null)graphic.material=displayMaterial;
            }
        }
        protected override void OnDisable(){if(graphic!=null&&originalMaterial!=null)graphic.material=originalMaterial;base.OnDisable();}
        protected override void OnDestroy()
        {
            ReleaseTextures();if(material!=null)Destroy(material);if(displayMaterial!=null)Destroy(displayMaterial);if(sourceMesh!=null)Destroy(sourceMesh);
            base.OnDestroy();
        }
    }
}
