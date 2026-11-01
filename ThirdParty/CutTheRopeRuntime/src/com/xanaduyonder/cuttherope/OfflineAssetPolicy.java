package com.xanaduyonder.cuttherope;

import java.net.URI;
import java.net.URISyntaxException;
import java.util.Locale;

/** Exact, non-normalizing URL allowlist shared by WebView interception and host JVM tests. */
final class OfflineAssetPolicy {
    static final String START_URL = "https://appassets.androidplatform.net/assets/cut-the-rope/index.html";
    private static final String AUTHORITY = "appassets.androidplatform.net";
    private static final String PREFIX = "/assets/cut-the-rope/";

    private OfflineAssetPolicy() { }

    static String assetPath(String url) {
        if (url == null) return null;
        try {
            URI uri = new URI(url);
            if (!"https".equals(uri.getScheme()) || !AUTHORITY.equals(uri.getRawAuthority())) return null;
            String path = uri.getRawPath();
            if (path == null || !path.startsWith(PREFIX) || path.length() == PREFIX.length()) return null;
            // Packaged filenames are ASCII. Do not decode escapes or normalize dot segments:
            // an encoded separator, NUL, backslash, or traversal is never a valid asset alias.
            if (!path.matches("[a-zA-Z0-9._~/-]+")) return null;
            for (String segment : path.substring(1).split("/", -1)) {
                if (segment.isEmpty() || segment.equals(".") || segment.equals("..")) return null;
            }
            return path.substring("/assets/".length());
        } catch (URISyntaxException invalid) {
            return null;
        }
    }

    static boolean isEntryUrl(String url) { return START_URL.equals(url); }

    static String mimeType(String path) {
        String lower = path.toLowerCase(Locale.ROOT);
        if (lower.endsWith(".html")) return "text/html";
        if (lower.endsWith(".js")) return "application/javascript";
        if (lower.endsWith(".css")) return "text/css";
        if (lower.endsWith(".json")) return "application/json";
        if (lower.endsWith(".png")) return "image/png";
        if (lower.endsWith(".jpg") || lower.endsWith(".jpeg")) return "image/jpeg";
        if (lower.endsWith(".svg")) return "image/svg+xml";
        if (lower.endsWith(".webp")) return "image/webp";
        if (lower.endsWith(".mp3")) return "audio/mpeg";
        if (lower.endsWith(".m4a") || lower.endsWith(".mp4")) return "audio/mp4";
        if (lower.endsWith(".aac")) return "audio/aac";
        if (lower.endsWith(".ogg")) return "audio/ogg";
        if (lower.endsWith(".wav")) return "audio/wav";
        if (lower.endsWith(".woff")) return "font/woff";
        if (lower.endsWith(".woff2")) return "font/woff2";
        if (lower.endsWith(".ttf")) return "font/ttf";
        if (lower.endsWith(".wasm")) return "application/wasm";
        if (lower.endsWith(".txt")) return "text/plain";
        return "application/octet-stream";
    }
}
