package com.xanaduyonder.fruitslice;

/** Counts completed system Back events, never touch movements or cancelled gestures. */
public final class FruitSliceBackPressState {
    private long first = -1;

    public boolean back(long elapsedRealtime) {
        if (first >= 0 && elapsedRealtime >= first && elapsedRealtime - first <= 2000) {
            reset();
            return true;
        }
        first = elapsedRealtime;
        return false;
    }

    public void reset() { first = -1; }
}
