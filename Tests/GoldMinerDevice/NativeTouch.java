import android.os.Process;
import android.os.SystemClock;
import android.view.InputDevice;
import android.view.InputEvent;
import android.view.MotionEvent;

import java.lang.reflect.InvocationTargetException;
import java.lang.reflect.Method;

/**
 * Android shell-only touchscreen driver; no dependency on the game or its private data.
 * Run using CLASSPATH=/data/local/tmp/gold-miner-touch.jar app_process / NativeTouch ...
 * Coordinates are pixels on the default display in its current orientation.
 */
public final class NativeTouch {
    private static final int SHELL_UID = 2000;
    // InputManager.INJECT_INPUT_EVENT_MODE_WAIT_FOR_FINISH (hidden Android constant).
    private static final int WAIT_FOR_FINISH = 2;
    private static final long MAX_HOLD_MS = 60_000;

    private final Object inputManager;
    private final Method injectInputEvent;
    private final int deviceId;
    private final float[] x = new float[2];
    private final float[] y = new float[2];
    private long downTime;
    private int activePointers;
    private int eventCount;

    private NativeTouch() throws ReflectiveOperationException {
        Class<?> managerClass;
        try {
            managerClass = Class.forName("android.hardware.input.InputManagerGlobal");
        } catch (ClassNotFoundException unavailableBeforeAndroid14) {
            managerClass = Class.forName("android.hardware.input.InputManager");
        }
        inputManager = managerClass.getMethod("getInstance").invoke(null);
        if (inputManager == null) {
            throw new IllegalStateException("Android input service is unavailable");
        }
        injectInputEvent = managerClass.getMethod("injectInputEvent", InputEvent.class, int.class);
        deviceId = touchscreenDeviceId();
        System.out.println("READY manager=" + managerClass.getSimpleName()
                + " uid=" + Process.myUid() + " deviceId=" + deviceId
                + " source=SOURCE_TOUCHSCREEN displayId=0");
    }

    public static void main(String[] args) {
        if (args.length == 1 && ("--help".equals(args[0]) || "help".equals(args[0]))) {
            usage();
            return;
        }
        NativeTouch driver = null;
        try {
            Command command = Command.parse(args);
            if (Process.myUid() != SHELL_UID) {
                throw new IllegalStateException("Run from adb shell app_process (uid 2000)");
            }
            driver = new NativeTouch();
            driver.run(command);
            System.out.println("OK command=" + command.name + " events=" + driver.eventCount
                    + " elapsedMs=" + (SystemClock.uptimeMillis() - driver.downTime));
        } catch (Exception error) {
            if (driver != null) {
                driver.cancelAfterFailure(error);
            }
            Throwable cause = error instanceof InvocationTargetException
                    ? ((InvocationTargetException) error).getTargetException() : error;
            System.err.println("FAIL " + cause.getClass().getSimpleName() + ": " + cause.getMessage());
            for (Throwable cleanupError : error.getSuppressed()) {
                System.err.println("CANCEL_FAIL " + cleanupError);
            }
            System.exit(1);
        }
    }

    private void run(Command command) throws ReflectiveOperationException, InterruptedException {
        x[0] = command.x1;
        y[0] = command.y1;
        x[1] = command.x2;
        y[1] = command.y2;
        downTime = SystemClock.uptimeMillis();
        send(MotionEvent.ACTION_DOWN, 1);
        if ("dual".equals(command.name)) {
            send(MotionEvent.ACTION_POINTER_DOWN | (1 << MotionEvent.ACTION_POINTER_INDEX_SHIFT), 2);
            Thread.sleep(command.holdMs);
            send(MotionEvent.ACTION_POINTER_UP | (1 << MotionEvent.ACTION_POINTER_INDEX_SHIFT), 2);
        } else if ("cross".equals(command.name)) {
            // Keep pointer 0 and downTime throughout: crossing halves must not create another DOWN.
            long moveStart = SystemClock.uptimeMillis();
            long elapsed;
            do {
                elapsed = SystemClock.uptimeMillis() - moveStart;
                if (elapsed < command.holdMs) {
                    Thread.sleep(Math.min(16, command.holdMs - elapsed));
                }
                elapsed = SystemClock.uptimeMillis() - moveStart;
                float fraction = command.holdMs == 0 ? 1f
                        : Math.min(1f, (float) elapsed / command.holdMs);
                x[0] = command.x1 + (command.x2 - command.x1) * fraction;
                y[0] = command.y1 + (command.y2 - command.y1) * fraction;
                send(MotionEvent.ACTION_MOVE, 1);
            } while (elapsed < command.holdMs);
        } else {
            Thread.sleep(command.holdMs);
        }
        send(MotionEvent.ACTION_UP, 1);
    }

