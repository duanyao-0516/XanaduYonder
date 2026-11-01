package com.xanaduyonder.fruitslice;

import android.app.Activity;
import android.graphics.Color;
import android.graphics.Insets;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.os.SystemClock;
import android.util.Log;
import android.view.Gravity;
import android.view.MotionEvent;
import android.view.View;
import android.view.WindowInsets;
import android.view.WindowInsetsController;
import android.view.WindowManager;
import android.webkit.ConsoleMessage;
import android.webkit.JavascriptInterface;
import android.webkit.RenderProcessGoneDetail;
import android.webkit.WebChromeClient;
import android.webkit.WebResourceRequest;
import android.webkit.WebResourceResponse;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.widget.Button;
import android.widget.FrameLayout;
import android.widget.LinearLayout;
import android.widget.TextView;
import android.widget.Toast;
import android.window.OnBackInvokedCallback;
import android.window.OnBackInvokedDispatcher;
import android.window.OnBackAnimationCallback;
import android.window.BackEvent;
import java.io.ByteArrayInputStream;
import java.io.IOException;
import java.io.InputStream;
import java.util.HashMap;
import java.util.Locale;
import java.util.Map;
import java.util.concurrent.atomic.AtomicBoolean;
import java.util.concurrent.atomic.AtomicInteger;
import java.util.concurrent.atomic.AtomicLong;

/** Owns only the offline browser and Android lifecycle, not Laya's gameplay state. */
public final class FruitSliceActivity extends Activity {
    private static final String ORIGIN = "appassets.androidplatform.net";
    private static final String ENTRY = "https://" + ORIGIN + "/fruit-slice/index.html";
    private final Handler handler = new Handler(Looper.getMainLooper());
    private final FruitSliceBackPressState backState = new FruitSliceBackPressState();
    private final AtomicInteger startupRequests = new AtomicInteger();
    private final AtomicInteger startupLevelsRequested = new AtomicInteger();
    private final AtomicLong startupLevelMask = new AtomicLong();
    private final AtomicBoolean startupMoldRequested = new AtomicBoolean();
    private final AtomicBoolean startupReadyReceived = new AtomicBoolean();
    private long token;
    private long startupRequestedAt;
    private WebView web;
    private FrameLayout root;
    private TextView loading;
    private Toast backToast;
    private boolean resumed;
    private boolean pageLoaded;
    private boolean sound;
    private boolean destroyed;
    private boolean failed;
    private boolean startupPageFinishedLogged;
    private boolean startupResumeLogged;
    private boolean startupFocusLogged;
    private OnBackInvokedCallback backCallback;
    private final Runnable loadTimeout = () -> showFailure("游戏加载超时，请返回大厅后重试。");

    @Override protected void onCreate(Bundle savedState) {
        super.onCreate(savedState);
        token = getIntent().getLongExtra("session", -1);
        if (!FruitSliceBridge.isCurrent(token)) { finish(); return; }
        startupRequestedAt = getIntent().getLongExtra("startup_requested_at", SystemClock.elapsedRealtime());
        logStartup("activity_on_create");
        sound = getIntent().getBooleanExtra("sound", true);
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
        root = new FrameLayout(this);
        root.setBackgroundColor(Color.rgb(32, 46, 40));
        setContentView(root);
        configureInsets();
        if (Build.VERSION.SDK_INT >= 33) {
            backCallback = Build.VERSION.SDK_INT >= 34 ? new OnBackAnimationCallback() {
                @Override public void onBackStarted(BackEvent event) { callGame("cancelInput()"); }
                @Override public void onBackCancelled() { backState.reset(); callGame("cancelInput()"); }
                @Override public void onBackInvoked() { handleBack(); }
            } : this::handleBack;
            getOnBackInvokedDispatcher().registerOnBackInvokedCallback(OnBackInvokedDispatcher.PRIORITY_DEFAULT, backCallback);
        }
        createBrowser();
        enterImmersive();
    }

