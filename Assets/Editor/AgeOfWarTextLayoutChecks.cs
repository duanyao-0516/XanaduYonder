using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Runtime.AgeOfWar;
using Object=UnityEngine.Object;
namespace XanaduYonder.EditorTools
{
    internal static class AgeOfWarTextLayoutChecks
    {
        private static List<float> GlyphTops(Mesh mesh)
        {
            var result=new List<float>();var vertices=mesh.vertices;
            for(int i=0;i+3<vertices.Length;i+=4)
            {
                float top=Mathf.Max(Mathf.Max(vertices[i].y,vertices[i+1].y),Mathf.Max(vertices[i+2].y,vertices[i+3].y));
                float bottom=Mathf.Min(Mathf.Min(vertices[i].y,vertices[i+1].y),Mathf.Min(vertices[i+2].y,vertices[i+3].y));
                if(top>bottom)result.Add(top);
            }
            return result;
        }
        internal static int Run()
        {
            using(var assets=AgeOfWarAssets.Load(2))
            {
                var owner=new GameObject("Original multiline metrics checks",typeof(RectTransform),typeof(Canvas));
                Mesh mesh=null;
                try
                {
                    owner.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
                    var field=new AgeOfWarTextField{name="Source font baseline probe",variable="",initial="M\nM\nM",font="font/windows/2",color="#ffffff",size=8,bold=true,w=200,h=100,wordWrap=true,multiline=true,leading=2};
                    var text=AgeOfWarDrawing.Text(owner.transform,assets,field);Canvas.ForceUpdateCanvases();mesh=text.canvasRenderer.GetMesh();
                    var vertices=mesh.vertices;var tops=new List<float>();
                    for(int i=0;i+3<vertices.Length;i+=4)
                    {
                        float top=Mathf.Max(Mathf.Max(vertices[i].y,vertices[i+1].y),Mathf.Max(vertices[i+2].y,vertices[i+3].y));
                        float bottom=Mathf.Min(Mathf.Min(vertices[i].y,vertices[i+1].y),Mathf.Min(vertices[i+2].y,vertices[i+3].y));
                        if(top>bottom)tops.Add(top);
                    }
                    var report=new StringBuilder();report.AppendLine("Source windows/2 ascent1408 descent256 em1024 size8 leading2 => baseline pitch15px");
                    report.AppendLine("Unity font size="+text.font.fontSize+" ascent="+text.font.ascent+" lineHeight="+text.font.lineHeight+" pixelsPerUnit="+text.pixelsPerUnit);
                    CharacterInfo info;text.font.GetCharacterInfo('M',out info,8,FontStyle.Bold);report.AppendLine("M glyph minY="+info.minY+" maxY="+info.maxY);
                    for(int i=0;i<text.cachedTextGenerator.lines.Count;i++){var line=text.cachedTextGenerator.lines[i];report.AppendLine("line "+i+" top="+line.topY+" height="+line.height+" char="+line.startCharIdx);}
                    for(int i=0;i<text.cachedTextGenerator.characters.Count;i++)report.AppendLine("char "+i+" cursor="+text.cachedTextGenerator.characters[i].cursorPos);
                    report.AppendLine("Rendered glyph tops="+string.Join(",",tops));
                    var folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../artifacts/validation/age-of-war-ui"));Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"text-layout-metrics.txt"),report.ToString());
                    if(tops.Count!=3)throw new InvalidOperationException("Expected three real glyph quads, got "+tops.Count);
                    if(Math.Abs(tops[0]+4)>.001)throw new InvalidOperationException("Original first baseline must be11px below the text inset; glyph top: "+tops[0]);
                    if(Math.Abs(tops[0]-tops[1]-15)>.001||Math.Abs(tops[1]-tops[2]-15)>.001)throw new InvalidOperationException("Original multiline baseline pitch must be15px; actual tops: "+string.Join(",",tops));
                    text.text="M\n\nM";Canvas.ForceUpdateCanvases();Object.DestroyImmediate(mesh);mesh=text.canvasRenderer.GetMesh();
                    var blankTops=GlyphTops(mesh);if(blankTops.Count!=2||Math.Abs(blankTops[0]-blankTops[1]-30)>.001)throw new InvalidOperationException("Original empty line must retain its15px baseline interval");
                    var clone=Object.Instantiate(owner);try{Canvas.ForceUpdateCanvases();var cloneText=clone.GetComponentInChildren<Text>();var cloneMesh=cloneText.canvasRenderer.GetMesh();try{var cloneTops=GlyphTops(cloneMesh);if(cloneTops.Count!=2||Math.Abs(cloneTops[0]+4)>.001||Math.Abs(cloneTops[0]-cloneTops[1]-30)>.001)throw new InvalidOperationException("Snapshot clone must retain original font layout metrics");}finally{Object.DestroyImmediate(cloneMesh);}}finally{Object.DestroyImmediate(clone);}
                    text.text=" M M\n M";Canvas.ForceUpdateCanvases();Object.DestroyImmediate(mesh);mesh=text.canvasRenderer.GetMesh();
                    var spaced=GlyphTops(mesh);if(spaced.Count!=3||Math.Abs(spaced[0]+4)>.001||Math.Abs(spaced[0]-spaced[1])>.001||Math.Abs(spaced[1]-spaced[2]-15)>.001)throw new InvalidOperationException("Spaces must retain character indices without consuming visible glyph quads: "+string.Join(",",spaced));
                    text.rectTransform.sizeDelta=new Vector2(20,100);text.text="MM MM MM";Canvas.ForceUpdateCanvases();Object.DestroyImmediate(mesh);mesh=text.canvasRenderer.GetMesh();
                    var wrapped=GlyphTops(mesh);if(text.cachedTextGenerator.lines.Count!=3||wrapped.Count!=6)throw new InvalidOperationException("Real wrapping fixture must produce three lines with two glyphs each");
                    for(int i=0;i<wrapped.Count;i++)if(Math.Abs(wrapped[i]-(-4-(i/2)*15))>.001)throw new InvalidOperationException("Automatic wrap must map original character indices to emitted glyph quads: "+string.Join(",",wrapped));
                    var damageField=new AgeOfWarTextField{name="Source damage font baseline probe",variable="",initial="M\nM",font="font/particules/29",color="#ffffff",size=14,w=200,h=100,wordWrap=true,multiline=true,leading=2};
                    var damageText=AgeOfWarDrawing.Text(owner.transform,assets,damageField);Canvas.ForceUpdateCanvases();
                    var damageMesh=damageText.canvasRenderer.GetMesh();try{var damageTops=GlyphTops(damageMesh);if(damageTops.Count!=2||Math.Abs(damageTops[0]-damageTops[1]-15.95)>.001)throw new InvalidOperationException("Original AVM2 damage font ascent10.75+descent3.2+leading2 must produce15.95px pitch; actual tops: "+string.Join(",",damageTops));}finally{Object.DestroyImmediate(damageMesh);}
                    return 8;
                }
                finally{Object.DestroyImmediate(owner);if(mesh!=null)Object.DestroyImmediate(mesh);}
            }
        }
    }
}
