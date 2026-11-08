import java.io.FileInputStream;
import java.util.*;
import com.jpexs.decompiler.flash.SWF;
import com.jpexs.decompiler.flash.action.Action;
import com.jpexs.decompiler.flash.action.swf1.ActionGetURL;
import com.jpexs.decompiler.flash.action.swf4.ActionGetURL2;
import com.jpexs.decompiler.flash.action.swf4.ActionPush;
import com.jpexs.decompiler.flash.tags.*;
import com.jpexs.decompiler.flash.tags.base.*;
import com.eclipsesource.json.*;

/** Independent semantic audit using the official JPEXS 26.4.0 parser.
 * This development tool is not shipped in the app and does not execute AS2.
 */
public final class AuditSwf {
    static int tagCount;
    static void require(boolean okay, String message) {
        if (!okay) throw new IllegalStateException(message);
    }
    static void inspectTags(SWF swf, Iterable<Tag> tags, Set<Integer> removedIds) {
        Map<Integer, CharacterTag> characters = swf.getCharacters(false);
        for (Tag tag : tags) {
            tagCount++;
            Set<Integer> needed = new HashSet<>();
            tag.getNeededCharacters(needed, new HashSet<>(), swf);
            for (int id : needed) {
                require(!removedIds.contains(id), "SDK symbol still referenced: " + id);
                require(id == 0 || id == 65535 || characters.containsKey(id), "Missing character: " + id);
            }
            if (tag instanceof FileAttributesTag) {
                require(!((FileAttributesTag)tag).useNetwork, "UseNetwork still set");
            }
            if (tag instanceof DefineSpriteTag) inspectTags(swf, ((DefineSpriteTag)tag).getTags(), removedIds);
        }
    }
    public static void main(String[] args) throws Exception {
        SWF original = new SWF(new FileInputStream(args[0]), false);
        SWF clean = new SWF(new FileInputStream(args[1]), false);
        require(!original.isAS3() && !clean.isAS3(), "Unexpected AS3 runtime");
        Map<String, ASMSource> before = original.getASMs(false);
        Map<String, ASMSource> after = clean.getASMs(false);
        Set<String> removed = new TreeSet<>(before.keySet());
        removed.removeAll(after.keySet());
        require(removed.equals(new TreeSet<>(Arrays.asList("/FWAd_AS2", "/__Packages/FWAd_AS2"))), "Unexpected script removal: " + removed);
        int actions = 0, displayFsCommands = 0;
        for (Map.Entry<String, ASMSource> entry : after.entrySet()) {
            ASMSource previous = before.get(entry.getKey());
            require(previous != null && Arrays.equals(previous.getActionBytes().getRangeData(), entry.getValue().getActionBytes().getRangeData()), "Gameplay actions changed: " + entry.getKey());
            Action previousAction = null;
            for (Action action : entry.getValue().getActions()) {
                actions++;
                if (action instanceof ActionGetURL2) {
                    // Flash encodes its two display-only fscommands using the
                    // same opcode as networking. Resolve the preceding literal
                    // stack arguments instead of rejecting legitimate commands.
                    ActionGetURL2 url = (ActionGetURL2) action;
                    require(previousAction instanceof ActionPush, "Dynamic GetURL2 arguments");
                    ActionPush push = (ActionPush) previousAction;
                    require(push.values.size() == 2 && Boolean.FALSE.equals(push.values.get(1)), "Unexpected GetURL2 arguments");
                    String target = ActionPush.valueToString(push.values.get(0), push.constantPool);
                    require((target.equals("\"FSCommand:allowscale\"") || target.equals("\"FSCommand:showmenu\"")) && !url.loadTargetFlag && !url.loadVariablesFlag && url.sendVarsMethod == 0, "Network GetURL2 operation: " + target);
                    displayFsCommands++;
                }
                if (action instanceof ActionGetURL) {
                    ActionGetURL url = (ActionGetURL) action;
                    require((url.urlString.equals("FSCommand:allowscale") || url.urlString.equals("FSCommand:showmenu")) && url.targetString.equals("false"), "External navigation instruction: " + url.urlString);
                    displayFsCommands++;
                }
                previousAction = action;
            }
        }
        Set<Integer> removedIds = new HashSet<>(Arrays.asList(1, 603));
        for (int id : removedIds) require(!clean.getCharacters(false).containsKey(id), "SDK character remains");
        inspectTags(clean, clean.getTags(), removedIds);
        JsonArray removedNames = new JsonArray();
        removed.forEach(removedNames::add);
        System.out.println(new JsonObject().add("parser", "JPEXS 26.4.0")
            .add("originalScripts", before.size()).add("preservedScripts", after.size())
            .add("removedScripts", removedNames).add("actionsInspected", actions)
            .add("displayOnlyFsCommands", displayFsCommands).add("recursiveTags", tagCount)
            .add("gameplayActionsByteIdentical", true).add("missingCharacterReferences", 0)
            .add("externalNavigationOperations", 0).add("networkGetUrl2Operations", 0)
            .add("gameplayExecuted", false).toString(WriterConfig.PRETTY_PRINT));
    }
}
