package com.xanaduyonder.bombit;

import android.app.Activity;
import android.content.Intent;
import android.content.Context;
import android.os.Handler;
import android.os.Looper;
import android.os.SystemClock;
import android.util.Log;
import java.lang.ref.WeakReference;
import org.json.JSONObject;

/** Unity owns only this launch/return contract; each Android game session owns its state. */
public final class BombItBridge {
    static final String SESSION_EXTRA = "bombItSession";
    private static final Handler MAIN = new Handler(Looper.getMainLooper());
    private static volatile boolean running;
    private static volatile String lastError = "";
    private static long session;
    private static WeakReference<BombItActivity> activity = new WeakReference<>(null);

    private BombItBridge() { }

    public static synchronized boolean open(Activity host, boolean soundEnabled, boolean hapticsEnabled) {
        if (running || host == null || host.isFinishing() || host.isDestroyed()) return false;
        final long token = ++session;
        final long requestedAt = SystemClock.elapsedRealtime();
        running = true;
        lastError = "";
        Log.i("BombIt", "session=" + token + " event=launch_queued elapsed=" + requestedAt);
        boolean queued = MAIN.post(() -> {
            synchronized (BombItBridge.class) {
                if (!running || session != token) return;
            }
            try {
                Log.i("BombIt", "session=" + token + " event=launch_dispatch elapsed="
                        + SystemClock.elapsedRealtime() + " queueMs=" + (SystemClock.elapsedRealtime() - requestedAt));
                if (host.isFinishing() || host.isDestroyed()) throw new IllegalStateException("Host unavailable");
                Intent intent = new Intent(host, BombItActivity.class);
                intent.putExtra(SESSION_EXTRA, token);
                intent.putExtra("soundEnabled", soundEnabled);
                intent.putExtra("hapticsEnabled", hapticsEnabled);
                host.startActivity(intent);
            } catch (RuntimeException | LinkageError error) {
                launchFailed(token, error);
            }
        });
        if (!queued) launchFailed(token, new IllegalStateException("Main looper rejected game launch"));
        return queued;
    }

    public static boolean isRunning() { return running; }
    public static String getLastError() { return lastError; }

    // Compatibility wrappers; both web games now write one ordered journal.
    static JSONObject pendingFeedback(Context context) {
        return com.xanaduyonder.webgames.WebGameFeedbackPreferences.readPending(context);
    }
    public static String readPendingFeedback(Activity host) {
        return com.xanaduyonder.webgames.WebGameFeedbackPreferences.readPendingFeedback(host);
    }
    static boolean savePendingFeedback(Context context, boolean sound, boolean haptics) {
        return com.xanaduyonder.webgames.WebGameFeedbackPreferences.write(context, "bomb-it", sound, haptics);
    }
    public static boolean acknowledgePendingFeedback(Activity host, String revision) {
        return com.xanaduyonder.webgames.WebGameFeedbackPreferences.acknowledgePendingFeedback(host, revision);
    }

    static synchronized long attach(BombItActivity instance, long requestedSession) {
        BombItActivity previous = activity.get();
        if (running) {
            if (requestedSession != session) return -1;
            if (previous != null && previous != instance && !previous.isFinishing()
                    && !previous.isDestroyed() && !previous.isChangingConfigurations()) return -1;
        } else {
            // Android can restore the game Activity after the entire process was reclaimed.
            session++;
            running = true;
            lastError = "";
        }
        activity = new WeakReference<>(instance);
        return session;
    }

    static synchronized void reportError(long token, BombItActivity instance, Throwable error) {
        if (session != token || activity.get() != instance) return;
        lastError = "Q版泡泡堂暂时无法打开，请返回大厅后重试。";
        Log.e("BombIt", "Local game host failed", error);
    }

    static synchronized void stopped(long token, BombItActivity instance) {
        if (session != token || activity.get() != instance) return;
        activity.clear();
        running = false;
    }

    private static synchronized void launchFailed(long token, Throwable error) {
        if (session != token) return;
        lastError = "Q版泡泡堂暂时无法打开，请稍后重试。";
        running = false;
        activity.clear();
        Log.e("BombIt", "Unable to launch local game", error);
    }
}
