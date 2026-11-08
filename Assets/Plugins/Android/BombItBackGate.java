package com.xanaduyonder.bombit;

/** Counts committed system Back actions, never pointer motion or canceled gestures. */
final class BombItBackGate {
    private static final long WINDOW_MILLIS = 2500;
    private long firstBack = -1;

    boolean shouldExit(long nowMillis) {
        if (firstBack >= 0 && nowMillis >= firstBack && nowMillis - firstBack <= WINDOW_MILLIS) {
            reset();
            return true;
        }
        firstBack = nowMillis;
        return false;
    }

    void reset() { firstBack = -1; }
}
