package com.xanaduyonder.classicgoldminer;

/** Receives committed Back events only. Predictive gesture start/progress/cancel never call it. */
public final class BackPressGate {
    public static final long WINDOW_MS = 2000;
    private long first = -1;

    public boolean commit(long now) {
        if (first >= 0 && now >= first && now - first <= WINDOW_MS) {
            reset();
            return true;
        }
        first = now;
        return false;
    }

    public void reset() { first = -1; }
}
