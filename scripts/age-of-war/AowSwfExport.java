import java.io.*;
import java.nio.file.*;
import java.util.*;
import javax.imageio.ImageIO;
import com.eclipsesource.json.*;
import com.jpexs.decompiler.flash.*;
import com.jpexs.decompiler.flash.tags.*;
import com.jpexs.decompiler.flash.tags.base.*;
import com.jpexs.decompiler.flash.timeline.*;
import com.jpexs.decompiler.flash.types.*;
import com.jpexs.decompiler.flash.exporters.commonshape.Matrix;
import com.jpexs.decompiler.flash.exporters.SoundExporter;
import com.jpexs.decompiler.flash.exporters.modes.SoundExportMode;

/** Offline-only original SWF timeline reader/rasterizer. Never included in the game. */
public class AowSwfExport {
 static JsonArray rect(RECT r){return new JsonArray().add(r.Xmin/20.0).add(r.Ymin/20.0).add(r.Xmax/20.0).add(r.Ymax/20.0);}
 static double letterSpacing(String html){if(html==null)return 0;var match=java.util.regex.Pattern.compile("letterSpacing\\s*=\\s*[\"']([-+0-9.eE]+)[\"']",java.util.regex.Pattern.CASE_INSENSITIVE).matcher(html);return match.find()?Double.parseDouble(match.group(1)):0;}
 static JsonArray matrix(MATRIX m){Matrix x=m==null?new Matrix():new Matrix(m);return new JsonArray().add(x.scaleX).add(x.rotateSkew0).add(x.rotateSkew1).add(x.scaleY).add(x.translateX/20).add(x.translateY/20);}
 static JsonObject timeline(Timeline t){
  JsonArray frames=new JsonArray();
  for(Frame f:t.getFrames()){
   JsonArray layers=new JsonArray(), labels=new JsonArray(), sounds=new JsonArray();
   for(String l:f.labels)labels.add(l);for(int s:f.sounds)sounds.add(s);
   for(DepthState d:f.layers.values()){
    JsonObject layer=new JsonObject().add("id",d.characterId).add("depth",d.depth).add("name",d.instanceName==null?"":d.instanceName).add("time",d.time).add("visible",d.isVisible).add("matrix",matrix(d.matrix)).add("clipDepth",d.clipDepth).add("ratio",d.ratio);
    ColorTransform c=d.colorTransForm;
    if(d.filters!=null && !d.filters.isEmpty()){JsonArray fs=new JsonArray();for(var filter:d.filters){JsonObject fo=new JsonObject().add("type",filter.getClass().getSimpleName());for(var field:filter.getClass().getFields())try{Object val=field.get(filter);if(val instanceof float[] vv){JsonArray aa=new JsonArray();for(float v:vv)aa.add(v);fo.add(field.getName(),aa);}else if(val instanceof Number n)fo.add(field.getName(),n.doubleValue());else if(val instanceof Boolean b)fo.add(field.getName(),b);else if(val instanceof RGBA color)fo.add(field.getName(),String.format("#%02x%02x%02x%02x",color.red,color.green,color.blue,color.alpha));}catch(Exception ignored){}fs.add(fo);}layer.add("filters",fs);}
    if(c!=null) layer.add("color",new JsonArray().add(c.getRedMulti()).add(c.getGreenMulti()).add(c.getBlueMulti()).add(c.getAlphaMulti()).add(c.getRedAdd()).add(c.getGreenAdd()).add(c.getBlueAdd()).add(c.getAlphaAdd()));
    layers.add(layer);
   }
   frames.add(new JsonObject().add("frame",f.frame+1).add("labels",labels).add("layers",layers).add("sounds",sounds).add("actionCount",f.actions.size()));
  }
  return new JsonObject().add("frames",frames);
 }
 static JsonObject metadata(SWF swf){
  JsonArray chars=new JsonArray();
  for(CharacterTag c: new TreeMap<Integer,CharacterTag>(swf.getCharacters(false)).values()){
   JsonArray names=new JsonArray();for(String n:c.getClassNames())names.add(n);
   JsonObject o=new JsonObject().add("id",c.getCharacterId()).add("type",c.getTagName()).add("export",c.getExportName()==null?"":c.getExportName()).add("classes",names);
   if(c instanceof BoundedTag b)o.add("bounds",rect(b.getRect()));
   if(c instanceof FontTag ft)o.add("font",new JsonObject().add("family",ft.getFontNameIntag()).add("glyphs",ft.getCharacterCount()).add("bold",ft.isBold()).add("italic",ft.isItalic()).add("hasLayout",ft.hasLayout()).add("ascent",ft.hasLayout()?ft.getAscent()/ft.getDivider():0).add("descent",ft.hasLayout()?ft.getDescent()/ft.getDivider():0).add("leading",ft.hasLayout()?ft.getLeading()/ft.getDivider():0).add("unitsPerEm",1024).add("rawAscent",ft.getAscent()).add("rawDescent",ft.getDescent()).add("rawLeading",ft.getLeading()).add("divider",ft.getDivider()));
   if(c instanceof DefineEditTextTag e)o.add("text",new JsonObject().add("variable",e.variableName==null?"":e.variableName).add("initial",e.initialText==null?"":e.initialText).add("fontId",e.fontId).add("size",e.fontHeight/20.0).add("align",e.align).add("color",e.textColor==null?"#000000":String.format("#%02x%02x%02x",e.textColor.red,e.textColor.green,e.textColor.blue)).add("outlined",e.useOutlines).add("letterSpacing",letterSpacing(e.initialText)).add("wordWrap",e.wordWrap).add("multiline",e.multiline).add("leftMargin",e.leftMargin/20.0).add("rightMargin",e.rightMargin/20.0).add("indent",e.indent/20.0).add("leading",e.leading/20.0));
   if(c instanceof DefineSpriteTag s)o.add("timeline",timeline(s.getTimeline()));
   if(c instanceof ButtonTag b)o.add("timeline",timeline(b.getTimeline()));
   chars.add(o);
  }
  return new JsonObject().add("fps",swf.frameRate).add("stage",rect(swf.displayRect)).add("root",timeline(swf.getTimeline())).add("characters",chars);
 }
 static void hideMarkers(SWF s){
  // These authored helpers are explicitly hidden by Unit/Turret at runtime.
  for(CharacterTag c:s.getCharacters(false).values())if(c instanceof DefineSpriteTag spr)
   for(Frame f:spr.getTimeline().getFrames()) f.layers.values().removeIf(d->((d.characterId==244||(d.characterId==589&&spr.spriteId!=999))&&!s.isAS3()) || (d.instanceName!=null && (d.instanceName.equals("delayTimer")||d.instanceName.equals("hitzone")||d.instanceName.equals("bp")||d.instanceName.equals("bp2")||d.instanceName.equals("btnSell")||d.instanceName.equals("bulletPoint")||d.instanceName.startsWith("bulletPoint"))));
 }
 static int stream(Iterable<Tag> tags,Path path,int count)throws Exception {
  for(Tag tag:tags){
   if(tag instanceof SoundStreamHeadTypeTag head)for(var range:head.getRanges()){
    long bytes=0;for(var block:range.getRawSoundData())bytes+=block.getLength();if(bytes==0)continue;
    if(count++>0)throw new IllegalStateException("Unexpected additional sponsor stream");
    try(var out=Files.newOutputStream(path)){new SoundExporter().exportSound(out,range,SoundExportMode.WAV);}
   }
   if(tag instanceof DefineSpriteTag sprite){sprite.getTimeline();count=stream(sprite.getTags(),path,count);}
  }
  return count;
 }
 public static void main(String[] a)throws Exception {
  SWF swf=new SWF(Files.newInputStream(Path.of(a[1])),false);
  if(a[0].equals("stream")){swf.getTimeline();if(stream(swf.getTags(),Path.of(a[2]),0)!=1)throw new IllegalStateException("Missing sponsor stream");return;}
  if(a[0].equals("inspect")){Files.writeString(Path.of(a[2]),metadata(swf).toString());return;}
  JsonArray jobs=Json.parse(Files.readString(Path.of(a[2]))).asArray();Path out=Path.of(a[3]);Files.createDirectories(out);hideMarkers(swf);
  for(JsonValue value:jobs){
   JsonObject j=value.asObject();int id=j.getInt("id",0); CharacterTag c=id==0?null:swf.getCharacter(id);
   Timeline tl=id==0?swf.getTimeline(): c instanceof DefineSpriteTag spr?spr.getTimeline():c instanceof ButtonTag b?b.getTimeline():null;
   Map<Frame,TreeMap<Integer,DepthState>> maskedFrames=new LinkedHashMap<>();
   Map<Timeline,Integer> stoppedTimelines=new LinkedHashMap<>();
   if(j.get("stoppedChildren")!=null)for(JsonValue sv:j.get("stoppedChildren").asArray()){
    JsonObject stop=sv.asObject();Timeline child=((DefineSpriteTag)swf.getCharacter(stop.getInt("id",0))).getTimeline();
    int count=child.getFrameCount(),last=stop.getInt("frame",count)-1;stoppedTimelines.put(child,count);
    int horizon=1;for(JsonValue fv:j.get("frames").asArray())horizon=Math.max(horizon,fv.asInt()+count);
    while(child.getFrameCount()<horizon)child.getFrames().add(new Frame(child.getFrame(last),child.getFrameCount()));
   }
   if(j.get("depthMask")!=null){JsonObject mask=j.get("depthMask").asObject();Timeline masked=((DefineSpriteTag)swf.getCharacter(mask.getInt("id",0))).getTimeline();for(Frame f:masked.getFrames()){maskedFrames.put(f,f.layers);f.layers=new TreeMap<>(f.layers);f.layers.entrySet().removeIf(entry->entry.getKey()<mask.getInt("min",0)||entry.getKey()>mask.getInt("max",65535));}}
   boolean composite=j.get("layers")!=null;
   if(composite || tl==null){
    DefineSpriteTag temp=new DefineSpriteTag(swf);int depth=1;
    JsonArray layers=composite?j.get("layers").asArray():new JsonArray().add(new JsonObject().add("id",id));
    for(JsonValue lv:layers){JsonObject layer=lv.asObject();Matrix m=new Matrix();
     if(layer.get("matrix")!=null){JsonArray v=layer.get("matrix").asArray();m=new Matrix(v.get(0).asDouble(),v.get(3).asDouble(),v.get(1).asDouble(),v.get(2).asDouble(),v.get(4).asDouble()*20,v.get(5).asDouble()*20);}
     CXFORMWITHALPHA color=null;
     if(layer.get("color")!=null){JsonArray v=layer.get("color").asArray();color=new CXFORMWITHALPHA();color.hasAddTerms=true;color.hasMultTerms=true;color.redMultTerm=v.get(0).asInt();color.greenMultTerm=v.get(1).asInt();color.blueMultTerm=v.get(2).asInt();color.alphaMultTerm=v.get(3).asInt();color.redAddTerm=v.get(4).asInt();color.greenAddTerm=v.get(5).asInt();color.blueAddTerm=v.get(6).asInt();color.alphaAddTerm=v.get(7).asInt();}
     temp.addTag(new PlaceObject2Tag(swf,false,depth++,layer.getInt("id",0),m.toMATRIX(),color,-1,null,-1,null));
    }
    temp.addTag(new ShowFrameTag(swf));temp.frameCount=1;tl=temp.getTimeline();
    int li=0;for(DepthState ds:tl.getFrame(0).layers.values()){JsonObject l=layers.get(li++).asObject();if(l.get("filterSource")!=null){JsonArray f=l.get("filterSource").asArray();ds.filters=((DefineSpriteTag)swf.getCharacter(f.get(0).asInt())).getTimeline().getFrame(f.get(1).asInt()-1).layers.get(f.get(2).asInt()).filters;}}
   }
   RECT bounds=id==0?swf.displayRect:((BoundedTag)c).getRect();
   if(j.get("bounds")!=null){JsonArray v=j.get("bounds").asArray();bounds=new RECT((int)Math.floor(v.get(0).asDouble()*20),(int)Math.ceil(v.get(2).asDouble()*20),(int)Math.floor(v.get(1).asDouble()*20),(int)Math.ceil(v.get(3).asDouble()*20));}
   ColorTransform parentColor=new ColorTransform();
   if(j.get("color")!=null){JsonArray v=j.get("color").asArray();CXFORMWITHALPHA color=new CXFORMWITHALPHA();color.hasAddTerms=true;color.hasMultTerms=true;color.redMultTerm=v.get(0).asInt();color.greenMultTerm=v.get(1).asInt();color.blueMultTerm=v.get(2).asInt();color.alphaMultTerm=v.get(3).asInt();color.redAddTerm=v.get(4).asInt();color.greenAddTerm=v.get(5).asInt();color.blueAddTerm=v.get(6).asInt();color.alphaAddTerm=v.get(7).asInt();parentColor=color;}
   java.util.List<com.jpexs.decompiler.flash.types.filters.FILTER> parentFilters=null;
   if(j.get("filterSource")!=null){JsonArray f=j.get("filterSource").asArray();Timeline ft=f.get(0).asInt()==0?swf.getTimeline():((DefineSpriteTag)swf.getCharacter(f.get(0).asInt())).getTimeline();parentFilters=ft.getFrame(f.get(1).asInt()-1).layers.get(f.get(2).asInt()).filters;}
   if(parentFilters!=null)for(var filter:parentFilters){int dx=(int)Math.ceil(filter.getDeltaX()*20),dy=(int)Math.ceil(filter.getDeltaY()*20);bounds=new RECT(bounds.Xmin-dx,bounds.Xmax+dx,bounds.Ymin-dy,bounds.Ymax+dy);}
   // FFDec uses floor(min/20) and ceil(size/20); preserve the actual render viewport.
   int xmin=(int)Math.floor(bounds.Xmin/20.0)-2,ymin=(int)Math.floor(bounds.Ymin/20.0)-2;
   RECT viewport=new RECT(xmin*20,((int)Math.ceil(bounds.Xmax/20.0)+2)*20,ymin*20,((int)Math.ceil(bounds.Ymax/20.0)+2)*20);
   String key=j.getString("name",Integer.toString(id));Path folder=out.resolve(key);Files.createDirectories(folder);
   JsonArray exported=new JsonArray();
   for(JsonValue v:j.get("frames").asArray()){
    int frame=v.asInt();
    int renderFrame=frame-1;
    if(composite){int k=0;for(DepthState d:tl.getFrame(0).layers.values()){JsonObject layer=j.get("layers").asArray().get(k++).asObject();d.time=layer.getBoolean("animate",false)?0:layer.getInt("frame",1)-frame;}renderFrame=0;}
    else if(!(c instanceof DefineSpriteTag) && !(c instanceof ButtonTag) && id!=0)renderFrame=0;
    var image=SWF.frameToImageGet(tl,renderFrame,composite?frame-1:0,null,0,viewport,new Matrix(),parentColor,null,1.0,true,1);
    if(parentFilters!=null)for(var filter:parentFilters)if(filter.enabled)image=filter.apply(image,1.0,0,0,image.getWidth(),image.getHeight());
    Path file=folder.resolve(frame+".png");ImageIO.write(image.getBufferedImage(),"PNG",file.toFile());
    exported.add(new JsonObject().add("frame",frame).add("file",key+"/"+frame+".png").add("originX",-xmin).add("originY",-ymin).add("width",image.getWidth()).add("height",image.getHeight()));
   }
   Files.writeString(folder.resolve("frames.json"),exported.toString());
   for(var entry:maskedFrames.entrySet())entry.getKey().layers=entry.getValue();
   for(var entry:stoppedTimelines.entrySet())entry.getKey().getFrames().subList(entry.getValue(),entry.getKey().getFrameCount()).clear();
   System.out.println(key+" "+exported.size()+" frames");
  }
 }
}
