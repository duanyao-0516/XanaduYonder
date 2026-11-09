package com.xanaduyonder.webgames;

import android.app.Activity;
import android.content.Context;
import android.system.ErrnoException;
import android.system.Os;
import android.system.OsConstants;
import android.util.Log;
import java.io.File;
import java.io.FileDescriptor;
import java.io.FileOutputStream;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.util.UUID;
import org.json.JSONException;
import org.json.JSONObject;

/** One latest global feedback edit while Unity is paused; never owns gameplay saves. */
public final class WebGameFeedbackPreferences {
    private WebGameFeedbackPreferences() { }
    private static File file(Context context) {
        return new File(context.getApplicationContext().getFilesDir(), "web-game-feedback/journal-v2.json");
    }
    private static File legacyFile(Context context) {
        return new File(context.getApplicationContext().getFilesDir(), "bomb-it-feedback/pending-v1.json");
    }
    private static void syncDirectory(File directory) throws ErrnoException {
        FileDescriptor descriptor = Os.open(directory.getPath(), OsConstants.O_RDONLY, 0);
        try { Os.fsync(descriptor); } finally { Os.close(descriptor); }
    }
    private static JSONObject readRecord(Context context) throws IOException, JSONException {
        File shared = file(context);
        boolean legacy = !shared.exists();
        File source = legacy ? legacyFile(context) : shared;
        if (!source.exists()) return null;
        if (source.length() > 1024) throw new IOException("Feedback journal exceeds schema size");
        JSONObject value = new JSONObject(new String(Files.readAllBytes(source.toPath()), StandardCharsets.UTF_8));
        if (!(value.opt("schemaVersion") instanceof Integer) || value.getInt("schemaVersion") != (legacy ? 1 : 2)
                || !(value.opt("revision") instanceof String)
                || !value.getString("revision").matches("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")
                || !(value.opt("soundEnabled") instanceof Boolean) || !(value.opt("hapticsEnabled") instanceof Boolean)
                || (!legacy && !(value.opt("pending") instanceof Boolean))) throw new IOException("Invalid feedback journal");
        if (legacy) { value.put("schemaVersion", 2); value.put("pending", true); value.put("source", "bomb-it"); }
        return value;
    }
    public static synchronized JSONObject readPending(Context context) {
        try {
            JSONObject record = readRecord(context);
            if (record == null || !record.getBoolean("pending")) return null;
            // Stable public schema; the durable ACK tombstone stays private.
            return new JSONObject().put("schemaVersion", 1).put("revision", record.getString("revision"))
                    .put("soundEnabled", record.getBoolean("soundEnabled")).put("hapticsEnabled", record.getBoolean("hapticsEnabled"));
        } catch (IOException | JSONException | RuntimeException error) {
            Log.w("WebGameFeedback", "Unable to read preference journal", error); return null;
        }
    }
    public static synchronized String readPendingFeedback(Activity host) {
        if (host == null) return "";
        JSONObject value = readPending(host); return value == null ? "" : value.toString();
    }
    public static synchronized boolean write(Context context, String source, boolean sound, boolean haptics) {
        if (!"bomb-it".equals(source) && !"level-devil".equals(source)) return false;
        try {
            return writeRecord(context, new JSONObject().put("schemaVersion", 2).put("revision", UUID.randomUUID().toString())
                    .put("source", source).put("pending", true).put("soundEnabled", sound).put("hapticsEnabled", haptics));
        } catch (JSONException error) { return false; }
    }
    private static boolean writeRecord(Context context, JSONObject record) {
        File temporary = null;
        try {
            File target = file(context), directory = target.getParentFile();
            if (!directory.isDirectory() && !directory.mkdirs()) throw new IOException("Cannot create feedback directory");
            temporary = File.createTempFile(".pending-", ".json", directory);
            try (FileOutputStream output = new FileOutputStream(temporary)) {
                output.write(record.toString().getBytes(StandardCharsets.UTF_8)); output.flush(); output.getFD().sync();
            }
            Os.rename(temporary.getPath(), target.getPath()); syncDirectory(directory); return true;
        } catch (IOException | ErrnoException | RuntimeException error) {
            Log.w("WebGameFeedback", "Feedback persistence is unconfirmed", error); return false;
        } finally {
            if (temporary != null && temporary.exists() && !temporary.delete()) Log.w("WebGameFeedback", "Temporary journal cleanup failed");
        }
    }
    public static synchronized boolean acknowledgePendingFeedback(Activity host, String revision) {
        if (host == null || revision == null) return false;
        try {
            JSONObject record = readRecord(host);
            if (record == null) return true;
            if (!revision.equals(record.getString("revision"))) return false;
            // Keep the ACK record: removing it would resurrect an old legacy
            // Bomb It journal on the next read. Rewriting also retries fsync.
            record.put("pending", false); return writeRecord(host, record);
        } catch (IOException | JSONException | RuntimeException error) {
            Log.w("WebGameFeedback", "Preference acknowledgement failed", error); return false;
        }
    }
}
