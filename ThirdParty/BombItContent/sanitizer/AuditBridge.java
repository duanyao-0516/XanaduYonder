import java.io.*;
import java.nio.file.*;
import java.util.*;
import com.jpexs.decompiler.flash.SWF;
import com.jpexs.decompiler.flash.action.Action;
import com.jpexs.decompiler.flash.action.swf1.ActionGetURL;
import com.jpexs.decompiler.flash.action.swf4.ActionGetURL2;
import com.jpexs.decompiler.flash.action.swf4.ActionPush;
import com.jpexs.decompiler.flash.tags.*;
import com.jpexs.decompiler.flash.tags.base.*;

/** Independent parser audit for the eight changed scripts, with no game execution. */
public final class AuditBridge {
    static void require(boolean value, String message) {
        if (!value) throw new IllegalStateException(message);
    }
    static byte[] append(byte[] original, byte[] bridge) {
        require(original[original.length - 1] == 0, "Missing original ActionEnd");
        byte[] result = Arrays.copyOf(original, original.length - 1 + bridge.length);
        System.arraycopy(bridge, 0, result, original.length - 1, bridge.length);
        return result;
    }
    static void inspectTags(SWF swf, Iterable<Tag> tags) {
        var characters = swf.getCharacters(false);
        for (Tag tag : tags) {
            Set<Integer> needed = new HashSet<>();
            tag.getNeededCharacters(needed, new HashSet<>(), swf);
            for (int id : needed) {
                require(id != 1 && id != 603, "Removed SDK character referenced");
                require(id == 0 || id == 65535 || characters.containsKey(id), "Missing character: " + id);
            }
            if (tag instanceof FileAttributesTag) require(!((FileAttributesTag)tag).useNetwork, "UseNetwork set");
            if (tag instanceof DefineSpriteTag) inspectTags(swf, ((DefineSpriteTag)tag).getTags());
        }
    }
    public static void main(String[] args) throws Exception {
        require(args.length == 3, "original.swf clean.swf sanitizer/bridge");
        SWF original = new SWF(new FileInputStream(args[0]), false);
        SWF clean = new SWF(new FileInputStream(args[1]), false);
        require(!original.isAS3() && !clean.isAS3(), "Unexpected AVM");
        Map<String, ASMSource> before = original.getASMs(false), after = clean.getASMs(false);
        Set<String> removed = new TreeSet<>(before.keySet()); removed.removeAll(after.keySet());
        require(removed.equals(Set.of("/FWAd_AS2", "/__Packages/FWAd_AS2")), "Unexpected removed scripts: " + removed);
        Map<String, String> patches = Map.of(
            "/frame 1 (name: \"loading\") - DoAction", "root-init",
            "/frame 3 (name: \"menu\") - DoAction", "menu-ready",
            "/DefineSprite (chid: 519)/frame 1 - DoAction", "timer",
            "/frame 8 (name: \"select\") - DoAction", "restore-select",
            "/frame 9 (name: \"game\") - DoAction", "restore-game",
            "/DefineSprite (chid: 554)/frame 1 - DoAction", "game-checkpoint",
            "/DefineButton2 (chid: 533)/BUTTONCONDACTION on(release, keyPress \"p\")", "pause-button",
            "/DefineButton2 (chid: 533)/BUTTONCONDACTION on(keyPress \"P\")", "pause-button");
        Set<String> applied = new TreeSet<>();
        int retained = 0, fscommands = 0, stateNotifications = 0;
        for (var entry : after.entrySet()) {
            ASMSource previous = before.get(entry.getKey());
            require(previous != null, "Unreviewed added script: " + entry.getKey());
            byte[] expected = previous.getActionBytes().getRangeData();
            String patch = patches.get(entry.getKey());
            if (patch != null) {
                byte[] bridge = Files.readAllBytes(Path.of(args[2], patch + ".bin"));
                expected = patch.equals("pause-button") ? bridge : append(expected, bridge);
                applied.add(entry.getKey());
            } else retained++;
            require(Arrays.equals(expected, entry.getValue().getActionBytes().getRangeData()), "Unexpected action mutation: " + entry.getKey());
            Action previousAction = null;
            for (Action action : entry.getValue().getActions()) {
                if (action instanceof ActionGetURL2) {
                    ActionGetURL2 url = (ActionGetURL2)action;
                    require(previousAction instanceof ActionPush, "Dynamic GetURL2");
                    ActionPush push = (ActionPush)previousAction;
                    require(push.values.size() == 2, "Unexpected URL argument count");
                    String target = ActionPush.valueToString(push.values.get(0), push.constantPool);
                    require(!url.loadTargetFlag && !url.loadVariablesFlag && url.sendVarsMethod == 0, "Unexpected URL flags");
                    if (target.equals("\"FSCommand:xanadu-state\"")) {
                        require("root-init".equals(patch) && "\"\"".equals(ActionPush.valueToString(push.values.get(1), push.constantPool)), "Unexpected bridge notification");
                        stateNotifications++;
                    } else {
                        require((target.equals("\"FSCommand:allowscale\"") || target.equals("\"FSCommand:showmenu\"")) && Boolean.FALSE.equals(push.values.get(1)), "External URL operation");
                        fscommands++;
                    }
                }
                if (action instanceof ActionGetURL) {
                    ActionGetURL url = (ActionGetURL)action;
                    if (url.urlString.equals("FSCommand:xanadu-state")) {
                        require("root-init".equals(patch) && url.targetString.equals(""), "Unexpected bridge notification");
                        stateNotifications++;
                    } else {
                        require((url.urlString.equals("FSCommand:allowscale") || url.urlString.equals("FSCommand:showmenu")) && url.targetString.equals("false"), "External URL operation");
                        fscommands++;
                    }
                }
                previousAction = action;
            }
        }
        require(applied.equals(patches.keySet()), "Incomplete bridge patch");
        require(stateNotifications == 1, "Expected one fixed bridge notification instruction");
        require(!clean.getCharacters(false).containsKey(1) && !clean.getCharacters(false).containsKey(603), "SDK retained");
        inspectTags(clean, clean.getTags());
        System.out.printf("{\"parser\":\"JPEXS 26.4.0\",\"unchangedScripts\":%d,\"appendedScripts\":6,\"replacedPauseActions\":2,\"removedSdkScripts\":2,\"displayOnlyFsCommands\":%d,\"fixedStateNotification\":1,\"externalNavigationOperations\":0,\"missingCharacters\":0,\"gameplayExecuted\":false}%n", retained, fscommands);
        System.exit(0); // FFDec parser worker pools are not owned by this CLI.
    }
}
