package com.xanaduyonder.zhaoyunadou;

/** Main-thread asynchronous operations cannot outlive replacement, exit or destruction. */
public final class CallbackEpoch {
    private long value;
    private boolean closed;
    public long next() { return ++value; }
    public boolean isCurrent(long candidate) { return !closed && candidate == value; }
    public void close() { closed = true; value++; }
}