    private void configureInsets() {
        if (Build.VERSION.SDK_INT >= 30) getWindow().setDecorFitsSystemWindows(false);
        root.setOnApplyWindowInsetsListener((view, insets) -> {
            int left = 0, top = 0, right = 0, bottom = 0;
            if (Build.VERSION.SDK_INT >= 30) {
                Insets safe = insets.getInsetsIgnoringVisibility(WindowInsets.Type.displayCutout());
                Insets bars = insets.getInsets(WindowInsets.Type.navigationBars());
                Insets gestures = insets.getInsets(WindowInsets.Type.mandatorySystemGestures());
                left = safe.left; top = safe.top; right = safe.right;
                bottom = Math.max(safe.bottom, Math.max(bars.bottom, gestures.bottom));
            } else {
                top = insets.getSystemWindowInsetTop();
                bottom = insets.getSystemWindowInsetBottom();
                if (Build.VERSION.SDK_INT >= 28 && insets.getDisplayCutout() != null) {
                    left = insets.getDisplayCutout().getSafeInsetLeft();
                    top = Math.max(top, insets.getDisplayCutout().getSafeInsetTop());
                    right = insets.getDisplayCutout().getSafeInsetRight();
                    bottom = Math.max(bottom, insets.getDisplayCutout().getSafeInsetBottom());
                }
            }
            // Buttons stay clear of cutouts/navigation; system Back retains both screen edges.
            view.setPadding(left, top, right, bottom);
            return insets;
        });
        root.requestApplyInsets();
    }

    private void createBrowser() {
        try {
            logStartup("webview_construct_begin");
            web = new WebView(this);
            logStartup("webview_construct_end");
            web.setBackgroundColor(Color.rgb(32, 46, 40));
            web.setOverScrollMode(View.OVER_SCROLL_NEVER);
            web.setHorizontalScrollBarEnabled(false);
            web.setVerticalScrollBarEnabled(false);
            WebSettings settings = web.getSettings();
            settings.setJavaScriptEnabled(true);
            settings.setDomStorageEnabled(true);
            settings.setAllowFileAccess(false);
            settings.setAllowContentAccess(false);
            settings.setAllowFileAccessFromFileURLs(false);
            settings.setAllowUniversalAccessFromFileURLs(false);
            settings.setBlockNetworkLoads(true);
            settings.setMixedContentMode(WebSettings.MIXED_CONTENT_NEVER_ALLOW);
            settings.setSupportMultipleWindows(false);
            settings.setJavaScriptCanOpenWindowsAutomatically(false);
            settings.setSupportZoom(false);
            settings.setBuiltInZoomControls(false);
            settings.setDisplayZoomControls(false);
            settings.setTextZoom(100);
            settings.setMediaPlaybackRequiresUserGesture(true);
            web.addJavascriptInterface(new HostBridge(), "XanaduFruitNative");
            web.setWebChromeClient(new WebChromeClient() {
                @Override public boolean onConsoleMessage(ConsoleMessage message) {
                    if (message.messageLevel() == ConsoleMessage.MessageLevel.ERROR)
                        Log.e("FruitSlice", message.message() + " @ " + message.lineNumber());
                    return true;
                }
            });
            web.setWebViewClient(new WebViewClient() {
                @Override public WebResourceResponse shouldInterceptRequest(WebView view, WebResourceRequest request) {
                    return localAsset(request.getUrl(), request.getMethod());
                }
                @Override public boolean shouldOverrideUrlLoading(WebView view, WebResourceRequest request) {
                    // There is exactly one top-level document. Game navigation is internal scenes.
                    return !ENTRY.equals(request.getUrl().toString());
                }
                @Override public boolean shouldOverrideUrlLoading(WebView view, String url) { return !ENTRY.equals(url); }
                @Override public void onPageFinished(WebView view, String url) {
                    if (!ENTRY.equals(url) || failed || destroyed) return;
                    if (!startupPageFinishedLogged) {
                        startupPageFinishedLogged = true;
                        logStartup("page_finished");
                    }
                    pageLoaded = true;
                    syncLifecycle();
                }
                @Override public boolean onRenderProcessGone(WebView view, RenderProcessGoneDetail detail) {
                    releaseBrowser();
                    showFailure("游戏画面已停止，请返回大厅后重试。");
                    return true;
                }
            });
            web.setOnTouchListener((view, event) -> {
                if (event.getActionMasked() == MotionEvent.ACTION_CANCEL) callGame("cancelInput()");
                return false;
            });
            root.addView(web, new FrameLayout.LayoutParams(-1, -1));
            loading = new TextView(this);
            loading.setText("正在加载切水果大师…");
            loading.setTextSize(18);
            loading.setTextColor(Color.WHITE);
            loading.setGravity(Gravity.CENTER);
            loading.setBackgroundColor(Color.rgb(32, 46, 40));
            root.addView(loading, new FrameLayout.LayoutParams(-1, -1));
            handler.postDelayed(loadTimeout, 30000);
            logStartup("load_url");
            web.loadUrl(ENTRY);
        } catch (RuntimeException | LinkageError error) {
            Log.e("FruitSlice", "WebView initialization failed", error);
            showFailure("当前设备暂时无法打开游戏，请检查系统 WebView 后重试。");
        }
    }

