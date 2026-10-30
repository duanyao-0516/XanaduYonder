import java.nio.file.*;
import com.eclipsesource.json.*;
import com.jpexs.decompiler.flash.*;
import com.jpexs.decompiler.flash.tags.*;
import com.jpexs.decompiler.flash.tags.base.*;

/** Source sample offsets are SWF metadata, not inferred silence trimming. */
public final class AowAudioMetadata {
 static JsonObject format(SoundTag sound) {
  var f=sound.getSoundFormat();
  return new JsonObject().add("sampleRate",f.samplingRate).add("channels",f.stereo?2:1)
   .add("sampleCount",sound.getTotalSoundSampleCount()).add("initialLatency",sound.getInitialLatency())
   .add("format",sound.getSoundFormatId());
 }
 static void streams(Iterable<Tag> tags,int parent,JsonArray output) {
  for(Tag tag:tags) {
   if(tag instanceof SoundStreamHeadTypeTag head)for(var range:head.getRanges()) {
    long bytes=0;for(var block:range.getRawSoundData())bytes+=block.getLength();
    if(bytes==0)continue;
    output.add(format(range).add("characterId",parent).add("firstFrame",range.startFrame+1)
     .add("lastFrame",range.endFrame+1).add("blocks",range.blocks.size()).add("encodedBytes",bytes)
     .add("seekSamples",range.getSeekSamples()));
   }
   if(tag instanceof DefineSpriteTag sprite){sprite.getTimeline();streams(sprite.getTags(),sprite.spriteId,output);}
  }
 }
 public static void main(String[] args)throws Exception {
  SWF swf=new SWF(Files.newInputStream(Path.of(args[0])),false);swf.getTimeline();
  JsonArray sounds=new JsonArray(),streams=new JsonArray();
  for(CharacterTag c:swf.getCharacters(false).values())if(c instanceof DefineSoundTag sound)
   sounds.add(format(sound).add("characterId",sound.soundId));
  streams(swf.getTags(),0,streams);
  Files.writeString(Path.of(args[1]),new JsonObject().add("sounds",sounds).add("streams",streams).toString());
 }
}
