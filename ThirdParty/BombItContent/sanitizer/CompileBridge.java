import java.nio.file.*;
import java.io.*;
import com.jpexs.decompiler.flash.SWF;
import com.jpexs.decompiler.flash.tags.DoActionTag;
import com.jpexs.decompiler.flash.action.Action;
import com.jpexs.decompiler.flash.action.parser.script.ActionScript2Parser;

/** Optional regeneration tool. Production content builds use the locked .bin files. */
public final class CompileBridge {
    public static void main(String[] args) throws Exception {
        if (args.length != 3) throw new IllegalArgumentException("original.swf source.as output.bin");
        SWF swf = new SWF(new FileInputStream(args[0]), false);
        DoActionTag target = new DoActionTag(swf);
        ActionScript2Parser parser = new ActionScript2Parser(swf, target);
        byte[] actions = Action.actionsToBytes(parser.actionsFromString(Files.readString(Path.of(args[1])), "UTF-8"), true, 9);
        Files.write(Path.of(args[2]), actions);
    }
}
