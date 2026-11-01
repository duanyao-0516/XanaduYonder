package com.xanaduyonder.zhaoyunadou;

import android.app.Activity;
import android.graphics.Color;
import android.net.Uri;
import android.net.http.SslError;
import android.os.Build;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.os.SystemClock;
import android.util.Log;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.view.WindowInsets;
import android.view.WindowInsetsController;
import android.view.WindowManager;
import android.webkit.ClientCertRequest;
import android.webkit.GeolocationPermissions;
import android.webkit.HttpAuthHandler;
import android.webkit.PermissionRequest;
import android.webkit.RenderProcessGoneDetail;
import android.webkit.SslErrorHandler;
import android.webkit.ValueCallback;
import android.webkit.WebChromeClient;
import android.webkit.WebResourceError;
import android.webkit.WebResourceRequest;
import android.webkit.WebResourceResponse;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.widget.Button;
import android.widget.FrameLayout;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;
import android.widget.Toast;
import android.window.OnBackInvokedCallback;
import android.window.OnBackInvokedDispatcher;
import org.json.JSONObject;
import org.json.JSONTokener;

/** Fullscreen local game. No JS-to-Java bridge, browser history, network or permanent host toolbar. */
@SuppressWarnings("deprecation")
public final class ZhaoyunAdouActivity extends Activity {
    private static final String ENTRY = AssetRequestPolicy.ORIGIN + "index.html";
    private static final long CALL_TIMEOUT_MS = 6000;
    private static final long STARTUP_TIMEOUT_MS = 60000;
    private final Handler handler = new Handler(Looper.getMainLooper());
    private final CallbackEpoch pageEpoch = new CallbackEpoch();
    private final CallbackEpoch lifecycleEpoch = new CallbackEpoch();
    private final CallbackEpoch statusEpoch = new CallbackEpoch();
    private final BackPressGate back = new BackPressGate(2000);
    private FrameLayout root;
    private WebView web;
    private View failurePanel;
    private Toast backToast;
    private LocalAssetServer assetServer;
    private OnBackInvokedCallback modernBack;
    private long session;
    private boolean ownsSession;
    private boolean soundEnabled;
    private boolean resumed;
    private boolean focused;
    private boolean configured;
    private boolean pageLoaded;
    private boolean ready;
    private boolean destroyed;
    private boolean exiting;
    private boolean nativeFrozen;
    private boolean lifecyclePending;
    private boolean pendingActive;
    private Boolean appliedActive;
    private final ForegroundBudget startupBudget = new ForegroundBudget(STARTUP_TIMEOUT_MS);
    private String lastFailure;
    private final Runnable statusPoll = this::pollStatus;
    private final Runnable startupWatchdog = () -> {
        if (!destroyed && !exiting && !ready && failurePanel == null && resumed && focused
                && startupBudget.remaining(SystemClock.elapsedRealtime()) == 0)
            showFailure("游戏尚未准备好", "离线游戏前台启动超时，尚未确认可游玩。可以重试载入或返回大厅。", this::reloadGame, false, true);
    };

    @Override protected void onCreate(Bundle savedState) {
        super.onCreate(savedState);
        session = getIntent().getLongExtra(ZhaoyunAdouBridge.SESSION, 0);
        ownsSession = ZhaoyunAdouBridge.started(session);
        if (!ownsSession) { finish(); return; }
        soundEnabled = getIntent().getBooleanExtra("soundEnabled", true);
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
        root = new FrameLayout(this);
        root.setBackgroundColor(Color.rgb(247, 244, 230));
        setContentView(root);
        configureFullscreenWindow();
        if (Build.VERSION.SDK_INT >= 33) {
            // onBackInvoked fires only on commitment. Cancelled predictive gestures do not arm the gate.
            modernBack = this::committedBack;
            getOnBackInvokedDispatcher().registerOnBackInvokedCallback(OnBackInvokedDispatcher.PRIORITY_DEFAULT, modernBack);
        }
        hideSystemBars();
        try {
            assetServer = new LocalAssetServer(getAssets());
            createWebView();
        } catch (Exception | LinkageError error) {
            showFailure("无法打开游戏", "离线资源或 WebView 不可用：" + error.getMessage(), this::reloadGame, false, true);
        }
    }

