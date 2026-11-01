package com.xanaduyonder.zhaoyunadou;

import android.content.res.AssetManager;
import android.webkit.WebResourceRequest;
import android.webkit.WebResourceResponse;
import java.io.ByteArrayInputStream;
import java.io.ByteArrayOutputStream;
import java.io.FilterInputStream;
import java.io.IOException;
import java.io.InputStream;
import java.nio.charset.StandardCharsets;
import java.util.Collections;
import java.util.HashMap;
import java.util.Map;
import org.json.JSONArray;
import org.json.JSONObject;

/** Every request gets an explicit response. A miss must never fall back to the network. */
final class LocalAssetServer {
    private static final String ROOT = "zhaoyun-adou/";
    private final AssetManager assets;
    private final Map<String, Long> lengths;
    LocalAssetServer(AssetManager assets) throws Exception {
        this.assets = assets;
        Map<String, Long> parsed = new HashMap<>();
        try (InputStream stream = assets.open("zhaoyun-adou-host/asset-index.json")) {
            ByteArrayOutputStream data = new ByteArrayOutputStream();
            byte[] buffer = new byte[8192];
            int size;
            while ((size = stream.read(buffer)) != -1) {
                if (data.size() + size > 2 * 1024 * 1024) throw new IOException("资源清单过大");
                data.write(buffer, 0, size);
            }
            JSONArray rows = new JSONObject(new String(data.toByteArray(), StandardCharsets.UTF_8)).getJSONArray("files");
            for (int i = 0; i < rows.length(); i++) {
                JSONObject row = rows.getJSONObject(i);
                String path = row.getString("path");
                long length = row.getLong("bytes");
                if (length < 0 || parsed.put(path, length) != null) throw new IOException("资源清单重复或大小无效");
                if (AssetRequestPolicy.resolve(new java.net.URI("https", AssetRequestPolicy.HOST, "/" + path, null).toASCIIString(), "GET", parsed.keySet()).status != 200)
                    throw new IOException("资源清单路径无效");
            }
        }
        if (!parsed.containsKey("index.html")) throw new IOException("离线游戏入口缺失");
        lengths = Collections.unmodifiableMap(parsed);
    }
    WebResourceResponse serve(WebResourceRequest request) {
        String range = null;
        for (Map.Entry<String, String> header : request.getRequestHeaders().entrySet())
            if ("Range".equalsIgnoreCase(header.getKey())) range = header.getValue();
        return serve(request.getUrl().toString(), request.getMethod(), range);
    }
    WebResourceResponse serve(String url, String method, String rangeHeader) {
        AssetRequestPolicy.Decision decision = AssetRequestPolicy.resolve(url, method, lengths.keySet());
        if (decision.status != 200) return error(decision.status, method, -1);
        long length = lengths.get(decision.path);
        AssetRequestPolicy.ByteRange range = AssetRequestPolicy.range(rangeHeader, length);
        if (range.status == 416) return error(416, method, length);
        Map<String, String> headers = headers();
        headers.put("Content-Length", Long.toString(range.count));
        headers.put("Accept-Ranges", "bytes");
        if (range.status == 206) headers.put("Content-Range", "bytes " + range.start + "-" + (range.start + range.count - 1) + "/" + length);
        String mime = AssetRequestPolicy.mime(decision.path);
        InputStream stream = null;
        try {
            if ("HEAD".equals(method)) stream = new ByteArrayInputStream(new byte[0]);
            else {
                stream = assets.open(ROOT + decision.path, AssetManager.ACCESS_STREAMING);
                skipFully(stream, range.start);
                stream = new LimitedInputStream(stream, range.count);
            }
            return new WebResourceResponse(mime, isText(mime) ? "UTF-8" : null, range.status,
                    range.status == 206 ? "Partial Content" : "OK", headers, stream);
        } catch (IOException missing) {
            if (stream != null) try { stream.close(); } catch (IOException ignored) { }
            return error(404, method, -1);
        }
    }
    private static boolean isText(String mime) {
        return mime.startsWith("text/") || mime.equals("application/json") || mime.equals("application/javascript") || mime.equals("image/svg+xml");
    }
    private static Map<String, String> headers() {
        Map<String, String> result = new HashMap<>();
        result.put("Cache-Control", "no-store");
        result.put("X-Content-Type-Options", "nosniff");
        result.put("Referrer-Policy", "no-referrer");
        return result;
    }
    private static WebResourceResponse error(int status, String method, long length) {
        String reason = status == 403 ? "Forbidden" : status == 405 ? "Method Not Allowed" : status == 416 ? "Range Not Satisfiable" : "Not Found";
        byte[] text = reason.getBytes(StandardCharsets.UTF_8);
        Map<String, String> headers = headers();
        headers.put("Content-Length", Integer.toString(text.length));
        if (status == 405) headers.put("Allow", "GET, HEAD");
        if (status == 416) headers.put("Content-Range", "bytes */" + length);
        return new WebResourceResponse("text/plain", "UTF-8", status, reason, headers,
                new ByteArrayInputStream("HEAD".equals(method) ? new byte[0] : text));
    }
    private static void skipFully(InputStream stream, long length) throws IOException {
        long remaining = length;
        while (remaining > 0) {
            long skipped = stream.skip(remaining);
            if (skipped > 0) remaining -= skipped;
            else if (stream.read() != -1) remaining--;
            else throw new IOException("资源短于登记大小");
        }
    }
    private static final class LimitedInputStream extends FilterInputStream {
        private long remaining;
        LimitedInputStream(InputStream stream, long length) { super(stream); remaining = length; }
        @Override public int read() throws IOException {
            if (remaining == 0) return -1;
            int value = super.read();
            if (value < 0) throw new IOException("资源短于登记大小");
            remaining--;
            return value;
        }
        @Override public int read(byte[] buffer, int offset, int count) throws IOException {
            if (count == 0) return 0;
            if (remaining == 0) return -1;
            int read = super.read(buffer, offset, (int) Math.min(count, remaining));
            if (read < 0) throw new IOException("资源短于登记大小");
            remaining -= read;
            return read;
        }
        @Override public long skip(long count) throws IOException {
            long skipped = super.skip(Math.min(Math.max(0, count), remaining));
            remaining -= skipped;
            return skipped;
        }
        @Override public int available() throws IOException { return (int) Math.min(super.available(), remaining); }
    }
}
