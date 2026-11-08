package com.xanaduyonder.bombit;

import java.net.URI;
import java.net.URISyntaxException;
import java.util.Set;

/** Exact packaged-file routing, shared by navigation checks and resource interception. */
final class BombItAssetPolicy {
    static final String HOST = "bomb-it.xanaduyonder.invalid";
    static final String ORIGIN = "https://" + HOST;
    static final String PREFIX = "/assets/bomb-it/";
    static final String ENTRY = ORIGIN + PREFIX + "index.html";

    static boolean validRelativePath(String path) {
        if (path == null || path.length() == 0 || path.length() > 256
                || !path.matches("[A-Za-z0-9][A-Za-z0-9 _./-]*")) return false;
        for (String part : path.split("/", -1)) {
            if (part.length() == 0 || part.equals(".") || part.equals("..")) return false;
        }
        return true;
    }

    static String pathFor(String url, String method, Set<String> files) {
        if (!"GET".equals(method) || url == null || url.length() > 1024) return null;
        try {
            URI uri = new URI(url);
            if (!"https".equals(uri.getScheme()) || !HOST.equals(uri.getHost())
                    || uri.getPort() != -1 || uri.getRawUserInfo() != null
                    || uri.getRawQuery() != null || uri.getRawFragment() != null) return null;
            String path = uri.getPath();
            if (path == null || !path.startsWith(PREFIX)) return null;
            // Decode exactly once, then require one canonical spelling. Encoded slashes,
            // dot segments, double encoding and alternate authorities never reach assets.
            if (!url.equals(new URI("https", HOST, path, null, null).toASCIIString())) return null;
            String relative = path.substring(PREFIX.length());
            return validRelativePath(relative) && files.contains(relative) ? relative : null;
        } catch (URISyntaxException | IllegalArgumentException error) {
            return null;
        }
    }
}
