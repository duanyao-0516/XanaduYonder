package com.xanaduyonder.cuttherope;

/** Receives only completed system Back events, never a gesture's start/progress/cancel. */
final class CompletedBackPolicy {
    private static final long EXIT_WINDOW_MS = 2000L;
    private long firstBackAt = -1L;

    boolean onCompletedBack(long elapsedRealtime) {
        if (firstBackAt >= 0 && elapsedRealtime >= firstBackAt
                && elapsedRealtime - firstBackAt <= EXIT_WINDOW_MS) {
            reset();
            return true;
        }
        firstBackAt = elapsedRealtime;
        return false;
    }

    void reset() { firstBackAt = -1L; }
}
