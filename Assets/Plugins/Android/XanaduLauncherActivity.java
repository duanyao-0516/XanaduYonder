package com.duanyao.xanaduyonder;

import android.app.Activity;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.os.Bundle;

/** Preserve an active game when the application icon reopens its existing task. */
public final class XanaduLauncherActivity extends Activity {
    @Override protected void onCreate(Bundle savedState) {
        super.onCreate(savedState);
        if (isTaskRoot()) {
            try {
                Bundle metadata = getPackageManager().getActivityInfo(getComponentName(), PackageManager.GET_META_DATA).metaData;
                String unityActivity = metadata.getString("xanadu.unity_activity");
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
