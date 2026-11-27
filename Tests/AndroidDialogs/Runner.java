package net.xanadu.validation;

import android.app.Activity;
import android.app.Dialog;
import android.app.Instrumentation;
import android.content.Intent;
import android.graphics.Bitmap;
import android.graphics.drawable.GradientDrawable;
import android.os.Bundle;
import android.os.SystemClock;
import android.util.TypedValue;
import android.text.Layout;
import android.view.InputDevice;
import android.view.MotionEvent;
import android.view.View;
import android.view.ViewGroup;
import android.widget.Button;
import android.widget.ScrollView;
import android.widget.TextView;

import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;
import java.lang.reflect.Field;
import java.lang.reflect.Method;
import java.util.concurrent.atomic.AtomicInteger;

/** Inject a failure at the host boundary, then exercise the shipped views with touch events. */
public final class Runner extends Instrumentation {
    private Activity activity;
    private String game;

    private Object field(String name) {
        try {
            Field field = activity.getClass().getDeclaredField(name);
            field.setAccessible(true);
            return field.get(activity);
        } catch (ReflectiveOperationException error) {
            throw new IllegalStateException(name, error);
        }
    }

    private void invoke(String name, Class<?>[] types, Object... arguments) {
        try {
            Method method = activity.getClass().getDeclaredMethod(name, types);
            method.setAccessible(true);
            method.invoke(activity, arguments);
        } catch (ReflectiveOperationException error) {
            throw new IllegalStateException(name, error);
        }
    }

    private void check(boolean condition, String description) {
        if (!condition) throw new AssertionError(game + " " + description);
        Bundle progress = new Bundle();
        progress.putString("passed", game + " " + description);
        sendStatus(1, progress);
    }

    private boolean showing(String name) {
        Object dialog = field(name);
        return dialog instanceof Dialog && ((Dialog) dialog).isShowing();
    }

    private void touch(long down, int action, float x, float y) {
        MotionEvent event = MotionEvent.obtain(down, SystemClock.uptimeMillis(), action, x, y, 0);
        try {
            event.setSource(InputDevice.SOURCE_TOUCHSCREEN);
            sendPointerSync(event);
        } finally {
            event.recycle();
        }
    }

    private void tap(float x, float y) {
        long down = SystemClock.uptimeMillis();
        touch(down, MotionEvent.ACTION_DOWN, x, y);
        SystemClock.sleep(70);
        touch(down, MotionEvent.ACTION_UP, x, y);
        waitForIdleSync();
        SystemClock.sleep(400);
    }

    private void center(View view) {
        int[] position = new int[2];
        view.getLocationOnScreen(position);
        tap(position[0] + view.getWidth() * .5f, position[1] + view.getHeight() * .5f);
    }

    private void tapBackdrop() {
        tap(5, activity.getWindow().getDecorView().getHeight() * .5f);
    }

    private void screenshot(String name) throws IOException {
        SystemClock.sleep(350);
        Bitmap image = getUiAutomation().takeScreenshot();
        File directory = getTargetContext().getExternalFilesDir("web-host-qa");
        if (image == null || directory == null) throw new IOException("Screenshot unavailable");
        try (FileOutputStream stream = new FileOutputStream(new File(directory, game + "-" + name + ".png"))) {
            if (!image.compress(Bitmap.CompressFormat.PNG, 100, stream)) throw new IOException("Screenshot encoding failed");
        } finally {
            image.recycle();
        }
    }

    private ScrollView paper(View view) {
        if (view instanceof ScrollView && view.getBackground() instanceof GradientDrawable) return (ScrollView) view;
        if (view instanceof ViewGroup) {
            ViewGroup parent = (ViewGroup) view;
            for (int i = 0; i < parent.getChildCount(); i++) {
                ScrollView result = paper(parent.getChildAt(i));
                if (result != null) return result;
            }
        }
        return null;
    }

    private int touchTargets(View view) {
        if (view instanceof Button) {
            int minimum = Math.round(48 * activity.getResources().getDisplayMetrics().density);
            if (view.getHeight() < minimum) throw new AssertionError("Undersized native action");
            return 1;
        }
        int count = 0;
        if (view instanceof ViewGroup) {
            ViewGroup parent = (ViewGroup) view;
            for (int i = 0; i < parent.getChildCount(); i++) count += touchTargets(parent.getChildAt(i));
        }
        return count;
    }