    private WebResourceResponse localAsset(Uri uri, String method) {
        startupRequests.incrementAndGet();
        if (!"GET".equals(method) || !"https".equals(uri.getScheme()) || !ORIGIN.equals(uri.getHost()) ||
            uri.getPort() != -1 || uri.getUserInfo() != null) return response(403, "Forbidden", "text/plain", new byte[0]);
        String path = uri.getPath();
        if (path == null || !path.startsWith("/fruit-slice/") || path.indexOf('\\') >= 0 || path.indexOf('\0') >= 0)
            return response(403, "Forbidden", "text/plain", new byte[0]);
        for (String part : path.split("/")) if (part.equals(".") || part.equals(".."))
            return response(403, "Forbidden", "text/plain", new byte[0]);
        logStartupAssetRequest(path);
        try {
            InputStream data = getAssets().open(path.substring(1));
            Map<String, String> headers = headers();
            return new WebResourceResponse(mime(path), "UTF-8", 200, "OK", headers, data);
        } catch (IOException error) {
            Log.e("FruitSlice", "Missing local asset: " + path);
            return response(404, "Not Found", "text/plain", new byte[0]);
        }
    }

    private void logStartup(String stage) {
        long now = SystemClock.elapsedRealtime();
        Log.i("FruitSliceStartup", "session=" + token + " stage=" + stage + " atMs=" + now +
            " elapsedMs=" + (now - startupRequestedAt) + " requests=" + startupRequests.get());
    }

    private void logStartupAssetRequest(String path) {
        if (path.equals("/fruit-slice/map/Conventional/mold.lh") &&
            startupMoldRequested.compareAndSet(false, true)) logStartup("mold_requested");
        final String prefix = "/fruit-slice/json/guan";
        if (!path.startsWith(prefix) || !path.endsWith(".json")) return;
        String number = path.substring(prefix.length(), path.length() - 5);
        if (number.length() < 1 || number.length() > 2) return;
        int level = 0;
        for (int i = 0; i < number.length(); i++) {
            char digit = number.charAt(i);
            if (digit < '0' || digit > '9') return;
            level = level * 10 + digit - '0';
        }
        if (level < 1 || level > 50) return;
        long bit = 1L << (level - 1);
        long previous = startupLevelMask.getAndUpdate(mask -> mask | bit);
        if ((previous & bit) != 0) return;
        int count = startupLevelsRequested.incrementAndGet();
        // These mark request dispatch, not completion; guan50 may not be the last request.
        if (count == 1) logStartup("first_level_requested");
        else if (count == 50) logStartup("all_50_levels_requested");
    }

    private static Map<String, String> headers() {
        Map<String, String> headers = new HashMap<>();
        headers.put("Cache-Control", "no-store");
        headers.put("X-Content-Type-Options", "nosniff");
        headers.put("Content-Security-Policy", "default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval'; " +
            "style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:; media-src 'self' blob:; " +
            "connect-src 'self'; worker-src 'self' blob:; frame-src 'none'; object-src 'none'; base-uri 'none'; form-action 'none'");
        return headers;
    }

    private static WebResourceResponse response(int code, String reason, String type, byte[] data) {
        return new WebResourceResponse(type, "UTF-8", code, reason, headers(), new ByteArrayInputStream(data));
    }

    private static String mime(String path) {
        String lower = path.toLowerCase(Locale.ROOT);
        if (lower.endsWith(".html")) return "text/html";
        if (lower.endsWith(".js")) return "application/javascript";
        if (lower.endsWith(".css")) return "text/css";
        if (lower.endsWith(".json") || lower.endsWith(".atlas") || lower.endsWith(".lh") || lower.endsWith(".lmat")) return "application/json";
        if (lower.endsWith(".png")) return "image/png";
        if (lower.endsWith(".jpg") || lower.endsWith(".jpeg")) return "image/jpeg";
        if (lower.endsWith(".mp3")) return "audio/mpeg";
        if (lower.endsWith(".woff")) return "font/woff";
        if (lower.endsWith(".ttf")) return "font/ttf";
        return "application/octet-stream";
    }

    private void enterImmersive() {
        if (Build.VERSION.SDK_INT >= 30) {
            WindowInsetsController controller = getWindow().getInsetsController();
            if (controller != null) {
                controller.setSystemBarsBehavior(WindowInsetsController.BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE);
                controller.hide(WindowInsets.Type.systemBars());
            }
        } else getWindow().getDecorView().setSystemUiVisibility(View.SYSTEM_UI_FLAG_FULLSCREEN |
            View.SYSTEM_UI_FLAG_HIDE_NAVIGATION | View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY |
            View.SYSTEM_UI_FLAG_LAYOUT_STABLE | View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN | View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION);
    }

