package com.xanaduyonder.classicgoldminer;

import java.net.URI;
import java.net.URISyntaxException;
import java.util.Locale;

/** One synthetic origin backed only by APK assets; no file/content/network fallthrough. */
public final class LocalAssetPolicy {
    public static final String ORIGIN = "https://classic-gold-miner.local/";
    public static final String ENTRY = ORIGIN + "index.html";
    public static final String CSP = "default-src 'none'; script-src 'self'; "
            + "style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:; media-src 'self' blob:; "
            + "font-src 'self' data:; connect-src 'self'; worker-src 'none'; child-src 'none'; "
            + "frame-src 'none'; object-src 'none'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'";

    private LocalAssetPolicy() { }

    public static String assetPath(String value) {
        if (value == null || value.length() > 2048) return null;
        try {
            URI uri = new URI(value);
            if (!"https".equals(uri.getScheme()) || !"classic-gold-miner.local".equals(uri.getRawAuthority())) return null;
            String path = uri.getRawPath();
            // Bundled names use this alphabet. Reject encoded separators/traversal before any decoding.
            if (path == null || !path.matches("/[A-Za-z0-9_./-]+") || path.contains("//")) return null;
            for (String segment : path.split("/")) {
                if (".".equals(segment) || "..".equals(segment)) return null;
            }
            if (path.endsWith("/")) return null;
            String query = uri.getRawQuery();
            if (query != null && !(isDocumentPath(path) && ("resume=1".equals(query) || "new=1".equals(query)))) return null;
            return "classic-gold-miner" + path;
        } catch (URISyntaxException exception) {
            return null;
        }
    }

    public static boolean isDocument(String url) {
        String asset = assetPath(url);
        return asset != null && isDocumentPath(asset.substring("classic-gold-miner".length()));
    }

    private static boolean isDocumentPath(String path) {
        return "/index.html".equals(path) || "/solo/index.html".equals(path) || "/duo/index.html".equals(path);
    }

    public static String mimeType(String path) {
        String name = path.toLowerCase(Locale.ROOT);
        if (name.endsWith(".html")) return "text/html";
        if ((name.endsWith(".js") || name.endsWith(".mjs"))) return "application/javascript";
        if (name.endsWith(".css")) return "text/css";
        if (name.endsWith(".json")) return "application/json";
        if ((name.endsWith(".xml") || name.endsWith(".fnt"))) return "application/xml";
        if (name.endsWith(".png")) return "image/png";
        if (name.endsWith(".jpg") || name.endsWith(".jpeg")) return "image/jpeg";
        if (name.endsWith(".webp")) return "image/webp";
        if (name.endsWith(".gif")) return "image/gif";
        if (name.endsWith(".svg")) return "image/svg+xml";
        if (name.endsWith(".mp3")) return "audio/mpeg";
        if (name.endsWith(".ogg")) return "audio/ogg";
        if (name.endsWith(".wav")) return "audio/wav";
        if (name.endsWith(".m4a")) return "audio/mp4";
        if (name.endsWith(".woff")) return "font/woff";
        if (name.endsWith(".woff2")) return "font/woff2";
        if (name.endsWith(".ttf")) return "font/ttf";
        return "application/octet-stream";
    }
}