    private float readingEdge;
    private int checkTextSizes(View view, int seen) {
        if (view instanceof TextView) {
            int sp = seen == 0 ? 16 : 14;
            float expected = TypedValue.applyDimension(TypedValue.COMPLEX_UNIT_SP, sp,
                    activity.getResources().getDisplayMetrics());
            if (Math.abs(((TextView) view).getTextSize() - expected) > 1)
                throw new AssertionError(game + " incorrect font size for " + ((TextView) view).getText());
            if (seen < 2) {
                TextView text = (TextView) view;
                Layout layout = text.getLayout();
                if (layout == null || layout.getLineCount() == 0)
                    throw new AssertionError(game + " reading text has no layout");
                int[] location = new int[2];
                text.getLocationOnScreen(location);
                float x = location[0] + text.getCompoundPaddingLeft() + layout.getLineLeft(0);
                if (seen == 0) readingEdge = x;
                else if (Math.abs(x - readingEdge) > 1)
                    throw new AssertionError(game + " title and detail do not share their reading edge");
            }
            return seen + 1;
        }
        if (view instanceof ViewGroup) {
            ViewGroup group = (ViewGroup) view;
            for (int i = 0; i < group.getChildCount(); i++) seen = checkTextSizes(group.getChildAt(i), seen);
        }
        return seen;
    }

    private void assertSurface(String name, int actionCount) {
        Dialog dialog = (Dialog) field(name);
        ScrollView card = paper(dialog.getWindow().getDecorView());
        GradientDrawable background = card == null ? null : (GradientDrawable) card.getBackground();
        check(card != null && checkTextSizes(card, 0) == 2 + actionCount, name + " uses 16sp title and 14sp copy and actions");
        int radius = Math.round(16 * activity.getResources().getDisplayMetrics().density);
        check(background != null && background.getColor().getDefaultColor() == 0xfff8f7f1
                        && Math.abs(background.getCornerRadius() - radius) < 1
                        && Math.abs(dialog.getWindow().getAttributes().dimAmount - .4f) < .01f
                        && touchTargets(card) == actionCount,
                name + " uses paper, 16dp corners, a 40% backdrop and 48dp actions");
    }

    private void assertSaveAndLaunchFailures() throws IOException {
        runOnMainSync(() -> invoke("showExitFailure", new Class<?>[0]));
        SystemClock.sleep(700);
        check(showing("exitDialog"), "save failure is shown");
        assertSurface("exitDialog", 2);
        screenshot("exit-failure");
        tapBackdrop();
        check(!showing("exitDialog") && !activity.isFinishing() && Boolean.FALSE.equals(field("exitFailurePending")),
                "scrim stays in activity and cancels failed exit");

        runOnMainSync(() -> invoke("fail", new Class<?>[]{Throwable.class}, new IOException("离线资源读取失败")));
        SystemClock.sleep(500);
        check(showing("errorDialog"), "startup error is shown");
        assertSurface("errorDialog", 1);
        screenshot("fatal");
        tapBackdrop();
        check(!showing("errorDialog") && !activity.isFinishing(), "dismissing fatal leaves recovery reachable");
        screenshot("fatal-collapsed");
        center((View) field("loading"));
        check(showing("errorDialog"), "tap error summary reopens details");
    }

    private void assertPausedSaveFailure() throws IOException {
        AtomicInteger retries = new AtomicInteger();
        Class<?>[] types = {String.class, String.class, Runnable.class, boolean.class, boolean.class};
        runOnMainSync(() -> invoke("showFailure", types, "尚未确认保存", "暂时无法确认最新进度已保存。",
                (Runnable) retries::incrementAndGet, true, true));
        SystemClock.sleep(500);
        check(field("failurePanel") != null, "save failure is shown");
        check(checkTextSizes((View) field("failurePanel"), 0) >= 4, "recovery uses 16sp title and 14sp copy and actions");
        screenshot("save-failure");
        tapBackdrop();
        check(field("failureSummary") != null && !activity.isFinishing() && retries.get() == 0,
                "scrim retains paused activity and never retries or discards");
        screenshot("save-collapsed");
        center((View) field("failureSummary"));
        check(field("failureSummary") == null && ((View) field("failurePanel")).getVisibility() == View.VISIBLE,
                "recovery actions visible again");
    }

    private void verify(String gameName, String activityClass) throws IOException {
        game = gameName;
        Intent intent = new Intent().setClassName(getTargetContext().getPackageName(), activityClass)
                .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
        activity = startActivitySync(intent);
        try {
            waitForIdleSync();
            for (int n = 0; n < 100 && !activity.hasWindowFocus(); n++) SystemClock.sleep(100);
            check(activity.hasWindowFocus(), "main activity has focus");
            if (game.equals("zhao")) assertPausedSaveFailure();
            else assertSaveAndLaunchFailures();
        } finally {
            runOnMainSync(() -> activity.finish());
            SystemClock.sleep(500);
        }
    }

    @Override public void onCreate(Bundle arguments) {
        super.onCreate(arguments);
        start();
    }

    @Override public void onStart() {
        Bundle result = new Bundle();
        try {
            verify("bomb", "com.xanaduyonder.bombit.BombItActivity");
            verify("level", "com.xanaduyonder.leveldevil.LevelDevilActivity");
            verify("zhao", "com.xanaduyonder.zhaoyunadou.ZhaoyunAdouActivity");
            result.putString("result", "PASS");
            finish(Activity.RESULT_OK, result);
        } catch (Throwable error) {
            result.putString("error", android.util.Log.getStackTraceString(error));
            finish(Activity.RESULT_CANCELED, result);
        }
    }
}