    private void send(int action, int pointerCount) throws ReflectiveOperationException {
        MotionEvent.PointerProperties[] properties = new MotionEvent.PointerProperties[pointerCount];
        MotionEvent.PointerCoords[] coordinates = new MotionEvent.PointerCoords[pointerCount];
        int maskedAction = action & MotionEvent.ACTION_MASK;
        int actionIndex = (action & MotionEvent.ACTION_POINTER_INDEX_MASK)
                >> MotionEvent.ACTION_POINTER_INDEX_SHIFT;
        for (int i = 0; i < pointerCount; i++) {
            properties[i] = new MotionEvent.PointerProperties();
            properties[i].id = i;
            properties[i].toolType = MotionEvent.TOOL_TYPE_FINGER;
            coordinates[i] = new MotionEvent.PointerCoords();
            coordinates[i].x = x[i];
            coordinates[i].y = y[i];
            boolean lifted = maskedAction == MotionEvent.ACTION_UP
                    || maskedAction == MotionEvent.ACTION_CANCEL
                    || (maskedAction == MotionEvent.ACTION_POINTER_UP && i == actionIndex);
            coordinates[i].pressure = lifted ? 0f : 1f;
            coordinates[i].size = 1f;
        }
        // The public array overload selects DEFAULT_DISPLAY and preserves explicit pointer IDs.
        MotionEvent event = MotionEvent.obtain(downTime, SystemClock.uptimeMillis(), action,
                pointerCount, properties, coordinates, 0, 0, 1f, 1f, deviceId, 0,
                InputDevice.SOURCE_TOUCHSCREEN, 0);
        if (event == null) {
            throw new IllegalStateException("MotionEvent.obtain returned null");
        }
        try {
            Object accepted = injectInputEvent.invoke(inputManager, event, WAIT_FOR_FINISH);
            if (!Boolean.TRUE.equals(accepted)) {
                throw new IllegalStateException("injectInputEvent rejected " + MotionEvent.actionToString(action));
            }
            eventCount++;
            activePointers = maskedAction == MotionEvent.ACTION_UP || maskedAction == MotionEvent.ACTION_CANCEL
                    ? 0 : maskedAction == MotionEvent.ACTION_POINTER_UP ? pointerCount - 1 : pointerCount;
            if (maskedAction != MotionEvent.ACTION_MOVE) {
                System.out.println("EVENT " + MotionEvent.actionToString(action)
                        + " pointers=" + pointerCount + " timeMs=" + (event.getEventTime() - downTime));
            }
        } finally {
            event.recycle();
        }
    }

    private void cancelAfterFailure(Exception originalError) {
        if (activePointers == 0) {
            return;
        }
        try {
            send(MotionEvent.ACTION_CANCEL, activePointers);
        } catch (Exception cleanupError) {
            originalError.addSuppressed(cleanupError);
        }
    }

    private static int touchscreenDeviceId() {
        for (int id : InputDevice.getDeviceIds()) {
            InputDevice device = InputDevice.getDevice(id);
            if (device != null && device.supportsSource(InputDevice.SOURCE_TOUCHSCREEN)) {
                return id;
            }
        }
        throw new IllegalStateException("No SOURCE_TOUCHSCREEN input device is available");
    }

    private static void usage() {
        System.out.println("NativeTouch tap x y holdMs");
        System.out.println("NativeTouch dual x1 y1 x2 y2 holdMs");
        System.out.println("NativeTouch cross x1 y1 x2 y2 holdMs");
        System.out.println("tap: DOWN, hold, UP; dual: DOWN(0), POINTER_DOWN(1), hold, POINTER_UP(1), UP(0)");
        System.out.println("cross: DOWN at start, MOVE to end over holdMs, UP; holdMs: 0..60000");
        System.out.println("Requires adb shell app_process; pixels use current orientation on display 0.");
    }

    private static final class Command {
        final String name;
        final float x1, y1, x2, y2;
        final long holdMs;

        private Command(String name, float x1, float y1, float x2, float y2, long holdMs) {
            this.name = name;
            this.x1 = x1;
            this.y1 = y1;
            this.x2 = x2;
            this.y2 = y2;
            this.holdMs = holdMs;
        }

        static Command parse(String[] args) {
            boolean tap = args.length > 0 && "tap".equals(args[0]);
            boolean pair = args.length > 0 && ("dual".equals(args[0]) || "cross".equals(args[0]));
            if ((!tap && !pair) || args.length != (tap ? 4 : 6)) {
                throw new IllegalArgumentException("Expected tap x y holdMs or dual/cross x1 y1 x2 y2 holdMs; use --help");
            }
            float x1 = coordinate(args[1]);
            float y1 = coordinate(args[2]);
            float x2 = tap ? x1 : coordinate(args[3]);
            float y2 = tap ? y1 : coordinate(args[4]);
            long holdMs = Long.parseLong(args[args.length - 1]);
            if (holdMs < 0 || holdMs > MAX_HOLD_MS) {
                throw new IllegalArgumentException("holdMs must be in 0.." + MAX_HOLD_MS);
            }
            return new Command(args[0], x1, y1, x2, y2, holdMs);
        }

        private static float coordinate(String value) {
            float parsed = Float.parseFloat(value);
            if (Float.isNaN(parsed) || Float.isInfinite(parsed) || parsed < 0 || parsed > 100_000) {
                throw new IllegalArgumentException("Coordinate must be finite and in 0..100000: " + value);
            }
            return parsed;
        }
    }
}
