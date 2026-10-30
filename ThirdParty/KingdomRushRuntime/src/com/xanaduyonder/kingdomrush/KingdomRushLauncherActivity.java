package com.xanaduyonder.kingdomrush;

import android.app.Activity;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.os.Bundle;

/** Standard launcher trampoline preserves the current game when reopening its task. */
public final class KingdomRushLauncherActivity extends Activity {
    @Override protected void onCreate(Bundle savedState) {
        super.onCreate(savedState);
        if (isTaskRoot()) {
            try {
                Bundle metadata = getPackageManager().getActivityInfo(getComponentName(), PackageManager.GET_META_DATA).metaData;
                String unityActivity = metadata.getString("kingdomrush.unity_activity");
                if (unityActivity == null) throw new IllegalStateException("Missing Unity Activity metadata");
                Intent intent = new Intent();
                intent.setClassName(this, unityActivity);
                startActivity(intent);
            } catch (PackageManager.NameNotFoundException error) {
                throw new IllegalStateException("Unity launcher metadata unavailable", error);
            }
        }
        finish();
    }
}
