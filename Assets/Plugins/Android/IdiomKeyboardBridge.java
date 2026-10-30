package com.duanyao.xanaduyonder;

import android.app.Activity;
import android.app.Application;
import android.os.Build;
import android.os.Bundle;
import android.graphics.Rect;
import android.view.View;
import android.view.WindowInsets;
import android.view.inputmethod.InputMethodManager;
import java.util.concurrent.atomic.AtomicBoolean;

/** Read-only IME measurements, scoped to the idiom page. Does not change window flags. */
public final class IdiomKeyboardBridge implements Application.ActivityLifecycleCallbacks {
    private static volatile IdiomKeyboardBridge instance;
    private final Activity activity;
    // visible, overlap, surface height, foreground, surface width: one immutable
    // measurement, published atomically for Unity's render thread.
    private volatile int[] state = new int[] {0, 0, 0, 1, 0};
    private final AtomicBoolean measurePending = new AtomicBoolean();
    private boolean resumed = true;
    private int fullBottom;
    private final android.view.ViewTreeObserver.OnGlobalLayoutListener layoutListener = this::measure;
    private IdiomKeyboardBridge(Activity activity) { this.activity = activity; }

    public static synchronized void install(final Activity activity) {
        if (instance != null && instance.activity == activity) return;
        uninstall();
        final IdiomKeyboardBridge bridge = new IdiomKeyboardBridge(activity);
        instance = bridge;
        activity.runOnUiThread(() -> {
            if (instance != bridge) return;
            activity.getApplication().registerActivityLifecycleCallbacks(bridge);
            activity.getWindow().getDecorView().getViewTreeObserver().addOnGlobalLayoutListener(bridge.layoutListener);
            bridge.measure();
        });
    }

    public static synchronized void uninstall() {
        final IdiomKeyboardBridge current = instance;
        instance = null;
        if (current != null) current.activity.runOnUiThread(current::detach);
    }

    private void detach() {
        activity.getApplication().unregisterActivityLifecycleCallbacks(this);
        android.view.ViewTreeObserver observer = activity.getWindow().getDecorView().getViewTreeObserver();
        if (observer.isAlive()) observer.removeOnGlobalLayoutListener(layoutListener);
    }

    public static int[] sample() {
        IdiomKeyboardBridge current = instance;
        if (current == null) return new int[] {0, 0, 0, 0, 0};
        // Insets animations do not always dispatch a global layout every frame.
        // Coalesce requests instead of queuing stale work if the UI thread stalls.
        if (current.measurePending.compareAndSet(false, true)) {
            current.activity.runOnUiThread(() -> {
                try { if (instance == current) current.measure(); }
                finally { current.measurePending.set(false); }
            });
        }
        return current.state;
    }

    private void measure() {
        View decor = activity.getWindow().getDecorView();
        View surface = findSurface(decor);
        if (surface == null) surface = decor;
        int[] location = new int[2];
        surface.getLocationOnScreen(location);
        int surfaceBottom = location[1] + surface.getHeight();
        Rect visibleFrame = new Rect();
        decor.getWindowVisibleDisplayFrame(visibleFrame);
        int[] decorLocation = new int[2];
        decor.getLocationOnScreen(decorLocation);
        fullBottom = Math.max(fullBottom, decorLocation[1] + decor.getHeight());
        boolean visible = false;
        int keyboardTop = fullBottom;
        WindowInsets insets = decor.getRootWindowInsets();
        if (Build.VERSION.SDK_INT >= 30 && insets != null) {
            visible = insets.isVisible(WindowInsets.Type.ime());
            int height = insets.getInsets(WindowInsets.Type.ime()).bottom;
            // A floating IME is visible with zero inset: no vertical displacement.
            if (visible && height > 0) keyboardTop = fullBottom - height;
        }
        int hidden = fullBottom - visibleFrame.bottom;
        if (!visible && hidden > fullBottom * 0.18f) {
            visible = true;
            keyboardTop = visibleFrame.bottom;
        }
        int overlap = visible ? Math.min(surface.getHeight(), Math.max(0, surfaceBottom - keyboardTop)) : 0;
        state = new int[] { visible ? 1 : 0, overlap, surface.getHeight(), resumed ? 1 : 0, surface.getWidth() };
    }

    private View findSurface(View view) {
        if (view instanceof android.view.SurfaceView) return view;
        if (view instanceof android.view.ViewGroup) {
            android.view.ViewGroup group = (android.view.ViewGroup)view;
            for (int i = 0; i < group.getChildCount(); i++) {
                View result = findSurface(group.getChildAt(i));
                if (result != null) return result;
            }
        }
        return null;
    }

    public static void hide() {
        IdiomKeyboardBridge current = instance;
        if (current == null) return;
        current.activity.runOnUiThread(() -> {
            View decor = current.activity.getWindow().getDecorView();
            InputMethodManager imm = (InputMethodManager)current.activity.getSystemService(Activity.INPUT_METHOD_SERVICE);
            if (imm != null) imm.hideSoftInputFromWindow(decor.getWindowToken(), 0);
        });
    }

    public void onActivityResumed(Activity a) { if (a == activity) { resumed = true; measure(); } }
    public void onActivityPaused(Activity a) { if (a == activity) { resumed = false; measure(); } }
    public void onActivityDestroyed(Activity a) {
        if (a == activity) {
            detach();
            if (instance == this) instance = null;
        }
    }
    public void onActivityCreated(Activity a, Bundle b) { }
    public void onActivityStarted(Activity a) { }
    public void onActivityStopped(Activity a) { }
    public void onActivitySaveInstanceState(Activity a, Bundle b) { }
}