    @SuppressWarnings("deprecation")
    private void configureFullscreenWindow() {
        if (Build.VERSION.SDK_INT >= 30) getWindow().setDecorFitsSystemWindows(false);
        else getWindow().getDecorView().setSystemUiVisibility(View.SYSTEM_UI_FLAG_LAYOUT_STABLE
                | View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN | View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION);
        if (Build.VERSION.SDK_INT >= 28) {
            WindowManager.LayoutParams attributes = getWindow().getAttributes();
            attributes.layoutInDisplayCutoutMode = WindowManager.LayoutParams.LAYOUT_IN_DISPLAY_CUTOUT_MODE_SHORT_EDGES;
            getWindow().setAttributes(attributes);
        }
        // Keep the original game layout across the full window. Do not pad the viewport,
        // move game controls, exclude system gestures, or intercept edge Back gestures.
    }
    @SuppressWarnings("deprecation")
    private void hideSystemBars() {
        if (Build.VERSION.SDK_INT >= 30) {
            WindowInsetsController controller = getWindow().getInsetsController();
            if (controller != null) {
                controller.setSystemBarsBehavior(WindowInsetsController.BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE);
                controller.hide(WindowInsets.Type.systemBars());
            }
        } else getWindow().getDecorView().setSystemUiVisibility(View.SYSTEM_UI_FLAG_LAYOUT_STABLE
                | View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN | View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION
                | View.SYSTEM_UI_FLAG_FULLSCREEN | View.SYSTEM_UI_FLAG_HIDE_NAVIGATION | View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY);
    }

