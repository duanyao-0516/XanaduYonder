package com.xanaduyonder.classicgoldminer;

import android.content.Context;
import android.util.AtomicFile;
import android.webkit.JavascriptInterface;
import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;
import org.json.JSONTokener;
import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.util.Iterator;

/** Synchronous WebView bridge. No legacy Unity save filename is accessed. */
public final class ClassicSaveStore {
    private static final int MAX_BYTES = 4 * 1024 * 1024;
    private final File directory;
    private final boolean sound;
    private String lastError = "";

    ClassicSaveStore(Context context, boolean sound) {
        directory = context.getFilesDir();
        this.sound = sound;
    }

    @JavascriptInterface
    public synchronized String readSave(String mode) {
        try {
            AtomicFile file = fileFor(mode);
            if (!file.getBaseFile().exists() && !new File(file.getBaseFile().getPath() + ".bak").exists()) {
                lastError = "";
                return "";
            }
            byte[] bytes = readBounded(file);
            String raw = new String(bytes, StandardCharsets.UTF_8);
            // Return the original data even when corrupt: the shared host reports it, preserves it,
            // and can offer an explicit new game instead of silently overwriting a damaged save.
            try { validate(mode, raw); lastError = ""; }
            catch (JSONException invalid) { lastError = "存档格式无效，原文件已保留。"; }
            return raw;
        } catch (IOException | IllegalArgumentException error) {
            lastError = "无法读取存档，原文件已保留：" + error.getMessage();
            // An error must not look like an absent save to ClassicHost.read().
            return "{\"nativeReadError\":true}";
        }
    }

    @JavascriptInterface
    public synchronized boolean writeSave(String mode, String json) {
        AtomicFile file = null;
        FileOutputStream output = null;
        try {
            file = fileFor(mode);
            validate(mode, json);
            byte[] bytes = json.getBytes(StandardCharsets.UTF_8);
            output = file.startWrite();
            output.write(bytes);
            output.flush();
            output.getFD().sync();
            file.finishWrite(output);
            output = null;
            // AtomicFile.finishWrite returns void on Android. Verify the committed bytes before
            // acknowledging success, so a failed rename cannot become a successful exit.
            if (!java.util.Arrays.equals(bytes, readBounded(file))) throw new IOException("提交后的存档校验失败");
            lastError = "";
            return true;
        } catch (IOException | JSONException | IllegalArgumentException error) {
            if (file != null && output != null) file.failWrite(output);
            lastError = "保存失败：" + error.getMessage();
            return false;
        }
    }

    @JavascriptInterface
    public synchronized String getLastSaveError() { return lastError; }

    @JavascriptInterface
    public boolean isSoundEnabled() { return sound; }

    private AtomicFile fileFor(String mode) {
        if (!"solo".equals(mode) && !"duo".equals(mode)) throw new IllegalArgumentException("不支持的游戏模式");
        return new AtomicFile(new File(directory, "classic-gold-miner-" + mode + "-v1.json"));
    }

    private static byte[] readBounded(AtomicFile file) throws IOException {
        try (FileInputStream input = file.openRead(); ByteArrayOutputStream bytes = new ByteArrayOutputStream()) {
            byte[] chunk = new byte[8192];
            int count;
            while ((count = input.read(chunk)) != -1) {
                if (bytes.size() + count > MAX_BYTES) throw new IOException("存档超过大小限制");
                bytes.write(chunk, 0, count);
            }
            return bytes.toByteArray();
        }
    }

    private static void validate(String mode, String json) throws JSONException {
        if (json == null || json.length() == 0 || json.length() > MAX_BYTES
                || json.getBytes(StandardCharsets.UTF_8).length > MAX_BYTES) throw new JSONException("存档为空或过大");
        // Bound nesting before invoking the platform parser (which otherwise recursively descends).
        int depth = 0;
        boolean quoted = false, escaped = false;
        for (int index = 0; index < json.length(); index++) {
            char c = json.charAt(index);
            if (quoted) {
                if (escaped) escaped = false;
                else if (c == '\\') escaped = true;
                else if (c == '"') quoted = false;
            } else if (c == '"') quoted = true;
            else if (c == '{' || c == '[') { if (++depth > 48) throw new JSONException("存档嵌套过深"); }
            else if (c == '}' || c == ']') depth--;
        }
        JSONTokener tokens = new JSONTokener(json);
        Object value = tokens.nextValue();
        if (!(value instanceof JSONObject) || tokens.nextClean() != 0) throw new JSONException("存档必须为单个 JSON 对象");
        JSONObject object = (JSONObject) value;
        Object schema = object.opt("schema");
        if (!(schema instanceof Number) || ((Number) schema).doubleValue() != 1d
                || !mode.equals(object.opt("mode"))) throw new JSONException("存档版本或模式不匹配");
        validateTree(object, new int[] {0});
    }

    private static void validateTree(Object value, int[] count) throws JSONException {
        if (++count[0] > 100000) throw new JSONException("存档包含过多数据");
        if (value instanceof JSONObject) {
            JSONObject object = (JSONObject) value;
            Iterator<String> names = object.keys();
            while (names.hasNext()) validateTree(object.get(names.next()), count);
        } else if (value instanceof JSONArray) {
            JSONArray array = (JSONArray) value;
            for (int index = 0; index < array.length(); index++) validateTree(array.get(index), count);
        } else if (value instanceof Number && !Double.isFinite(((Number) value).doubleValue())) {
            throw new JSONException("存档包含非有限数值");
        }
    }
}
