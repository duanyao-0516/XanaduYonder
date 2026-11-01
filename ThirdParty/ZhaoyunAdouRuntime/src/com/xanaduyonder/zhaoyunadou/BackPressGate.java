package com.xanaduyonder.zhaoyunadou;

/** Only a committed system Back calls commit; gesture previews/cancels never reach it. */
public final class BackPressGate {
    private final long windowMillis;
    private boolean armed;
    private long firstAt;
    public BackPressGate(long windowMillis) {
        if (windowMillis <= 0) throw new IllegalArgumentException("Return window must be positive");
        this.windowMillis = windowMillis;
    }
    public boolean commit(long elapsedMillis) {
        if (armed && elapsedMillis >= firstAt && elapsedMillis - firstAt <= windowMillis) {
            reset();
            return true;
        }
        firstAt = elapsedMillis;
        armed = true;
        return false;
    }
    public void reset() { armed = false; }
}
