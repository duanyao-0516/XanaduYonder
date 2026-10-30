package com.xanaduyonder.kingdomrush;

import android.app.ActivityManager;
import android.content.Context;
import android.os.Build;
import android.os.Bundle;
import android.telephony.TelephonyManager;
import android.util.DisplayMetrics;
import android.view.KeyEvent;
import android.view.ViewConfiguration;
import android.view.WindowManager;
import java.io.File;
import java.util.Locale;
import org.love2d.android.GameActivity;

/** Internal landscape Activity. Its normal exit is love.event.quit(), then finish(). */
public final class KingdomRushActivity extends GameActivity {
    private boolean hapticsEnabled;
    private boolean soundEnabled;

    @Override protected void onCreate(Bundle savedState) {
        soundEnabled = getIntent().getBooleanExtra("soundEnabled", true);
        hapticsEnabled = getIntent().getBooleanExtra("hapticsEnabled", true);
        KingdomRushBridge.started();
        try {
            super.onCreate(savedState);
            if (isRuntimeEntryRejected()) return;
            if (mBrokenLibraries) {
                KingdomRushBridge.reportError("王国保卫战运行时加载失败，请查看 Android 日志");
                finish();
                return;
            }
            // This runs after Activity.onCreate and before SDL_main starts in onResume.
            getFilesDir();
            getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
        } catch (RuntimeException | LinkageError error) {
            KingdomRushBridge.reportError("王国保卫战启动失败：" + error.toString());
            mBrokenLibraries = true;
            finish();
        }
    }

    @Override public void loadLibraries() {
        super.loadLibraries();
        // Set before SDL's recreation guard, including retry after a failed first launch.
        nativeSetenv("SDL_ANDROID_ALLOW_RECREATE_ACTIVITY", "1");
    }

    @Override public File getFilesDir() {
        File directory = new File(super.getFilesDir(), "kingdom-rush-1");
        if (!directory.isDirectory() && !directory.mkdirs() && !directory.isDirectory()) {
            throw new IllegalStateException("Cannot create Kingdom Rush save directory");
        }
        File saveParent = new File(directory, "save");
        if (!saveParent.isDirectory() && !saveParent.mkdirs() && !saveParent.isDirectory()) {
            throw new IllegalStateException("Cannot create Kingdom Rush save parent directory");
        }
        return directory;
    }

    @Override public boolean dispatchKeyEvent(KeyEvent event) {
        if (isRuntimeEntryRejected()) return super.dispatchKeyEvent(event);
        if (event.getKeyCode() == KeyEvent.KEYCODE_BACK && !mBrokenLibraries) {
            if (event.getAction() == KeyEvent.ACTION_DOWN && event.getRepeatCount() == 0) {
                onNativeKeyDown(KeyEvent.KEYCODE_ESCAPE);
            } else if (event.getAction() == KeyEvent.ACTION_UP) {
                onNativeKeyUp(KeyEvent.KEYCODE_ESCAPE);
            }
            return true;
        }
        return super.dispatchKeyEvent(event);
    }

    @Override public void onBackPressed() {
        if (isRuntimeEntryRejected()) { finish(); return; }
        if (mBrokenLibraries) { finish(); return; }
        onNativeKeyDown(KeyEvent.KEYCODE_ESCAPE);
        onNativeKeyUp(KeyEvent.KEYCODE_ESCAPE);
    }

    @Override public void vibrate(double seconds) {
        if (hapticsEnabled && seconds > 0) super.vibrate(seconds);
    }

    @Override protected void onNativeRuntimeStopped() {
        KingdomRushBridge.stopped();
    }

    @Override protected void onNativeRuntimeEntryRejected() {
        KingdomRushBridge.reportError("上一局正在退出，请稍后从大厅重新进入王国保卫战");
    }

    /** Called by the real Lua JNI module. Unknown fields are errors, never empty defaults. */
    public String getSystemProperty(String name) {
        DisplayMetrics metrics = getResources().getDisplayMetrics();
        switch (name) {
            case "HAS_PERMANENT_MENU_KEY": return Boolean.toString(ViewConfiguration.get(this).hasPermanentMenuKey());
            case "API_LEVEL": return Integer.toString(Build.VERSION.SDK_INT);
            case "CURRENT_LOCALE": return Locale.getDefault().toString();
            case "PHONE_TYPE": {
                TelephonyManager telephony = (TelephonyManager) getSystemService(Context.TELEPHONY_SERVICE);
                if (telephony == null) throw new IllegalStateException("Telephony service unavailable");
                return Integer.toString(telephony.getPhoneType());
            }
            case "DEVICE_MODEL": return Build.MODEL;
            case "DEVICE_CPU_CORES": return Integer.toString(Runtime.getRuntime().availableProcessors());
            case "DEVICE_RAM": {
                ActivityManager.MemoryInfo info = new ActivityManager.MemoryInfo();
                ((ActivityManager) getSystemService(Context.ACTIVITY_SERVICE)).getMemoryInfo(info);
                return Long.toString(info.totalMem / (1024 * 1024));
            }
            case "DENSITY_DPI": return Integer.toString(metrics.densityDpi);
            case "X_DPI": return Float.toString(metrics.xdpi);
            case "Y_DPI": return Float.toString(metrics.ydpi);
            case "DIAGONAL_SIZE_INCHES": {
                DisplayMetrics real = new DisplayMetrics();
                getWindowManager().getDefaultDisplay().getRealMetrics(real);
                if (real.xdpi <= 0 || real.ydpi <= 0) throw new IllegalStateException("Display DPI unavailable");
                return Double.toString(Math.hypot(real.widthPixels / (double) real.xdpi, real.heightPixels / (double) real.ydpi));
            }
            case "SOUND_ENABLED": return Boolean.toString(soundEnabled);
            case "HAPTICS_ENABLED": return Boolean.toString(hapticsEnabled);
            default: throw new IllegalArgumentException("Unknown Android system property: " + name);
        }
    }

    public void reportGameError(String message) { KingdomRushBridge.reportError(message); }
}
