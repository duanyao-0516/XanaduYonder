package com.xanaduyonder.cuttherope;

/** Host JVM tests: elapsed-time Back decisions and the actual Android asset router policy. */
public final class RuntimePolicyTest {
    private static int checks;
    private static void check(boolean value, String message) {
        checks++;
        if (!value) throw new AssertionError(message);
    }
    private static void blocked(String url) {
        check(OfflineAssetPolicy.assetPath(url) == null, "Unexpected permitted URL: " + url);
    }
    public static void main(String[] args) {
        CompletedBackPolicy policy = new CompletedBackPolicy();
        check(!policy.onCompletedBack(0), "First completed Back must only arm");
        check(policy.onCompletedBack(2000), "Second Back at inclusive 2000ms boundary must exit");
        check(!policy.onCompletedBack(2001), "A consumed exit must reset the pair");
        check(!policy.onCompletedBack(4002), "Expired pair must arm a new first Back");
        check(policy.onCompletedBack(4003), "Second event after a newly armed pair must exit");
        check(!policy.onCompletedBack(5000), "First event arms");
        policy.reset();
        check(!policy.onCompletedBack(5001), "Pause/focus/reset cannot leave a stale exit armed");
        check(!policy.onCompletedBack(4999), "Non-monotonic timestamp cannot exit");
        policy.reset();
        // Predictive cancellation never calls onCompletedBack: no state transition to test/implement.
        check(!policy.onCompletedBack(6000), "After cancellation without callback first completion arms");
        check(policy.onCompletedBack(6001), "Two real completions are required");

        String origin = "https://appassets.androidplatform.net";
        check("cut-the-rope/index.html".equals(OfflineAssetPolicy.assetPath(OfflineAssetPolicy.START_URL)), "Index asset path");
        check("cut-the-rope/images/menu.2x.png".equals(OfflineAssetPolicy.assetPath(origin + "/assets/cut-the-rope/images/menu.2x.png?v=1")), "Nested resource with cache query");
        check(OfflineAssetPolicy.isEntryUrl(OfflineAssetPolicy.START_URL), "Entry URL accepted");
        check(!OfflineAssetPolicy.isEntryUrl(OfflineAssetPolicy.START_URL + "?redirect=x"), "Navigation cannot change entry");
        check(!OfflineAssetPolicy.isEntryUrl(origin + "/assets/cut-the-rope/main.js"), "Resource is not a page");
        for (String url : new String[] {
            "http://appassets.androidplatform.net/assets/cut-the-rope/index.html",
            "https://example.com/assets/cut-the-rope/index.html",
            "https://appassets.androidplatform.net.evil.test/assets/cut-the-rope/index.html",
            "https://user@appassets.androidplatform.net/assets/cut-the-rope/index.html",
            "https://appassets.androidplatform.net:443/assets/cut-the-rope/index.html",
            origin + "/assets/other/private.txt", origin + "/assets/cut-the-rope/../private.txt",
            origin + "/assets/cut-the-rope/./index.html", origin + "/assets/cut-the-rope//index.html",
            origin + "/assets/cut-the-rope/%2e%2e/private.txt", origin + "/assets/cut-the-rope/%252e%252e/private.txt",
            origin + "/assets/cut-the-rope/images%2fprivate.txt", origin + "/assets/cut-the-rope/image%00.png",
            origin + "/assets/cut-the-rope/..\\private.txt", origin + "/assets/cut-the-rope/",
            "file:///android_asset/cut-the-rope/index.html", "content://x/index.html", "javascript:alert(1)",
            "//appassets.androidplatform.net/assets/cut-the-rope/index.html", "not a URI", "", null
        }) blocked(url);
        check("text/html".equals(OfflineAssetPolicy.mimeType("index.html")), "HTML MIME");
        check("application/javascript".equals(OfflineAssetPolicy.mimeType("game.js")), "JavaScript MIME");
        check("image/png".equals(OfflineAssetPolicy.mimeType("images/a.PNG")), "PNG MIME");
        check("audio/mp4".equals(OfflineAssetPolicy.mimeType("audio/a.m4a")), "Audio MIME");
        check("application/octet-stream".equals(OfflineAssetPolicy.mimeType("a.unknown")), "Unknown binary MIME");
        System.out.println("PASS: " + checks + " runtime policy assertions");
    }
}
