package com.xanaduyonder.classicgoldminer;

import android.app.Activity;
import android.content.Intent;
import android.os.Handler;
import android.os.Looper;
import java.lang.ref.WeakReference;

/** Unity JNI entry point. The Unity Activity remains underneath the game. */
public final class ClassicGoldMinerBridge {
    private static final Handler MAIN = new Handler(Looper.getMainLooper());
    private static boolean running;
    private static long generation;
    private static String lastError = "";
    private static WeakReference<ClassicGoldMinerActivity> current = new WeakReference<>(null);
    private ClassicGoldMinerBridge() { }

    public static synchronized boolean open(Activity owner, boolean sound, boolean haptics) {
        if (running) return true;
        if (owner == null || owner.isFinishing() || owner.isDestroyed()) {
            lastError = "大厅已关闭，无法打开经典黄金矿工。";
            return false;
        }
        running = true;
        lastError = "";
        final long launch = ++generation;
        WeakReference<Activity> weakOwner = new WeakReference<>(owner);
        MAIN.post(() -> {
            Activity activity = weakOwner.get();
            synchronized (ClassicGoldMinerBridge.class) {
                if (!running || generation != launch) return;
            }
            try {
                if (activity == null || activity.isFinishing() || activity.isDestroyed()) throw new IllegalStateException("大厅不可用");
                Intent intent = new Intent(activity, ClassicGoldMinerActivity.class);
                intent.putExtra("launch", launch);
                intent.putExtra("sound", sound);
                intent.putExtra("haptics", haptics);
                activity.startActivity(intent);
                MAIN.postDelayed(() -> launchTimeout(launch), 15000);
            } catch (RuntimeException error) {
                failLaunch(launch, "无法打开系统 WebView：" + error.getClass().getSimpleName());
            }
        });
        return true;
    }

    public static synchronized boolean isRunning() { return running; }
    public static synchronized String getLastError() { return lastError; }

    static synchronized boolean started(ClassicGoldMinerActivity activity, long launch) {
        // A process may be recreated directly into this Activity after Android reclaimed it.
        if (!running && current.get() == null && launch >= generation) {
            generation = launch;
            running = true;
        }
        if (!running || launch != generation) return false;
        current = new WeakReference<>(activity);
        return true;
    }

    static synchronized void ended(ClassicGoldMinerActivity activity) {
        if (current.get() == activity) {
            current.clear();
            running = false;
        }
    }

    static synchronized void reportError(String message) { lastError = message; }

    private static synchronized void launchTimeout(long launch) {
        if (running && generation == launch && current.get() == null) {
            failLaunch(launch, "系统 WebView 启动超时，请重试。");
        }
    }

    private static synchronized void failLaunch(long launch, String message) {
        if (generation == launch) {
            lastError = message;
            running = false;
        }
    }
}
