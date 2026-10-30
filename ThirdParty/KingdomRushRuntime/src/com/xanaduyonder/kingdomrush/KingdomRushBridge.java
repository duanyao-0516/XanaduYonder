package com.xanaduyonder.kingdomrush;

import android.app.Activity;
import android.content.Intent;
import android.os.Handler;
import android.os.Looper;
import android.util.Log;

/** Unity-facing launch contract. All game and SDL state remains in its Activity. */
public final class KingdomRushBridge {
    private static volatile boolean running;
    private static volatile String lastError = "";

    private KingdomRushBridge() { }

    public static synchronized boolean open(Activity host, boolean soundEnabled, boolean hapticsEnabled) {
        if (running || host == null || host.isFinishing() || host.isDestroyed()) return false;
        running = true;
        lastError = "";
        boolean queued = new Handler(Looper.getMainLooper()).post(() -> {
            try {
                if (host.isFinishing() || host.isDestroyed()) {
                    throw new IllegalStateException("The game hall is no longer available");
                }
                Intent intent = new Intent(host, KingdomRushActivity.class);
                intent.putExtra("soundEnabled", soundEnabled);
                intent.putExtra("hapticsEnabled", hapticsEnabled);
                host.startActivity(intent);
            } catch (RuntimeException | LinkageError error) {
                reportError("无法打开王国保卫战：" + error.getMessage());
                stopped();
            }
        });
        if (!queued) {
            reportError("无法打开王国保卫战：Android 主线程不可用");
            running = false;
        }
        return queued;
    }

    public static boolean isRunning() { return running; }
    public static String getLastError() { return lastError; }

    static void started() { running = true; }
    static void stopped() { running = false; }
    static void reportError(String message) {
        lastError = message == null ? "王国保卫战运行时发生错误" : message;
        Log.e("KingdomRush", lastError);
    }
}
