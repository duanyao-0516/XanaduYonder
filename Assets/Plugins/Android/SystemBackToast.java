package com.duanyao.xanaduyonder;

import android.app.Activity;
import android.widget.Toast;

/** Presentation only; Unity owns the current game's exit confirmation. */
public final class SystemBackToast {
    private static Toast current;

    public static void setVisible(Activity activity, boolean visible) {
        activity.runOnUiThread(() -> {
            if (current != null) {
                current.cancel();
                current = null;
            }
            if (visible && !activity.isFinishing() && !activity.isDestroyed()) {
                current = Toast.makeText(activity.getApplicationContext(),
                        "再按一次返回大厅", Toast.LENGTH_SHORT);
                current.show();
            }
        });
    }
}
