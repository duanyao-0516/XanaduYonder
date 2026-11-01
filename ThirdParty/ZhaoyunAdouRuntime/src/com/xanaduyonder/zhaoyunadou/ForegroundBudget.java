package com.xanaduyonder.zhaoyunadou;

/** A loading deadline spends time only while its Activity is resumed and focused. */
public final class ForegroundBudget {
    private final long limit;
    private long spent;
    private long since;
    private boolean foreground;
    public ForegroundBudget(long limit) {
        if (limit <= 0) throw new IllegalArgumentException("Budget must be positive");
        this.limit = limit;
    }
    public void reset(long now, boolean active) { spent = 0; since = now; foreground = active; }
    public void setForeground(boolean active, long now) {
        if (active == foreground) return;
        if (foreground) {
            long elapsed = now >= since ? now - since : 0;
            spent = elapsed >= limit - spent ? limit : spent + elapsed;
        }
        foreground = active;
        since = now;
    }
    public long remaining(long now) {
        long left = limit - spent;
        long elapsed = foreground && now >= since ? now - since : 0;
        return elapsed >= left ? 0 : left - elapsed;
    }
}
