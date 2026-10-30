using System;
using UnityEngine;
using UnityEngine.UI;

namespace XanaduYonder.Runtime.AgeOfWar
{
    /// <summary>Uses the embedded SWF font layout, before the text filter renders its mask.</summary>
    [DisallowMultipleComponent]
    internal sealed class AgeOfWarTextLayout : BaseMeshEffect
    {
        [SerializeField] private float ascent;
        [SerializeField] private float baselinePitch;

        internal static void Initialize(Text text,AgeOfWarAssets assets,AgeOfWarTextField field)
        {
            var font=assets.GetFontDefinition(field.font);
            // Some original fonts omit DefineFont layout. Do not invent metrics from their export.
            if(font==null||!font.hasLayout||font.unitsPerEm<=0)return;
            var effect=text.gameObject.AddComponent<AgeOfWarTextLayout>();
            effect.ascent=Twips(field.size*font.ascent/font.unitsPerEm);
            effect.baselinePitch=effect.ascent+Twips(field.size*font.descent/font.unitsPerEm)+field.leading;
            text.SetVerticesDirty();
        }

        // Original AVM2 getLineMetrics truncates scaled ascent/descent to signed twips.
        private static float Twips(float value){return (float)(Math.Truncate(value*20)/20);}

        public override void ModifyMesh(VertexHelper helper)
        {
            if(!IsActive()||helper.currentVertCount==0)return;
            var text=graphic as Text;if(text==null||text.font==null)return;
            var lines=text.cachedTextGenerator.lines;if(lines.Count==0)return;
            int quads=helper.currentVertCount/4,line=0;
            var characterIndices=new int[quads];int nextCharacter=0;
            float unitsPerPixel=1/text.pixelsPerUnit;
            float nativeAscent=0;bool measured=false;
            // The exported TTF can have different vertical metrics. Measure its actual generated
            // baseline from a glyph instead of assuming Font.lineHeight is the SWF baseline pitch.
            for(int glyph=0;glyph<quads;glyph++)
            {
                characterIndices[glyph]=-1;
                float top=float.NegativeInfinity,bottom=float.PositiveInfinity;
                for(int corner=0;corner<4;corner++)
                {
                    var vertex=new UIVertex();helper.PopulateUIVertex(ref vertex,glyph*4+corner);
                    top=Mathf.Max(top,vertex.position.y);bottom=Mathf.Min(bottom,vertex.position.y);
                }
                if(top<=bottom)continue;
                // UILineInfo indexes the input string. TextGenerator emits no visible quad for
                // newlines, spaces, or other blank glyphs. Keep those two indices independent;
                // automatic wrapping still uses the generator's original string boundaries.
                while(nextCharacter<text.text.Length)
                {
                    int index=nextCharacter++;char character=text.text[index];CharacterInfo info;
                    if(char.IsWhiteSpace(character)||char.IsControl(character)||
                        !text.font.GetCharacterInfo(character,out info,text.fontSize,text.fontStyle)||
                        info.maxX<=info.minX||info.maxY<=info.minY)continue;
                    characterIndices[glyph]=index;
                    while(line+1<lines.Count&&index>=lines[line+1].startCharIdx)line++;
                    if(!measured)
                    {
                        nativeAscent=lines[line].topY*unitsPerPixel-(top-info.maxY*unitsPerPixel);
                        measured=true;
                    }
                    break;
                }
            }
            if(!measured)return;
            line=0;
            for(int glyph=0;glyph<quads;glyph++)
            {
                int character=characterIndices[glyph];if(character<0)continue;
                while(line+1<lines.Count&&character>=lines[line+1].startCharIdx)line++;
                float sourceBaseline=lines[0].topY*unitsPerPixel-ascent-line*baselinePitch;
                float nativeBaseline=lines[line].topY*unitsPerPixel-nativeAscent;
                float shift=sourceBaseline-nativeBaseline;
                for(int corner=0;corner<4;corner++)
                {
                    var vertex=new UIVertex();int index=glyph*4+corner;helper.PopulateUIVertex(ref vertex,index);
                    vertex.position.y+=shift;helper.SetUIVertex(vertex,index);
                }
            }
        }
    }
}
