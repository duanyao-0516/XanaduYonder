package com.xanaduyonder.zhaoyunadou;

import java.util.Arrays;
import java.util.HashSet;
import java.util.Set;

public final class HostBoundaryTest {
    private static int checks;
    private static final String BASE = AssetRequestPolicy.ORIGIN;
    private static final Set<String> FILES = new HashSet<>(Arrays.asList("index.html", "resources/music/main.mp3", "scene/MainScene.ls", "resources/img/a b.png"));
    private static void check(boolean value, String description) {
        checks++;
        if (!value) throw new AssertionError(description);
    }
    private static void request(String url, String method, int status, String path) {
        AssetRequestPolicy.Decision result = AssetRequestPolicy.resolve(url, method, FILES);
        check(result.status == status, method + " " + url + " expected " + status + " got " + result.status);
        check(path == null ? result.path == null : path.equals(result.path), "resolved path: " + url);
    }
    private static void range(String header, long length, int status, long start, long count) {
        AssetRequestPolicy.ByteRange result = AssetRequestPolicy.range(header, length);
        check(result.status == status && result.start == start && result.count == count, "range: " + header);
    }
    public static void main(String[] args) {
        request(BASE, "GET", 200, "index.html");
        request(BASE + "scene/MainScene.ls?v=1", "HEAD", 200, "scene/MainScene.ls");
        request(BASE + "resources/img/a%20b.png", "GET", 200, "resources/img/a b.png");
        request(BASE + "not-listed.png", "GET", 404, null);
        request(BASE + "index.html", "POST", 405, null);
        for (String url : Arrays.asList("http://zhaoyun-adou.xanadu.invalid/index.html", "https://example.org/index.html",
                "file:///android_asset/index.html", "content://anything", "data:text/html,hello", "javascript:alert(1)",
                "https://user@zhaoyun-adou.xanadu.invalid/index.html", "https://zhaoyun-adou.xanadu.invalid:8443/index.html",
                BASE + "../index.html", BASE + "%2e%2e/index.html", BASE + "x/%2E%2E/index.html", BASE + "%252e%252e/index.html",
                BASE + "resources%2Fmusic/main.mp3", BASE + "resources%5cmusic/main.mp3", BASE + "//index.html",
                BASE + "index.html%00", BASE + "index.html#x", BASE + "%C0%AE/index.html", BASE + "%zz")) {
            request(url, "GET", 403, null);
        }
        range(null, 100, 200, 0, 100);
        range("bytes=0-9", 100, 206, 0, 10);
        range("bytes=10-", 100, 206, 10, 90);
        range("bytes=-10", 100, 206, 90, 10);
        range("bytes=-1000", 100, 206, 0, 100);
        range("bytes=50-1000", 100, 206, 50, 50);
        for (String value : Arrays.asList("bytes=100-", "bytes=9-1", "bytes=-0", "bytes=", "bytes=1-3,5-7", "items=1-3", "bytes=9999999999999999999999-"))
            range(value, 100, 416, 0, 0);
        range("bytes=0-", 0, 416, 0, 0);
        check(AssetRequestPolicy.mime("scene/MainScene.ls").equals("application/json"), "scene MIME");
        check(AssetRequestPolicy.mime("resources/music/main.mp3").equals("audio/mpeg"), "audio MIME");

        BackPressGate back = new BackPressGate(2000);
        check(!back.commit(0), "first return at uptime zero prompts");
        check(back.commit(1999), "second committed return inside window exits");
        check(!back.commit(2000), "exit consumes first return");
        check(back.commit(4000), "exact two seconds accepted");
        check(!back.commit(10000), "first return prompts");
        check(!back.commit(12001), "expired first return is replaced");
        check(back.commit(12002), "next after replacement exits");
        check(!back.commit(20000), "first before focus loss");
        back.reset();
        check(!back.commit(20001), "focus loss resets first return");
        back.reset();
        // Predictive gesture start/progress/cancel deliberately never call commit.
        check(!back.commit(30000), "cancelled gesture did not count");
        back.reset();
        check(!back.commit(Long.MAX_VALUE - 5), "large uptime first return");
        check(back.commit(Long.MAX_VALUE - 1), "large uptime without overflow");
        check(!back.commit(300), "restart first return");
        check(!back.commit(200), "backward clock does not exit");

        CallbackEpoch epoch = new CallbackEpoch();
        long first = epoch.next();
        check(epoch.isCurrent(first), "new operation callback accepted");
        long second = epoch.next();
        check(!epoch.isCurrent(first) && epoch.isCurrent(second), "superseded callbacks rejected");
        epoch.close();
        check(!epoch.isCurrent(second), "destroy rejects callbacks");
        check(!epoch.isCurrent(epoch.next()), "destroy cannot revive epoch");
        ForegroundBudget startup = new ForegroundBudget(60000);
        startup.reset(1000, false);
        check(startup.remaining(121000) == 60000, "initial background time does not spend startup budget");
        startup.setForeground(true, 121000);
        check(startup.remaining(131000) == 50000, "foreground loading spends budget");
        startup.setForeground(false, 131000);
        check(startup.remaining(1000000) == 50000, "long lock screen preserves remaining budget");
        startup.setForeground(true, 1000000);
        startup.setForeground(true, 1010000);
        check(startup.remaining(1020000) == 30000, "duplicate focus callback cannot reset deadline");
        startup.setForeground(false, 1020000);
        startup.setForeground(false, 1030000);
        check(startup.remaining(2000000) == 30000, "duplicate pause does not spend time twice");
        startup.setForeground(true, 2000000);
        check(startup.remaining(2030000) == 0, "sixty foreground seconds reach timeout");
        startup.reset(3000000, true);
        check(startup.remaining(3000000) == 60000, "explicit reload receives fresh budget");
        check(startup.remaining(2999999) == 60000, "backward clock cannot create negative elapsed time");
        check(startup.remaining(Long.MAX_VALUE) == 0, "large timestamp saturates remaining budget");
        System.out.println("HOST_BOUNDARIES_PASS: " + checks + " assertions");
    }
}
