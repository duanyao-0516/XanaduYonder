package com.xanaduyonder.zhaoyunadou;

import android.app.Activity;
import android.content.Intent;
import android.os.Handler;
import android.os.Looper;
import android.util.Log;

/** Unity owns the hall; this bridge owns only one queued/running Android session. */
public final class ZhaoyunAdouBridge {
    static final String SESSION = "zhaoyunAdouSession";
    private static volatile boolean running;
    private static volatile String lastError = "";
    private static long session;
    private static boolean attached;
    private ZhaoyunAdouBridge() { }

    public static synchronized boolean open(Activity host, boolean soundEnabled, boolean hapticsEnabled) {
        if (running || host == null || host.isFinishing() || host.isDestroyed()) return false;
        final long token = ++session;
        running = true;
        attached = false;
        lastError = "";
        boolean queued = new Handler(Looper.getMainLooper()).post(() -> {
            try {
                if (host.isFinishing() || host.isDestroyed()) throw new IllegalStateException("游戏大厅已关闭");
                Intent intent = new Intent(host, ZhaoyunAdouActivity.class);
                intent.putExtra(SESSION, token);
                intent.putExtra("soundEnabled", soundEnabled);
                // H5 has no native haptic capability or permission. Keep the public launch signature compatible.
                host.startActivity(intent);
            } catch (RuntimeException | LinkageError error) {
                reportError(token, "赵云与阿斗启动失败：" + error.getMessage());
                stopped(token);
            }
        });
        if (!queued) {
            reportError(token, "Android 主线程不可用");
            stopped(token);
        }
        return queued;
    }
    public static boolean isRunning() { return running; }
    public static String getLastError() { return lastError; }
    static synchronized boolean started(long token) {
        // Android may recreate the task after process death. That is a new WebView with persisted long-term data.
        if (!running && !attached) { session = token; running = true; lastError = ""; }
        if (token != session || attached) return false;
        attached = true;
        return true;
    }
    static synchronized void stopped(long token) {
        if (token == session) { attached = false; running = false; }
    }
    static synchronized void reportError(long token, String message) {
        if (token != session) return;
        lastError = message == null ? "赵云与阿斗宿主发生错误" : message;
        Log.e("ZhaoyunAdou", lastError);
    }
}