    private void handleBack() {
        if (destroyed || isFinishing()) return;
        callGame("cancelInput()");
        if (backState.back(SystemClock.elapsedRealtime())) { finish(); return; }
        if (backToast != null) backToast.cancel();
        backToast = Toast.makeText(this, "再返回一次回到大厅", Toast.LENGTH_SHORT);
        backToast.show();
    }

    @Override public void onBackPressed() { handleBack(); }

    private void callGame(String expression) {
        if (web != null && !destroyed) web.evaluateJavascript("if(window.XanaduFruitHost){window.XanaduFruitHost." + expression + ";}", null);
    }

    private void syncLifecycle() {
        if (!pageLoaded) return;
        callGame("setSoundEnabled(" + sound + ")");
        callGame("setPaused(" + (!resumed || !hasWindowFocus()) + ")");
    }

    @Override protected void onResume() {
        super.onResume();
        resumed = true;
        if (!startupReadyReceived.get() && !startupResumeLogged) {
            startupResumeLogged = true;
            logStartup("resume_before_ready");
        }
        if (web != null) web.onResume();
        enterImmersive();
        syncLifecycle();
    }

    @Override protected void onPause() {
        resumed = false;
        backState.reset();
        callGame("cancelInput()");
        callGame("setPaused(true)");
        if (web != null) web.onPause();
        super.onPause();
    }

    @Override public void onWindowFocusChanged(boolean focused) {
        super.onWindowFocusChanged(focused);
        if (!startupReadyReceived.get() && !startupFocusLogged) {
            startupFocusLogged = true;
            logStartup("first_focus_before_ready_" + focused);
        }
        if (!focused) { backState.reset(); callGame("cancelInput()"); }
        else enterImmersive();
        syncLifecycle();
    }

    private final class HostBridge {
        @JavascriptInterface public void ready() {
            final boolean firstReady = startupReadyReceived.compareAndSet(false, true);
            if (firstReady) logStartup("ready_received");
            handler.post(() -> {
                if (destroyed || failed) return;
                handler.removeCallbacks(loadTimeout);
                if (loading != null) { root.removeView(loading); loading = null; }
                pageLoaded = true;
                syncLifecycle();
                if (firstReady && web != null) {
                    logStartup("ready_ui_applied");
                    final WebView readyWeb = web;
                    readyWeb.postVisualStateCallback(1, new WebView.VisualStateCallback() {
                        @Override public void onComplete(long requestId) {
                            if (!destroyed && web == readyWeb) logStartup("ready_visual_state_committed");
                        }
                    });
                }
            });
        }
        @JavascriptInterface public void reportError(String message) {
            Log.e("FruitSlice", message == null ? "Game error" : message.substring(0, Math.min(message.length(), 2000)));
            handler.post(() -> showFailure("游戏加载遇到问题，请返回大厅后重试。"));
        }
    }

    private void showFailure(String message) {
        if (destroyed || isFinishing() || failed || root == null) return;
        failed = true;
        handler.removeCallbacks(loadTimeout);
        callGame("shutdown()");
        releaseBrowser();
        root.removeAllViews();
        LinearLayout panel = new LinearLayout(this);
        panel.setOrientation(LinearLayout.VERTICAL);
        panel.setGravity(Gravity.CENTER);
        int padding = (int) (24 * getResources().getDisplayMetrics().density);
        panel.setPadding(padding, padding, padding, padding);
        TextView text = new TextView(this);
        text.setText(message); text.setTextColor(Color.WHITE); text.setTextSize(18); text.setGravity(Gravity.CENTER);
        Button exit = new Button(this); exit.setText("返回大厅"); exit.setOnClickListener(view -> finish());
        panel.addView(text); panel.addView(exit);
        root.addView(panel, new FrameLayout.LayoutParams(-1, -1));
    }

    private void releaseBrowser() {
        if (web == null) return;
        WebView old = web; web = null;
        root.removeView(old);
        old.removeJavascriptInterface("XanaduFruitNative");
        old.stopLoading();
        old.onPause();
        old.setWebChromeClient(null);
        old.setWebViewClient(new WebViewClient());
        old.destroy();
    }

    @Override protected void onDestroy() {
        callGame("shutdown()");
        destroyed = true;
        handler.removeCallbacksAndMessages(null);
        if (backToast != null) backToast.cancel();
        if (Build.VERSION.SDK_INT >= 33 && backCallback != null)
            getOnBackInvokedDispatcher().unregisterOnBackInvokedCallback(backCallback);
        releaseBrowser();
        FruitSliceBridge.stopped(token);
        super.onDestroy();
    }
}
