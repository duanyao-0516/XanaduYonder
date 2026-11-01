package com.xanaduyonder.fruitslice;

import android.app.Activity;
import android.content.Intent;
import android.os.SystemClock;
import android.util.Log;

/** The lobby remains beneath a single internal Activity; a token rejects stale teardown. */
public final class FruitSliceBridge {
    private static boolean running;
    private static long session;
    private static String lastError = "";

    private FruitSliceBridge() { }

    public static synchronized boolean open(Activity parent, boolean sound, boolean haptics) {
        if (running) return true;
        if (parent == null || parent.isFinishing() || parent.isDestroyed()) return false;
        running = true;
        lastError = "";
        final long token = ++session;
        final long requestedAt = SystemClock.elapsedRealtime();
        Log.i("FruitSliceStartup", "session=" + token + " stage=open_requested atMs=" + requestedAt);
        try {
            parent.runOnUiThread(() -> {
                try {
                    if (!isCurrent(token)) return;
                    if (parent.isFinishing() || parent.isDestroyed()) throw new IllegalStateException("Lobby is closing");
                    Intent intent = new Intent(parent, FruitSliceActivity.class);
                    intent.putExtra("session", token);
                    intent.putExtra("sound", sound);
                    intent.putExtra("startup_requested_at", requestedAt);
                    Log.i("FruitSliceStartup", "session=" + token + " stage=start_activity elapsedMs=" +
                        (SystemClock.elapsedRealtime() - requestedAt));
                    parent.startActivity(intent);
                } catch (RuntimeException | LinkageError error) {
                    failed(token, error.toString());
                    stopped(token);
                }
            });
        } catch (RuntimeException | LinkageError error) {
            failed(token, error.toString());
            stopped(token);
            return false;
        }
        return true;
    }

    public static synchronized boolean isRunning() { return running; }
    public static synchronized String getLastError() { return lastError; }
    static synchronized boolean isCurrent(long token) { return running && token == session; }
    static synchronized void failed(long token, String message) {
        if (token == session) lastError = message;
    }
    static synchronized void stopped(long token) {
        if (token == session) running = false;
    }
}
