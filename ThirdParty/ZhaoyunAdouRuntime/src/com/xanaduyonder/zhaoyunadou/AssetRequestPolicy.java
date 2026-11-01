package com.xanaduyonder.zhaoyunadou;

import java.io.ByteArrayOutputStream;
import java.net.URI;
import java.nio.ByteBuffer;
import java.nio.charset.CodingErrorAction;
import java.nio.charset.StandardCharsets;
import java.util.Locale;
import java.util.Set;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

/** Pure policy shared by Android asset serving and JVM boundary tests. */
public final class AssetRequestPolicy {
    public static final String HOST = "zhaoyun-adou.xanadu.invalid";
    public static final String ORIGIN = "https://" + HOST + "/";
    private static final Pattern RANGE = Pattern.compile("bytes=(\\d*)-(\\d*)");
    private AssetRequestPolicy() { }

    public static final class Decision {
        public final int status;
        public final String path;
        Decision(int status, String path) { this.status = status; this.path = path; }
    }
    public static Decision resolve(String url, String method, Set<String> files) {
        if (!"GET".equals(method) && !"HEAD".equals(method)) return new Decision(405, null);
        try {
            URI uri = new URI(url);
            if (!"https".equalsIgnoreCase(uri.getScheme()) || !HOST.equalsIgnoreCase(uri.getHost())
                    || uri.getRawUserInfo() != null || (uri.getPort() != -1 && uri.getPort() != 443)
                    || uri.getRawFragment() != null) return new Decision(403, null);
            String raw = uri.getRawPath();
            if (raw == null || raw.isEmpty()) raw = "/";
            // A path separator must be literal, not a second interpretation of a URL.
            String lower = raw.toLowerCase(Locale.ROOT);
            if (lower.contains("%2f") || lower.contains("%5c")) return new Decision(403, null);
            String decoded = decode(raw);
            if (!decoded.startsWith("/") || decoded.contains("\\") || decoded.contains("%")) return new Decision(403, null);
            for (int i = 0; i < decoded.length(); i++) {
                char c = decoded.charAt(i);
                if (c < 32 || c == 127) return new Decision(403, null);
            }
            String path = decoded.equals("/") ? "index.html" : decoded.substring(1);
            for (String segment : path.split("/", -1)) {
                if (segment.isEmpty() || segment.equals(".") || segment.equals("..")) return new Decision(403, null);
            }
            return files.contains(path) ? new Decision(200, path) : new Decision(404, null);
        } catch (Exception invalid) {
            return new Decision(403, null);
        }
    }
    private static String decode(String value) throws Exception {
        ByteArrayOutputStream bytes = new ByteArrayOutputStream();
        for (int i = 0; i < value.length();) {
            if (value.charAt(i) == '%') {
                if (i + 2 >= value.length()) throw new IllegalArgumentException("Incomplete escape");
                int a = Character.digit(value.charAt(i + 1), 16), b = Character.digit(value.charAt(i + 2), 16);
                if (a < 0 || b < 0) throw new IllegalArgumentException("Invalid escape");
                bytes.write((a << 4) | b);
                i += 3;
            } else {
                int next = value.indexOf('%', i);
                if (next < 0) next = value.length();
                byte[] part = value.substring(i, next).getBytes(StandardCharsets.UTF_8);
                bytes.write(part, 0, part.length);
                i = next;
            }
        }
        return StandardCharsets.UTF_8.newDecoder().onMalformedInput(CodingErrorAction.REPORT)
                .onUnmappableCharacter(CodingErrorAction.REPORT).decode(ByteBuffer.wrap(bytes.toByteArray())).toString();
    }
    public static final class ByteRange {
        public final int status;
        public final long start, count;
        ByteRange(int status, long start, long count) { this.status = status; this.start = start; this.count = count; }
    }
    public static ByteRange range(String header, long length) {
        if (length < 0) throw new IllegalArgumentException("Negative resource size");
        if (header == null || header.isEmpty()) return new ByteRange(200, 0, length);
        try {
            Matcher m = RANGE.matcher(header.trim());
            if (!m.matches() || length == 0 || (m.group(1).isEmpty() && m.group(2).isEmpty())) return new ByteRange(416, 0, 0);
            long start, end;
            if (m.group(1).isEmpty()) {
                long suffix = Long.parseLong(m.group(2));
                if (suffix <= 0) return new ByteRange(416, 0, 0);
                start = Math.max(0, length - suffix);
                end = length - 1;
            } else {
                start = Long.parseLong(m.group(1));
                end = m.group(2).isEmpty() ? length - 1 : Math.min(Long.parseLong(m.group(2)), length - 1);
            }
            if (start < 0 || start >= length || end < start) return new ByteRange(416, 0, 0);
            return new ByteRange(206, start, end - start + 1);
        } catch (NumberFormatException invalid) {
            return new ByteRange(416, 0, 0);
        }
    }
    public static String mime(String path) {
        String lower = path.toLowerCase(Locale.ROOT);
        if (lower.endsWith(".html")) return "text/html";
        if (lower.endsWith(".js")) return "application/javascript";
        if (lower.endsWith(".css")) return "text/css";
        if (lower.endsWith(".json") || lower.endsWith(".ls") || lower.endsWith(".lh")) return "application/json";
        if (lower.endsWith(".png")) return "image/png";
        if (lower.endsWith(".jpg") || lower.endsWith(".jpeg")) return "image/jpeg";
        if (lower.endsWith(".webp")) return "image/webp";
        if (lower.endsWith(".svg")) return "image/svg+xml";
        if (lower.endsWith(".mp3")) return "audio/mpeg";
        if (lower.endsWith(".wav")) return "audio/wav";
        if (lower.endsWith(".ogg")) return "audio/ogg";
        if (lower.endsWith(".woff2")) return "font/woff2";
        if (lower.endsWith(".ttf")) return "font/ttf";
        if (lower.endsWith(".atlas") || lower.endsWith(".txt") || lower.endsWith(".glsl") || lower.endsWith(".shader")) return "text/plain";
        return "application/octet-stream";
    }
}
