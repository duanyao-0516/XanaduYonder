import java.io.*;import java.security.*;import java.util.*;import java.nio.file.*;import com.jpexs.decompiler.flash.*;import com.jpexs.decompiler.flash.tags.*;import com.jpexs.decompiler.flash.abc.*;import com.jpexs.decompiler.flash.abc.avm2.parser.script.*;
public class InjectProbe {
static Map<String,Integer> hashes(SWF swf)throws Exception {Map<String,Integer> h=new TreeMap<>();for(ABCContainerTag tag:swf.getAbcList()){ABC abc=tag.getABC();ByteArrayOutputStream out=new ByteArrayOutputStream();abc.saveToStream(out);String hash=HexFormat.of().formatHex(MessageDigest.getInstance("SHA-256").digest(out.toByteArray()));h.merge(hash,1,Integer::sum);}return h;}
public static void main(String[]args)throws Exception{
 SWF swf=new SWF(Files.newInputStream(Path.of(args[0])),false);Map<String,Integer> originalHashes=hashes(swf);SWF.initPlayer();
 DoABCTag added=new DoABCTag(swf);ABC abc=new ABC(added);added.setABC(abc);
 AbcIndexing index=swf.getAbcIndex();index.selectAbc(abc);new ActionScript3Parser(index).addScript(Files.readString(Path.of(args[2])),"probe.as",0,0,"",abc);added.setModified(true);
 System.out.println("doc="+swf.getDocumentClass()+" abcCount="+swf.getAbcList().size());
 int n=0;for(Tag tag:swf.getTags()){if(tag instanceof ShowFrameTag)break;n++;}System.out.println("insert="+n+" before="+swf.getTags().get(n));swf.addTag(n,added);
 try(OutputStream out=Files.newOutputStream(Path.of(args[1]))){swf.saveTo(out);}Map<String,Integer> outputHashes=hashes(new SWF(Files.newInputStream(Path.of(args[1])),false));for(var e:originalHashes.entrySet())if(outputHashes.getOrDefault(e.getKey(),0)<e.getValue())throw new AssertionError("Original ABC changed");System.out.println("Verified original ABC hashes retained: "+originalHashes);System.out.println("Added AS3 probe ABC without editing original methods: "+abc.bodies.size()+" new bodies");
}}
