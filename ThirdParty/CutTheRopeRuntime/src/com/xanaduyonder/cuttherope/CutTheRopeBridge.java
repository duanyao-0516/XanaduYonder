package com.xanaduyonder.cuttherope;

import android.app.Activity;
import android.content.Intent;
import android.os.Handler;
import android.os.Looper;
import android.util.Log;

/** Unity-facing launch state; game UI, WebView, and persistence belong to the game Activity. */
public final class CutTheRopeBridge {
    private static volatile boolean running;
    private static volatile String lastError = "";
    private static Activity current;

    private CutTheRopeBridge() { }

    public static synchronized boolean open(Activity host, boolean soundEnabled) {
        if (running || host == null || host.isFinishing() || host.isDestroyed()) return false;
        running = true;
        lastError = "";
        boolean queued = new Handler(Looper.getMainLooper()).post(() -> {
            try {
                if (host.isFinishing() || host.isDestroyed()) {
                    throw new IllegalStateException("The game hall is no longer available");
                }
                Intent intent = new Intent(host, CutTheRopeActivity.class);
                intent.putExtra("soundEnabled", soundEnabled);
                host.startActivity(intent);
            } catch (RuntimeException | LinkageError error) {
                reportError("割绳子启动失败：" + error);
                launchFailed();
            }
        });
        if (!queued) {
            reportError("割绳子启动失败：Android 主线程不可用");
            running = false;
        }
        return queued;
    }

    public static boolean isRunning() { return running; }
    public static String getLastError() { return lastError; }

    static synchronized boolean started(Activity activity) {
        if (current != null && current != activity) return false;
        current = activity;
        running = true;
        return true;
    }

    static synchronized void stopped(Activity activity) {
        if (current != activity) return;
        current = null;
        running = false;
    }

    private static synchronized void launchFailed() {
        if (current == null) running = false;
    }

    static void reportError(String message) {
        lastError = message == null || message.isEmpty() ? "割绳子运行时发生错误" : message;
        Log.e("CutTheRope", lastError);
    }
}
