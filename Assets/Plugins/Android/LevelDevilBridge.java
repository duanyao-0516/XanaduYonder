package com.xanaduyonder.leveldevil;

import android.app.Activity;
import android.content.Intent;
import android.content.Context;
import org.json.JSONObject;
import android.os.Handler;
import android.os.Looper;
import android.os.SystemClock;
import android.util.Log;
import java.lang.ref.WeakReference;

/** Unity owns only this launch/return contract; each Android game session owns its state. */
public final class LevelDevilBridge {
    static final String SESSION_EXTRA = "levelDevilSession";
    private static final Handler MAIN = new Handler(Looper.getMainLooper());
    private static volatile boolean running;
    private static volatile String lastError = "";
    private static long session;
    private static WeakReference<LevelDevilActivity> activity = new WeakReference<>(null);

    private LevelDevilBridge() { }

    public static synchronized boolean open(Activity host, boolean soundEnabled, boolean hapticsEnabled) {
        if (running || host == null || host.isFinishing() || host.isDestroyed()) return false;
        final long token = ++session;
        final long requestedAt = SystemClock.elapsedRealtime();
        running = true;
        lastError = "";
        Log.i("LevelDevil", "session=" + token + " event=launch_queued elapsed=" + requestedAt);
        boolean queued = MAIN.post(() -> {
            synchronized (LevelDevilBridge.class) {
                if (!running || session != token) return;
            }
            try {
                Log.i("LevelDevil", "session=" + token + " event=launch_dispatch elapsed="
                        + SystemClock.elapsedRealtime() + " queueMs=" + (SystemClock.elapsedRealtime() - requestedAt));
                if (host.isFinishing() || host.isDestroyed()) throw new IllegalStateException("Host unavailable");
                Intent intent = new Intent(host, LevelDevilActivity.class);
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

    static JSONObject pendingFeedback(Context context) {
        return com.xanaduyonder.webgames.WebGameFeedbackPreferences.readPending(context);
    }
    static boolean savePendingFeedback(Context context, boolean sound, boolean haptics) {
        return com.xanaduyonder.webgames.WebGameFeedbackPreferences.write(context, "level-devil", sound, haptics);
    }

    static synchronized long attach(LevelDevilActivity instance, long requestedSession) {
        LevelDevilActivity previous = activity.get();
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

    static synchronized void reportError(long token, LevelDevilActivity instance, Throwable error) {
        if (session != token || activity.get() != instance) return;
        lastError = "只有一道门暂时无法打开，请返回大厅后重试。";
        Log.e("LevelDevil", "Local game host failed", error);
    }

    static synchronized void stopped(long token, LevelDevilActivity instance) {
        if (session != token || activity.get() != instance) return;
        activity.clear();
        running = false;
    }

    private static synchronized void launchFailed(long token, Throwable error) {
        if (session != token) return;
        lastError = "只有一道门暂时无法打开，请稍后重试。";
        running = false;
        activity.clear();
        Log.e("LevelDevil", "Unable to launch local game", error);
    }
}