    @SuppressWarnings("deprecation")
    private void createWebView() {
        configured = false; pageLoaded = false; ready = false; appliedActive = null; lifecyclePending = false;
        nativeFrozen = false; exiting = false;
        startupBudget.reset(SystemClock.elapsedRealtime(), resumed && focused);
        WebView.setWebContentsDebuggingEnabled(false);
        web = new WebView(this);
        WebSettings settings = web.getSettings();
        settings.setJavaScriptEnabled(true);
        settings.setDomStorageEnabled(true);
        settings.setBlockNetworkLoads(true);
        settings.setAllowFileAccess(false);
        settings.setAllowContentAccess(false);
        settings.setAllowFileAccessFromFileURLs(false);
        settings.setAllowUniversalAccessFromFileURLs(false);
        settings.setGeolocationEnabled(false);
        settings.setJavaScriptCanOpenWindowsAutomatically(false);
        settings.setSupportMultipleWindows(true);
        settings.setMixedContentMode(WebSettings.MIXED_CONTENT_NEVER_ALLOW);
        settings.setSafeBrowsingEnabled(false); // This origin is APK-only and may never initiate remote checks.
        settings.setSaveFormData(false);
        settings.setSupportZoom(false);
        settings.setBuiltInZoomControls(false);
        settings.setDisplayZoomControls(false);
        settings.setTextZoom(100);
        settings.setUseWideViewPort(true);
        settings.setLoadWithOverviewMode(true);
        settings.setMediaPlaybackRequiresUserGesture(true);
        web.setBackgroundColor(Color.rgb(247, 244, 230));
        web.setOverScrollMode(View.OVER_SCROLL_NEVER);
        web.setVerticalScrollBarEnabled(false);
        web.setHorizontalScrollBarEnabled(false);
        web.setDownloadListener((url, agent, disposition, mime, length) -> { /* Downloads are not a game capability. */ });
        web.setWebViewClient(new GameClient());
        web.setWebChromeClient(new WebChromeClient() {
            @Override public boolean onCreateWindow(WebView view, boolean dialog, boolean userGesture, android.os.Message result) { return false; }
            @Override public void onPermissionRequest(PermissionRequest request) { request.deny(); }
            @Override public void onGeolocationPermissionsShowPrompt(String origin, GeolocationPermissions.Callback callback) { callback.invoke(origin, false, false); }
            @Override public boolean onShowFileChooser(WebView view, ValueCallback<Uri[]> result, FileChooserParams params) { result.onReceiveValue(null); return true; }
        });
        root.addView(web, 0, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));
        updateStartupWatchdog();
        web.loadUrl(ENTRY);
    }

    private final class GameClient extends WebViewClient {
        @Override public WebResourceResponse shouldInterceptRequest(WebView view, WebResourceRequest request) { return assetServer.serve(request); }
        @Override public WebResourceResponse shouldInterceptRequest(WebView view, String url) { return assetServer.serve(url, "GET", null); }
        @Override public boolean shouldOverrideUrlLoading(WebView view, WebResourceRequest request) { return true; }
        @Override public boolean shouldOverrideUrlLoading(WebView view, String url) { return true; }
        @Override public void onReceivedSslError(WebView view, SslErrorHandler callback, SslError error) { callback.cancel(); }
        @Override public void onReceivedHttpAuthRequest(WebView view, HttpAuthHandler callback, String host, String realm) { callback.cancel(); }
        @Override public void onReceivedClientCertRequest(WebView view, ClientCertRequest request) { request.cancel(); }
        @Override public void onPageFinished(WebView view, String url) {
            if (destroyed || view != web || !ENTRY.equals(url) || configured || failurePanel != null) return;
            pageLoaded = true;
            final long generation = pageEpoch.next();
            JSONObject options = new JSONObject();
            try { options.put("soundEnabled", soundEnabled); } catch (Exception impossible) { throw new IllegalStateException(impossible); }
            callApi("configure", options.toString(), pageEpoch, generation, result -> {
                if (!ok(result)) { startupFailure(result); return; }
                configured = true;
                reconcileLifecycle();
                scheduleStatus(0);
            });
        }
        @Override public void onReceivedError(WebView view, WebResourceRequest request, WebResourceError error) {
            if (view == web && request.isForMainFrame() && !destroyed && !exiting)
                showFailure("离线页面加载失败", String.valueOf(error.getDescription()), ZhaoyunAdouActivity.this::reloadGame, false, true);
        }
        @Override public void onReceivedHttpError(WebView view, WebResourceRequest request, WebResourceResponse response) {
            if (view == web && request.isForMainFrame() && !destroyed && !exiting)
                showFailure("离线入口缺失", "本地页面返回 " + response.getStatusCode(), ZhaoyunAdouActivity.this::reloadGame, false, true);
        }
        @Override public boolean onRenderProcessGone(WebView view, RenderProcessGoneDetail detail) {
            if (view != web) return true;
            destroyWebView();
            showFailure("游戏画面已停止", "上次已保存的长期进度仍保留，当前战斗无法恢复。尚未确认本次保存。", ZhaoyunAdouActivity.this::reloadGame, false, true);
            return true;
        }
    }

    private interface ApiResult { void complete(JSONObject value); }
    private void callApi(String method, String argument, CallbackEpoch epoch, long generation, ApiResult callback) {
        if (web == null || destroyed || !epoch.isCurrent(generation)) return;
        final WebView target = web;
        final boolean[] settled = { false };
        Runnable timeout = () -> {
            if (settled[0] || destroyed || target != web || !epoch.isCurrent(generation)) return;
            settled[0] = true;
            callback.complete(failure("游戏没有确认“" + method + "”操作；尚未确认保存或暂停。"));
        };
        handler.postDelayed(timeout, CALL_TIMEOUT_MS);
        String script = "(function(){try{var h=window.XanaduZhaoyun;var m=" + JSONObject.quote(method)
                + ";if(!h||typeof h[m]!=='function')return JSON.stringify({ok:false,error:'离线宿主接口尚未建立'});"
                + "var r=h[m](" + (argument == null ? "" : argument) + ");"
                + "if(r===undefined)return JSON.stringify({ok:false,error:'宿主接口未返回结果'});"
                + "return typeof r==='string'?r:JSON.stringify(r);}catch(e){return JSON.stringify({ok:false,error:String(e&&e.message||e)});}})()";
        try {
            target.evaluateJavascript(script, value -> {
                handler.removeCallbacks(timeout);
                if (settled[0] || destroyed || target != web || !epoch.isCurrent(generation)) return;
                settled[0] = true;
                JSONObject result;
                try {
                    Object parsed = new JSONTokener(value == null ? "null" : value).nextValue();
                    if (parsed instanceof String) parsed = new JSONTokener((String) parsed).nextValue();
                    result = parsed instanceof JSONObject ? (JSONObject) parsed : failure("宿主接口返回格式无效");
                } catch (Exception invalid) { result = failure("无法读取游戏确认：" + invalid.getMessage()); }
                callback.complete(result);
            });
        } catch (RuntimeException error) {
            handler.removeCallbacks(timeout);
            if (!settled[0] && epoch.isCurrent(generation)) { settled[0] = true; callback.complete(failure(error.toString())); }
        }
    }
    private static JSONObject failure(String message) {
        JSONObject object = new JSONObject();
        try { object.put("ok", false); object.put("error", message); } catch (Exception impossible) { throw new IllegalStateException(impossible); }
        return object;
    }
    private static boolean ok(JSONObject result) { return result.optBoolean("ok", false); }
    private static String error(JSONObject result) {
        return result.isNull("error") ? "游戏未确认操作" : result.optString("error", "游戏未确认操作");
    }
    private void startupFailure(JSONObject result) {
        showFailure("游戏接口未准备好", error(result), this::reloadGame, false, true);
    }
    private void scheduleStatus(long delay) {
        handler.removeCallbacks(statusPoll);
        if (!destroyed && !exiting && configured && !nativeFrozen && failurePanel == null) handler.postDelayed(statusPoll, delay);
    }
    private void pollStatus() {
        if (destroyed || exiting || web == null || nativeFrozen || !configured || failurePanel != null) return;
        long generation = statusEpoch.next();
        callApi("status", null, statusEpoch, generation, result -> {
            // Initialization can fail before BridgeReady, or become ready before the first poll.
            // Choose recovery from this response rather than the previous cached state.
            ready = result.optBoolean("ready", false);
            String message = result.isNull("error") ? "" : result.optString("error", "");
            if (!message.isEmpty()) {
                // status.paused also includes the game's own pause, which does not confirm mute
                // or persistence. Let the failure panel request the host suspension handshake.
                showFailure("游戏状态异常", message, ready ? this::retrySaveAndResume : this::reloadGame, ready, !ready);
                return;
            }
            updateStartupWatchdog();
            if (!ready && startupBudget.remaining(SystemClock.elapsedRealtime()) == 0) {
                startupWatchdog.run();
                return;
            }
            scheduleStatus(ready ? 1000 : 250);
        });
    }
    private void updateStartupWatchdog() {
        boolean counting = web != null && resumed && focused && !ready && failurePanel == null && !destroyed && !exiting;
        long now = SystemClock.elapsedRealtime();
        startupBudget.setForeground(counting, now);
        handler.removeCallbacks(startupWatchdog);
        if (counting) handler.postDelayed(startupWatchdog, Math.max(1, startupBudget.remaining(now)));
    }
    private void reconcileLifecycle() {
        updateStartupWatchdog();
        if (destroyed || exiting || web == null || !configured) return;
        boolean wanted = resumed && focused && failurePanel == null;
        if (lifecyclePending && pendingActive == wanted) return;
        if (!lifecyclePending && appliedActive != null && appliedActive.booleanValue() == wanted) {
            if (wanted) scheduleStatus(0);
            return;
        }
        lifecyclePending = true;
        pendingActive = wanted;
        long generation = lifecycleEpoch.next();
        // A rapid pause/resume supersedes the pending callback; it cannot freeze a newly resumed page.
        thaw();
        callApi("setActive", Boolean.toString(wanted), lifecycleEpoch, generation, result -> {
            if (!ok(result)) {
                // Initialization may fail after configure but before this acknowledgement.
                // Read status before freezing; a cached ready value can still lag this page.
                callApi("status", null, lifecycleEpoch, generation, status -> {
                    lifecyclePending = false;
                    ready = status.optBoolean("ready", false);
                    freezeAcknowledgedPause(result);
                    showFailure("保存或暂停尚未完成", error(result), ready ? this::retrySaveAndResume : this::reloadGame, ready, !ready);
                });
                return;
            }
            lifecyclePending = false;
            appliedActive = wanted;
            if (!wanted) {
                statusEpoch.next();
                handler.removeCallbacks(statusPoll);
                // Only a successful game flush/pause/mute acknowledgement permits freezing JS timers.
                freezeWebView();
            } else scheduleStatus(0);
            if (wanted != (resumed && focused && failurePanel == null)) reconcileLifecycle();
        });
    }
    private void retrySaveAndResume() {
        if (destroyed || exiting || web == null || lifecyclePending) return;
        // setActive(true) cannot clear a prior persistence error. Retry the acknowledged flush
        // first, then release only the host pause. A battle still requires its Continue action.
        lifecyclePending = true;
        pendingActive = false;
        long generation = lifecycleEpoch.next();
        statusEpoch.next();
        thaw();
        callApi("setActive", "false", lifecycleEpoch, generation, result -> {
            lifecyclePending = false;
            if (!ok(result)) {
                freezeAcknowledgedPause(result);
                showFailure("仍未完成保存", error(result), this::retrySaveAndResume, true, false);
                return;
            }
            appliedActive = false;
            freezeWebView();
            clearFailure();
            reconcileLifecycle();
        });
    }
    private void freezeAcknowledgedPause(JSONObject result) {
        if (result.optBoolean("paused", false)) {
            appliedActive = false;
            freezeWebView();
        }
    }
    private void freezeWebView() {
        if (web == null || nativeFrozen) return;
        statusEpoch.next();
        handler.removeCallbacks(statusPoll);
        web.onPause();
        web.pauseTimers();
        nativeFrozen = true;
    }
    private void thaw() {
        if (web != null && nativeFrozen) { web.resumeTimers(); web.onResume(); nativeFrozen = false; }
    }

    @Override protected void onResume() {
        super.onResume();
        resumed = true;
        hideSystemBars();
        reconcileLifecycle();
    }
    @Override protected void onPause() {
        resumed = false;
        resetBack();
        reconcileLifecycle();
        super.onPause();
    }
    @Override public void onWindowFocusChanged(boolean value) {
        super.onWindowFocusChanged(value);
        focused = value;
        if (!value) resetBack(); else hideSystemBars();
        reconcileLifecycle();
    }
    @Override public void onBackPressed() { committedBack(); }
    private void committedBack() {
        if (destroyed || exiting || !resumed || !focused) return;
        if (!back.commit(SystemClock.elapsedRealtime())) {
            if (backToast != null) backToast.cancel();
            backToast = Toast.makeText(this, "再返回一次回到大厅", Toast.LENGTH_SHORT);
            backToast.show();
            return;
        }
        resetBack();
        beginExit();
    }
    private void resetBack() {
        back.reset();
        if (backToast != null) { backToast.cancel(); backToast = null; }
    }
    private void beginExit() {
        if (destroyed || exiting) return;
        if (web == null || !configured) {
            showFailure("尚未确认保存", "游戏接口不可用，无法确认最新进度已保存。", this::reloadGame, false, true);
            return;
        }
        exiting = true;
        clearFailure();
        lifecycleEpoch.next(); lifecyclePending = false;
        statusEpoch.next(); handler.removeCallbacks(statusPoll); handler.removeCallbacks(startupWatchdog);
        long generation = pageEpoch.next();
        thaw();
        callApi("exit", null, pageEpoch, generation, result -> {
            if (!ok(result)) {
                exiting = false;
                freezeAcknowledgedPause(result);
                showFailure("未能保存并返回", error(result) + "\n退出结算已开始，当前页面保留以便重试保存；不会恢复已结算的战斗。也可以选择未确认保存直接返回大厅。", this::beginExit, false, true);
                return;
            }
            destroyWebView();
            finish();
        });
    }

    private void showFailure(String title, String message, Runnable retry, boolean mayStay, boolean mayReturnWithoutSave) {
        if (destroyed || root == null || isFinishing()) return;
        pageEpoch.next(); statusEpoch.next(); lifecycleEpoch.next(); lifecyclePending = false;
        handler.removeCallbacks(statusPoll); handler.removeCallbacks(startupWatchdog);
        resetBack();
        lastFailure = title + "：" + message;
        clearFailure();
        ScrollView scroll = new ScrollView(this);
        scroll.setFillViewport(true);
        scroll.setBackgroundColor(Color.rgb(247, 244, 230));
        LinearLayout content = new LinearLayout(this);
        content.setOrientation(LinearLayout.VERTICAL);
        content.setGravity(Gravity.CENTER);
        int pad = (int) (24 * getResources().getDisplayMetrics().density + .5f);
        content.setPadding(pad, pad, pad, pad);
        TextView heading = new TextView(this); heading.setText(title); heading.setTextSize(22); heading.setTextColor(Color.BLACK); heading.setGravity(Gravity.CENTER);
        content.addView(heading, new LinearLayout.LayoutParams(-1, -2));
        TextView detail = new TextView(this); detail.setText(message); detail.setTextSize(16); detail.setTextColor(Color.DKGRAY); detail.setPadding(0, pad, 0, pad);
        content.addView(detail, new LinearLayout.LayoutParams(-1, -2));
        addButton(content, "重试", retry);
        if (mayStay && web != null) addButton(content, "留在游戏", this::retrySaveAndResume);
        if (mayReturnWithoutSave) addButton(content, "返回大厅（未确认本次保存）", () -> {
            // The visible error and explicit button already disclose the unconfirmed save.
            // Preserve the diagnostic without making the hall mislabel this choice as a launch failure.
            Log.e("ZhaoyunAdou", "User returned without confirmed save: " + lastFailure);
            destroyWebView();
            finish();
        });
        scroll.addView(content, new ScrollView.LayoutParams(-1, -1));
        failurePanel = scroll;
        root.addView(scroll, new FrameLayout.LayoutParams(-1, -1));
        updateStartupWatchdog();
        // A status/startup error can be discovered while gameplay is still running. Pause it once,
        // with an acknowledgement; a failed pause only updates this panel and cannot recurse.
        if (web != null && pageLoaded && !nativeFrozen && !exiting) {
            long generation = lifecycleEpoch.next();
            callApi("setActive", "false", lifecycleEpoch, generation, result -> {
                if (failurePanel != scroll) return;
                if (ok(result) || result.optBoolean("paused", false)) {
                    appliedActive = false;
                    freezeWebView();
                }
                if (!ok(result)) detail.setText(message + "\n\n暂停/保存状态：" + error(result));
            });
        }
        // The original page is retained on save failure; never report successful exit or silently finish.
    }
    private void addButton(LinearLayout parent, String text, Runnable action) {
        Button button = new Button(this); button.setText(text); button.setAllCaps(false);
        button.setOnClickListener(view -> action.run());
        parent.addView(button, new LinearLayout.LayoutParams(-1, -2));
    }
    private void clearFailure() {
        if (failurePanel != null && root != null) root.removeView(failurePanel);
        failurePanel = null;
    }
    private void reloadGame() {
        if (destroyed) return;
        destroyWebView(); clearFailure();
        try {
            assetServer = new LocalAssetServer(getAssets());
            createWebView();
        } catch (Exception | LinkageError exception) {
            showFailure("无法重新载入", exception.toString(), this::reloadGame, false, true);
        }
    }
    private void destroyWebView() {
        pageEpoch.next(); lifecycleEpoch.next(); statusEpoch.next();
        handler.removeCallbacks(statusPoll); handler.removeCallbacks(startupWatchdog);
        lifecyclePending = false; configured = false; pageLoaded = false; ready = false;
        if (web == null) return;
        WebView previous = web;
        web = null;
        try {
            // pauseTimers is global within this process. Do not leave the next game launch frozen.
            if (nativeFrozen) previous.resumeTimers();
            previous.stopLoading();
            if (root != null) root.removeView(previous);
            previous.removeAllViews();
            previous.destroy();
        } catch (RuntimeException ignored) { /* A renderer loss can already have invalidated the view. */ }
        nativeFrozen = false;
    }
    @Override protected void onDestroy() {
        destroyed = true;
        resetBack();
        handler.removeCallbacksAndMessages(null);
        if (Build.VERSION.SDK_INT >= 33 && modernBack != null) {
            getOnBackInvokedDispatcher().unregisterOnBackInvokedCallback(modernBack);
            modernBack = null;
        }
        destroyWebView();
        pageEpoch.close(); lifecycleEpoch.close(); statusEpoch.close();
        super.onDestroy();
        // The hall must remain locked until the Activity and its WebView have actually been destroyed.
        if (ownsSession) ZhaoyunAdouBridge.stopped(session);
    }
}
