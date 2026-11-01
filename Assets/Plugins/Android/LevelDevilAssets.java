package com.xanaduyonder.leveldevil;

import android.content.res.AssetManager;
import android.webkit.WebResourceResponse;
import java.io.ByteArrayInputStream;
import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;
import java.util.Collections;
import java.util.HashMap;
import java.util.HashSet;
import java.util.Map;
import java.util.Set;
import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

/** The installed manifest is the sole authority for paths, sizes, hashes and MIME types. */
final class LevelDevilAssets {
    private static final int MAX_ASSET_BYTES = 16 * 1024 * 1024;
    private static final String CONTENT_SECURITY_POLICY =
            "default-src 'none'; script-src 'self' 'unsafe-inline' 'unsafe-eval'; "
            + "style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:; "
            + "media-src 'self' blob:; font-src 'self' data:; connect-src 'self'; "
            + "frame-src 'none'; child-src 'none'; worker-src 'none'; "
            + "object-src 'none'; base-uri 'none'; form-action 'none'";
    private final AssetManager manager;
    private final Map<String, Entry> entries = new HashMap<>();

    private static final class Entry {
        final String hash;
        final int bytes;
        final String mime;
        Entry(String hash, int bytes, String mime) {
            this.hash = hash; this.bytes = bytes; this.mime = mime;
        }
    }

    LevelDevilAssets(AssetManager manager) throws IOException, JSONException {
        this.manager = manager;
        byte[] raw;
        try (InputStream input = manager.open("level-devil/manifest.json", AssetManager.ACCESS_STREAMING)) {
            raw = readBounded(input, 1024 * 1024);
        }
        JSONArray files = new JSONObject(new String(raw, StandardCharsets.UTF_8)).getJSONArray("files");
        if (files.length() == 0 || files.length() > 4096) throw new IOException("Invalid asset count");
        Set<String> mimeTypes = new HashSet<>();
        Collections.addAll(mimeTypes, "text/html", "application/javascript", "text/javascript",
                "application/json", "application/octet-stream", "audio/mpeg", "image/png", "text/css");
        for (int i = 0; i < files.length(); i++) {
            JSONObject file = files.getJSONObject(i);
            String path = file.getString("path"), hash = file.getString("sha256"), mime = file.getString("mime");
            Object size = file.get("bytes");
            if (!(size instanceof Integer || size instanceof Long)) throw new IOException("Invalid asset length");
            long bytes = ((Number) size).longValue();
            if (!LevelDevilAssetPolicy.validRelativePath(path) || entries.containsKey(path)
                    || !hash.matches("[a-f0-9]{64}") || bytes <= 0 || bytes > MAX_ASSET_BYTES
                    || !mimeTypes.contains(mime)) throw new IOException("Invalid asset manifest entry");
            entries.put(path, new Entry(hash, (int) bytes, mime));
        }
        if (!entries.containsKey("index.html")) throw new IOException("Missing game entry");
    }

    Set<String> paths() { return Collections.unmodifiableSet(entries.keySet()); }

    int size(String path) {
        Entry entry = entries.get(path);
        return entry == null ? 0 : entry.bytes;
    }

    WebResourceResponse load(String path) throws IOException {
        Entry entry = entries.get(path);
        if (entry == null) return denied();
        byte[] data;
        try (InputStream input = manager.open("level-devil/" + path, AssetManager.ACCESS_STREAMING)) {
            data = readBounded(input, entry.bytes);
        }
        if (data.length != entry.bytes || !sha256(data).equals(entry.hash)) {
            throw new IOException("Game asset integrity check failed");
        }
        Map<String, String> headers = new HashMap<>();
        headers.put("Cache-Control", "no-store");
        headers.put("X-Content-Type-Options", "nosniff");
        headers.put("Content-Security-Policy", CONTENT_SECURITY_POLICY);
        headers.put("Referrer-Policy", "no-referrer");
        headers.put("Permissions-Policy", "camera=(), microphone=(), geolocation=(), payment=(), usb=()");
        String encoding = entry.mime.startsWith("text/") || entry.mime.endsWith("javascript")
                || entry.mime.equals("application/json") ? "UTF-8" : null;
        return new WebResourceResponse(entry.mime, encoding, 200, "OK", headers, new ByteArrayInputStream(data));
    }

    static WebResourceResponse denied() {
        return new WebResourceResponse("text/plain", "UTF-8", 403, "Forbidden",
                Collections.singletonMap("Cache-Control", "no-store"), new ByteArrayInputStream(new byte[0]));
    }

    private static byte[] readBounded(InputStream input, int maximum) throws IOException {
        ByteArrayOutputStream output = new ByteArrayOutputStream(Math.min(maximum, 64 * 1024));
        byte[] buffer = new byte[8192];
        int count;
        while ((count = input.read(buffer)) != -1) {
            if (count > maximum - output.size()) throw new IOException("Game asset exceeds declared size");
            output.write(buffer, 0, count);
        }
        return output.toByteArray();
    }

    private static String sha256(byte[] data) throws IOException {
        try {
            StringBuilder result = new StringBuilder(64);
            for (byte value : MessageDigest.getInstance("SHA-256").digest(data)) {
                result.append(Character.forDigit((value >>> 4) & 15, 16));
                result.append(Character.forDigit(value & 15, 16));
            }
            return result.toString();
        } catch (NoSuchAlgorithmException error) {
            throw new IOException("SHA-256 unavailable", error);
        }
    }
}
