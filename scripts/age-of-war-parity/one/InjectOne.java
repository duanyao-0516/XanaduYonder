import java.io.*;
import java.nio.file.*;
import java.security.*;
import java.util.*;
import com.jpexs.decompiler.flash.*;
import com.jpexs.decompiler.flash.tags.*;
import com.jpexs.decompiler.flash.tags.base.ASMSource;
import com.jpexs.decompiler.flash.timeline.*;
import com.jpexs.decompiler.flash.action.parser.script.ActionScript2Parser;

/** Adds new DoAction tags; never decompiles/recompiles the original actions. */
public class InjectOne {
 static void inject(SWF swf, Timelined target, int frame, String script) throws Exception {
  int current=1,index=0;
  for(Tag tag:target.getTags()) {
   if(tag instanceof ShowFrameTag) {
    if(current==frame) {
     DoActionTag extra=new DoActionTag(swf);
     extra.setActions(new ActionScript2Parser(swf,extra).actionsFromString(script,"UTF-8"));
     extra.setModified();target.addTag(index,extra);return;
    }
    current++;
   }
   index++;
  }
  throw new IllegalStateException("Frame not found: "+frame);
 }
 static Map<String,Integer> actionHashes(SWF swf)throws Exception {
  Map<String,Integer> hashes=new TreeMap<>();
  for(ASMSource source:swf.getASMs(false).values()){String hash=HexFormat.of().formatHex(MessageDigest.getInstance("SHA-256").digest(source.getActionBytes().getRangeData()));hashes.merge(hash,1,Integer::sum);}
  return hashes;
 }
 public static void main(String[] args)throws Exception {
  Path source=Path.of(args[0]),output=Path.of(args[1]),scripts=Path.of(args[2]);
  byte[] original=Files.readAllBytes(source);
  SWF swf=new SWF(new ByteArrayInputStream(original),false);
  Map<String,Integer> sourceHashes=actionHashes(swf);
  inject(swf,swf,1,Files.readString(scripts.resolve("boot.as")));
  inject(swf,swf,13,Files.readString(scripts.resolve("observe.as")));
  for(int id:new int[]{33,46,78,212,699,886}) {
   String hook="this.__parityOriginalEnterFrame=this.onEnterFrame; this.onEnterFrame=function(){if((_root.parityFrame<6 or (_root.parityFrame>=39 and _root.parityFrame<=44) or (_root.parityFrame>=550 and _root.parityFrame<=559) or (_root.parityFrame>=670 and _root.parityFrame<=675))){trace(\"AOW|P,\"+_root.parityFrame+\",\"+_name+\",before\");} this.__parityOriginalEnterFrame(); if((_root.parityFrame<6 or (_root.parityFrame>=39 and _root.parityFrame<=44) or (_root.parityFrame>=550 and _root.parityFrame<=559) or (_root.parityFrame>=670 and _root.parityFrame<=675))){trace(\"AOW|P,\"+_root.parityFrame+\",\"+_name+\",after\");}};";
   if(id==699)hook+="this.__parityHit=this.hit;this.hit=function(){trace(\"AOW|H,\"+_root.parityFrame+\",\"+uid+\",melee,\"+ed_id);this.__parityHit();};this.__parityRanged=this.ranged_hit;this.ranged_hit=function(){trace(\"AOW|H,\"+_root.parityFrame+\",\"+uid+\",ranged,\"+ed_id);this.__parityRanged();};";
   inject(swf,(DefineSpriteTag)swf.getCharacter(id),1,hook);
  }
  Files.createDirectories(output.getParent());
  try(OutputStream out=Files.newOutputStream(output)){swf.saveTo(out);}
  Map<String,Integer> outputHashes=actionHashes(new SWF(Files.newInputStream(output),false));
  for(var entry:sourceHashes.entrySet())if(outputHashes.getOrDefault(entry.getKey(),0)<entry.getValue())throw new AssertionError("Original action bytes changed: "+entry.getKey());
  System.out.println("Verified all "+sourceHashes.values().stream().mapToInt(Integer::intValue).sum()+" original action byte arrays preserved in output.");
  if(!Arrays.equals(original,Files.readAllBytes(source)))throw new AssertionError("Original modified");
  System.out.println("Copied original actions; added observer tags. Input SHA256="+HexFormat.of().formatHex(MessageDigest.getInstance("SHA-256").digest(original)));
 }
}
