package com.xanaduyonder.leveldevil;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.ComponentName;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.content.res.Configuration;
import android.graphics.Color;
import android.graphics.Insets;
import android.media.AudioAttributes;
import android.media.AudioFocusRequest;
import android.media.AudioManager;
import android.net.Uri;
import android.net.http.SslError;
import android.os.Build;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.os.SystemClock;
import android.util.Log;
import android.view.DisplayCutout;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.view.WindowInsets;
import android.view.WindowInsetsController;
import android.view.WindowManager;
import android.webkit.CookieManager;
import android.webkit.ConsoleMessage;
import android.webkit.GeolocationPermissions;
import android.webkit.JsPromptResult;
import android.webkit.JsResult;
import android.webkit.PermissionRequest;
import android.webkit.RenderProcessGoneDetail;
import android.webkit.ServiceWorkerClient;
import android.webkit.ServiceWorkerController;
import android.webkit.ServiceWorkerWebSettings;
import android.webkit.SslErrorHandler;
import android.webkit.ValueCallback;
import android.webkit.WebChromeClient;
import android.webkit.WebResourceError;
import android.webkit.WebResourceRequest;
import android.webkit.WebResourceResponse;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.widget.FrameLayout;
import android.widget.TextView;
import android.widget.Toast;
import android.window.OnBackInvokedCallback;
import android.window.OnBackInvokedDispatcher;
import androidx.webkit.WebViewAssetLoader;
import androidx.webkit.WebViewCompat;
import androidx.webkit.WebViewFeature;
import java.io.IOException;
import java.util.Collections;
import org.json.JSONException;
import org.json.JSONObject;

/** Fullscreen local game host. No game rule, progress or external navigation lives here. */
public final class LevelDevilActivity extends Activity {
    private static final String TAG = "LevelDevil";
    private static final String PROMPT_PREFIX = "xanadu-level-devil:";
    private static final long EXIT_TIMEOUT_MILLIS = 10000;
    private static final ServiceWorkerClient BLOCK_ALL_WORKERS = new ServiceWorkerClient() {
        @Override public WebResourceResponse shouldInterceptRequest(WebResourceRequest request) {
            return LevelDevilAssets.denied();
        }
    };

    private final Handler handler = new Handler(Looper.getMainLooper());
    private final LevelDevilBackGate backGate = new LevelDevilBackGate();
    private WebView web;
    private FrameLayout viewport;
    private TextView loading;
    private LevelDevilAssets assets;
    private WebViewAssetLoader loader;
    private AudioManager audioManager;
    private AudioFocusRequest audioFocus;
    private OnBackInvokedCallback backCallback;
    private AlertDialog errorDialog;
    private AlertDialog exitDialog;
    private long session = -1;
    private int exitSequence;
    private String pendingExit;
    private boolean exitFailurePending;
    private boolean soundEnabled = true;
    private boolean audioGranted;
    private boolean resumed;
    private boolean windowFocused;
    private boolean pageLoaded;
    private boolean webPaused;
    private boolean ready;
    private boolean fatal;
    private boolean destroyed;
    private boolean listenerInstalled;
    private boolean finishingToLobby;
    private long createdElapsed;
    private int safeLeft, safeTop, safeRight, safeBottom;

    private void trace(String event, String detail) {
        long now = SystemClock.elapsedRealtime();
        Log.i(TAG, "session=" + session + " event=" + event + " elapsed=" + now
                + " sinceCreateMs=" + (now - createdElapsed) + " " + detail);
    }

    private void resetBackGate(String reason) {
        trace("back_reset", "reason=" + reason);
        backGate.reset();
    }

    private final Runnable startupTimeout = () -> {
        if (!ready && !destroyed && !fatal) fail(new IOException("Local game readiness timed out"));
    };
    private final Runnable exitTimeout = () -> {
        if (pendingExit != null && !destroyed) showExitFailure();
    };

    @Override protected void onCreate(Bundle savedState) {
        createdElapsed = SystemClock.elapsedRealtime();
        super.onCreate(savedState);
        soundEnabled = getIntent().getBooleanExtra("soundEnabled", true);
        session = LevelDevilBridge.attach(this, getIntent().getLongExtra(LevelDevilBridge.SESSION_EXTRA, -1));
        if (session < 0) { finish(); return; }
        getIntent().putExtra(LevelDevilBridge.SESSION_EXTRA, session);
        trace("create", "restored=" + (savedState != null));
        try {
            getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
            configureWindow();
            installBackCallback();
            buildViewport();
            trace("viewport_created", "");
            configureAudio();
            assets = new LevelDevilAssets(getAssets());
            trace("manifest_loaded", "files=" + assets.paths().size());
            loader = new WebViewAssetLoader.Builder().setDomain(LevelDevilAssetPolicy.HOST)
                    .setHttpAllowed(false).addPathHandler(LevelDevilAssetPolicy.PREFIX, path -> {
                        long started = SystemClock.elapsedRealtime();
                        try {
                            WebResourceResponse response = assets.load(path);
                            trace("asset_loaded", "path=" + path + " bytes=" + assets.size(path)
                                    + " readAndHashMs=" + (SystemClock.elapsedRealtime() - started));
                            return response;
                        }
                        catch (IOException error) {
                            handler.post(() -> fail(error));
                            return LevelDevilAssets.denied();
                        }
                    }).build();
            configureWebView();
            trace("webview_configured", "");
            web.loadUrl(LevelDevilAssetPolicy.ENTRY);
            trace("navigation_requested", "");
            updateTimeouts();
        } catch (RuntimeException | LinkageError | IOException | JSONException error) {
            fail(error);
        }
    }

    private void configureWindow() {
        getWindow().setNavigationBarColor(Color.TRANSPARENT);
        getWindow().setStatusBarColor(Color.TRANSPARENT);
        if (Build.VERSION.SDK_INT >= 28) {
            WindowManager.LayoutParams attributes = getWindow().getAttributes();
            attributes.layoutInDisplayCutoutMode = WindowManager.LayoutParams.LAYOUT_IN_DISPLAY_CUTOUT_MODE_SHORT_EDGES;
            getWindow().setAttributes(attributes);
        }
        if (Build.VERSION.SDK_INT >= 30) getWindow().setDecorFitsSystemWindows(false);
    }

    @SuppressWarnings("deprecation")
    private void hideSystemBars() {
        // PhoneWindow has no DecorView before setContentView. The content attach
        // callback establishes a real window before accessing its insets controller.
        if (destroyed || finishingToLobby || viewport == null || !viewport.isAttachedToWindow()) return;
        if (Build.VERSION.SDK_INT >= 30) {
            WindowInsetsController controller = viewport.getWindowInsetsController();
            if (controller != null) {
                if (Build.VERSION.SDK_INT >= 31) {
                    controller.setSystemBarsBehavior(WindowInsetsController.BEHAVIOR_DEFAULT);
                } else {
                    controller.setSystemBarsBehavior(WindowInsetsController.BEHAVIOR_SHOW_BARS_BY_SWIPE);
                }
                // OEM game modes can consume a swipe while navigation is hidden.
                // Keep the transparent system control reachable while drawing edge to edge.
                controller.show(WindowInsets.Type.navigationBars());
                controller.hide(WindowInsets.Type.statusBars());
            }
        } else {
            // Preserve direct system Back on older Android, including Android 10's
            // gesture navigation. Fullscreen content still respects navigation insets.
            viewport.getRootView().setSystemUiVisibility(View.SYSTEM_UI_FLAG_FULLSCREEN
                    | View.SYSTEM_UI_FLAG_LAYOUT_STABLE | View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN
                    | View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION);
        }
    }

    private void buildViewport() {
        viewport = new FrameLayout(this);
        viewport.setBackgroundColor(Color.BLACK);
        viewport.addOnAttachStateChangeListener(new View.OnAttachStateChangeListener() {
            @Override public void onViewAttachedToWindow(View view) {
                view.removeOnAttachStateChangeListener(this);
                view.requestApplyInsets();
                hideSystemBars();
            }
            @Override public void onViewDetachedFromWindow(View view) { }
        });
        viewport.setOnApplyWindowInsetsListener((view, insets) -> {
            int left = insets.getSystemWindowInsetLeft(), top = insets.getSystemWindowInsetTop();
            int right = insets.getSystemWindowInsetRight(), bottom = insets.getSystemWindowInsetBottom();
            if (Build.VERSION.SDK_INT >= 28) {
                DisplayCutout cutout = insets.getDisplayCutout();
                if (cutout != null) {
                    left = Math.max(left, cutout.getSafeInsetLeft()); top = Math.max(top, cutout.getSafeInsetTop());
                    right = Math.max(right, cutout.getSafeInsetRight()); bottom = Math.max(bottom, cutout.getSafeInsetBottom());
                }
            }
            if (Build.VERSION.SDK_INT >= 29) {
                Insets gestures = insets.getSystemGestureInsets();
                left = Math.max(left, gestures.left); top = Math.max(top, gestures.top);
                right = Math.max(right, gestures.right); bottom = Math.max(bottom, gestures.bottom);
            }
            safeLeft = left; safeTop = top; safeRight = right; safeBottom = bottom;
            syncViewportInsets();
            return insets;
        });
        loading = new TextView(this);
        loading.setText("正在打开游戏…");
        loading.setTextColor(Color.WHITE);
        loading.setGravity(Gravity.CENTER);
        loading.setBackgroundColor(Color.BLACK);
        viewport.addView(loading, new FrameLayout.LayoutParams(-1, -1));
        setContentView(viewport);
        viewport.requestApplyInsets();
    }

    @SuppressWarnings("deprecation")
    private void configureWebView() {
        trace("webview_initialization_begin", "");
        WebView.setWebContentsDebuggingEnabled(false);
        web = new WebView(this);
        trace("webview_created", "");
        web.setBackgroundColor(Color.BLACK);
        web.setOverScrollMode(View.OVER_SCROLL_NEVER);
        web.setHapticFeedbackEnabled(false);
        web.setOnLongClickListener(view -> true);
        WebSettings settings = web.getSettings();
        settings.setJavaScriptEnabled(true);
        settings.setDomStorageEnabled(true);
        settings.setDatabaseEnabled(false);
        settings.setBlockNetworkLoads(true);
        settings.setAllowFileAccess(false);
        settings.setAllowContentAccess(false);
        settings.setAllowFileAccessFromFileURLs(false);
        settings.setAllowUniversalAccessFromFileURLs(false);
        settings.setMixedContentMode(WebSettings.MIXED_CONTENT_NEVER_ALLOW);
        settings.setJavaScriptCanOpenWindowsAutomatically(false);
        settings.setSupportMultipleWindows(false);
        settings.setGeolocationEnabled(false);
        settings.setSaveFormData(false);
        settings.setSafeBrowsingEnabled(false);
        settings.setCacheMode(WebSettings.LOAD_NO_CACHE);
        settings.setSupportZoom(false);
        settings.setBuiltInZoomControls(false);
        settings.setDisplayZoomControls(false);
        settings.setMediaPlaybackRequiresUserGesture(true);
        CookieManager.getInstance().setAcceptCookie(false);
        CookieManager.getInstance().setAcceptThirdPartyCookies(web, false);
        ServiceWorkerController workers = ServiceWorkerController.getInstance();
        ServiceWorkerWebSettings workerSettings = workers.getServiceWorkerWebSettings();
        workerSettings.setBlockNetworkLoads(true);
        workerSettings.setAllowContentAccess(false);
        workerSettings.setAllowFileAccess(false);
        workers.setServiceWorkerClient(BLOCK_ALL_WORKERS);
        web.setWebViewClient(new LocalClient());
        web.setWebChromeClient(new LocalChromeClient());
        web.setDownloadListener((url, userAgent, disposition, mime, length) -> Log.w(TAG, "Blocked download"));
        if (WebViewFeature.isFeatureSupported(WebViewFeature.WEB_MESSAGE_LISTENER)) {
            WebViewCompat.addWebMessageListener(web, "LevelDevilNative",
                    Collections.singleton(LevelDevilAssetPolicy.ORIGIN), (view, message, origin, mainFrame, reply) -> {
                        if (!mainFrame || !LevelDevilAssetPolicy.ORIGIN.equals(origin.toString())
                                || !LevelDevilAssetPolicy.ENTRY.equals(view.getUrl())) return;
                        try { receiveMessage(message.getData()); }
                        catch (IllegalStateException error) { Log.w(TAG, "Rejected non-string game message"); }
                    });
            listenerInstalled = true;
        }
        // On older providers the packaged adapter uses a prefixed prompt. No Java
        // interface is exposed to every frame; CSP prohibits all child frames.
        viewport.addView(web, 0, new FrameLayout.LayoutParams(-1, -1, Gravity.CENTER));
        web.addOnLayoutChangeListener((view, left, top, right, bottom, oldLeft, oldTop, oldRight, oldBottom) -> {
            if (left != oldLeft || top != oldTop || right != oldRight || bottom != oldBottom) syncViewportInsets();
        });
    }

    private final class LocalClient extends WebViewClient {
        @Override public void onPageStarted(WebView view, String url, android.graphics.Bitmap favicon) {
            if (LevelDevilAssetPolicy.ENTRY.equals(url)) trace("page_started", "");
        }

        @Override public void onPageCommitVisible(WebView view, String url) {
            if (LevelDevilAssetPolicy.ENTRY.equals(url)) trace("page_commit_visible", "");
        }

        @Override public WebResourceResponse shouldInterceptRequest(WebView view, WebResourceRequest request) {
            String path = LevelDevilAssetPolicy.pathFor(request.getUrl().toString(), request.getMethod(), assets.paths());
            if (path == null) { Log.w(TAG, "Blocked non-manifest game request"); return LevelDevilAssets.denied(); }
            WebResourceResponse response = loader.shouldInterceptRequest(request.getUrl());
            return response == null ? LevelDevilAssets.denied() : response;
        }

        @Override public WebResourceResponse shouldInterceptRequest(WebView view, String url) {
            if (LevelDevilAssetPolicy.pathFor(url, "GET", assets.paths()) == null) return LevelDevilAssets.denied();
            WebResourceResponse response = loader.shouldInterceptRequest(Uri.parse(url));
            return response == null ? LevelDevilAssets.denied() : response;
        }

        @Override public boolean shouldOverrideUrlLoading(WebView view, WebResourceRequest request) {
            return !request.isForMainFrame() || !"GET".equals(request.getMethod())
                    || !LevelDevilAssetPolicy.ENTRY.equals(request.getUrl().toString());
        }

        @Override public boolean shouldOverrideUrlLoading(WebView view, String url) {
            return !LevelDevilAssetPolicy.ENTRY.equals(url);
        }

        @Override public void onPageFinished(WebView view, String url) {
            if (!LevelDevilAssetPolicy.ENTRY.equals(url)) fail(new IOException("Unexpected game navigation"));
            else {
                // The adapter exists before the first interactive engine tick. Send
                // the cached native state now too, including a background cold load.
                pageLoaded = true;
                trace("page_finished", "");
                syncViewportInsets();
                syncHostState();
            }
        }

        @Override public void onReceivedError(WebView view, WebResourceRequest request, WebResourceError error) {
            if (request.isForMainFrame()) fail(new IOException("Local game entry load failed"));
        }

        @Override public void onReceivedHttpError(WebView view, WebResourceRequest request, WebResourceResponse response) {
            if (request.isForMainFrame()) fail(new IOException("Local game entry was rejected"));
        }

        @Override public void onReceivedSslError(WebView view, SslErrorHandler callback, SslError error) {
            callback.cancel();
            fail(new IOException("Unexpected TLS request"));
        }

        @Override public boolean onRenderProcessGone(WebView view, RenderProcessGoneDetail detail) {
            if (view == web) {
                ready = false;
                try { disposeWebView(); }
                catch (RuntimeException error) { Log.w(TAG, "Stopped renderer cleanup failed", error); }
                fail(new IOException("Game renderer stopped"));
            }
            return true;
        }
    }

    private final class LocalChromeClient extends WebChromeClient {
        @Override public boolean onConsoleMessage(ConsoleMessage message) {
            String value = message.message();
            if (value != null && value.startsWith("LevelDevilStartup:") && value.length() <= 2048) {
                trace("web_startup", value);
                return true;
            }
            return super.onConsoleMessage(message);
        }

        @Override public void onPermissionRequest(PermissionRequest request) { request.deny(); }
        @Override public void onGeolocationPermissionsShowPrompt(String origin, GeolocationPermissions.Callback callback) {
            callback.invoke(origin, false, false);
        }
        @Override public boolean onShowFileChooser(WebView view, ValueCallback<Uri[]> callback, FileChooserParams params) {
            callback.onReceiveValue(null);
            return true;
        }
        @Override public boolean onCreateWindow(WebView view, boolean dialog, boolean userGesture, android.os.Message message) {
            return false;
        }
        @Override public boolean onJsAlert(WebView view, String url, String message, JsResult result) { result.cancel(); return true; }
        @Override public boolean onJsConfirm(WebView view, String url, String message, JsResult result) { result.cancel(); return true; }
        @Override public boolean onJsBeforeUnload(WebView view, String url, String message, JsResult result) { result.cancel(); return true; }
        @Override public boolean onJsPrompt(WebView view, String url, String message, String defaultValue, JsPromptResult result) {
            if (!listenerInstalled && LevelDevilAssetPolicy.ENTRY.equals(url)
                    && LevelDevilAssetPolicy.ENTRY.equals(view.getUrl()) && message != null
                    && message.startsWith(PROMPT_PREFIX)) {
                receiveMessage(message.substring(PROMPT_PREFIX.length()));
                result.confirm("");
            } else result.cancel();
            return true;
        }
    }

    private void receiveMessage(String raw) {
        if (destroyed || fatal || finishingToLobby || raw == null || raw.length() > 16384) return;
        try {
            JSONObject message = new JSONObject(raw);
            String type = message.getString("type");
            switch (type) {
                case "ready":
                    if (!ready) {
                        ready = true;
                        trace("ready", "");
                        handler.removeCallbacks(startupTimeout);
                        syncViewportInsets();
                        loading.setVisibility(View.GONE);
                        syncHostState();
                        updateTimeouts();
                    }
                    break;
                case "exit-intent":
                    trace("exit_intent", "source=game_button");
                    beginExit();
                    break;
                case "exit-ready":
                    if (pendingExit != null && pendingExit.equals(message.optString("requestId", ""))) {
                        pendingExit = null;
                        trace("exit_ready", "");
                        handler.removeCallbacks(exitTimeout);
                        finishToLobby();
                    }
                    break;
                case "exit-error":
                    if (pendingExit != null && pendingExit.equals(message.optString("requestId", ""))) showExitFailure();
                    break;
                case "error":
                    if (message.optBoolean("recoverable", false)) {
                        // The adapter owns a persistent save warning. A native modal
                        // here would steal focus and turn a recoverable write failure
                        // into a paused game or an unrelated launch error.
                        Toast.makeText(this, "存档状态需要注意，请查看游戏内提示。", Toast.LENGTH_LONG).show();
                    } else fail(new IOException("Game adapter reported an error"));
                    break;
                default:
                    Log.w(TAG, "Rejected unknown game message");
            }
        } catch (JSONException error) { Log.w(TAG, "Rejected malformed game message"); }
    }

    private void installBackCallback() {
        if (Build.VERSION.SDK_INT >= 33) {
            backCallback = () -> {
                trace("back_dispatch", "source=onBackInvoked");
                committedBack();
            };
            getOnBackInvokedDispatcher().registerOnBackInvokedCallback(OnBackInvokedDispatcher.PRIORITY_DEFAULT, backCallback);
        }
    }

    @Override public void onBackPressed() {
        // OEM/user settings can choose this route even on API 33+.
        trace("back_dispatch", "source=onBackPressed");
        committedBack();
    }

    private void committedBack() {
        if (destroyed || finishingToLobby || pendingExit != null || exitFailurePending || exitDialog != null) {
            trace("back_ignored", "destroyed=" + destroyed + " finishing=" + finishingToLobby
                    + " pending=" + (pendingExit != null) + " saveFailure=" + exitFailurePending);
            return;
        }
        long now = SystemClock.uptimeMillis();
        boolean exit = backGate.shouldExit(now);
        trace("back_decision", "uptime=" + now + " exit=" + exit
                + " resumed=" + resumed + " focused=" + windowFocused + " ready=" + ready);
        if (exit) {
            if (!ready || fatal) finishToLobby();
            else beginExit();
        } else Toast.makeText(this, "再返回一次，回到大厅", Toast.LENGTH_SHORT).show();
    }

    private void beginExit() {
        if (destroyed || fatal || !ready || web == null || pendingExit != null) return;
        exitFailurePending = false;
        final String request = "exit-" + session + "-" + (++exitSequence);
        pendingExit = request;
        trace("exit_requested", "request=" + request);
        releaseInput();
        updateTimeouts();
        // The adapter stops player input while allowing pending completion/checkpoint
        // work to finish. Only actual host inactivity may suspend that work here.
        evaluate("(function(){var h=window.LevelDevilHost;if(!h||typeof h.requestExit!=='function')return false;"
                + "h.requestExit(" + JSONObject.quote(request) + ");return true;})()", value -> {
                    if (request.equals(pendingExit) && !"true".equals(value)) showExitFailure();
                });
    }

    private void showExitFailure() {
        if (fatal || destroyed) return;
        handler.removeCallbacks(exitTimeout);
        pendingExit = null;
        exitFailurePending = true;
        resetBackGate("save_failed");
        if (!resumed || !windowFocused || destroyed || isFinishing() || exitDialog != null) return;
        exitDialog = new AlertDialog.Builder(this).setTitle("进度尚未保存")
                .setMessage("暂时无法确认保存完成。可以重试保存，或留在游戏中继续。")
                .setCancelable(false)
                .setPositiveButton("重试保存并返回", (dialog, which) -> {
                    exitDialog = null;
                    exitFailurePending = false;
                    cancelGameExit();
                    beginExit();
                })
                .setNegativeButton("留在游戏", (dialog, which) -> {
                    exitDialog = null;
                    exitFailurePending = false;
                    cancelGameExit();
                    syncHostState();
                }).create();
        exitDialog.show();
    }

    private void cancelGameExit() {
        evaluate("if(window.LevelDevilHost)window.LevelDevilHost.cancelExit();", null);
    }

    private void releaseInput() {
        if (pageLoaded || ready) evaluate("if(window.LevelDevilHost)window.LevelDevilHost.releaseInput();", null);
    }

    private void syncViewportInsets() {
        if ((!pageLoaded && !ready) || destroyed || web == null) return;
        evaluate("(function(){var h=window.LevelDevilHost,d=window.devicePixelRatio||1;"
                + "if(h&&typeof h.setViewportInsets==='function')h.setViewportInsets("
                + safeLeft + "/d," + safeTop + "/d," + safeRight + "/d," + safeBottom + "/d);})()", null);
    }

    private void syncHostState() {
        if ((!pageLoaded && !ready) || destroyed || web == null) return;
        boolean active = resumed && windowFocused && !fatal && exitDialog == null;
        boolean audible = soundEnabled && audioGranted;
        evaluate("if(window.LevelDevilHost)window.LevelDevilHost.setHostState(" + active + "," + audible + ");", null);
    }

    private void updateTimeouts() {
        handler.removeCallbacks(startupTimeout);
        handler.removeCallbacks(exitTimeout);
        // Background/focus loss deliberately stops game ticks. It is not a failed
        // load or save. Grant a fresh bounded foreground window when returning.
        if (destroyed || fatal || !resumed || !windowFocused) return;
        if (!ready) handler.postDelayed(startupTimeout, 30000);
        if (pendingExit != null) handler.postDelayed(exitTimeout, EXIT_TIMEOUT_MILLIS);
    }

    private void evaluate(String source, ValueCallback<String> callback) {
        if (web == null || destroyed) return;
        try { web.evaluateJavascript(source, callback); }
        catch (RuntimeException error) { fail(error); }
    }

    private void configureAudio() {
        setVolumeControlStream(AudioManager.STREAM_MUSIC);
        audioManager = (AudioManager) getSystemService(AUDIO_SERVICE);
        if (audioManager == null) return;
        audioFocus = new AudioFocusRequest.Builder(AudioManager.AUDIOFOCUS_GAIN)
                .setAudioAttributes(new AudioAttributes.Builder().setUsage(AudioAttributes.USAGE_GAME)
                        .setContentType(AudioAttributes.CONTENT_TYPE_MUSIC).build())
                .setOnAudioFocusChangeListener(change -> {
                    if (destroyed || !resumed) return;
                    audioGranted = change == AudioManager.AUDIOFOCUS_GAIN;
                    syncHostState();
                }, handler).build();
    }

    private void requestAudio() {
        if (!soundEnabled || audioManager == null || audioFocus == null || audioGranted) return;
        try { audioGranted = audioManager.requestAudioFocus(audioFocus) == AudioManager.AUDIOFOCUS_REQUEST_GRANTED; }
        catch (RuntimeException error) { audioGranted = false; Log.w(TAG, "Audio focus unavailable"); }
    }

    private void abandonAudio() {
        audioGranted = false;
        if (audioManager != null && audioFocus != null) {
            try { audioManager.abandonAudioFocusRequest(audioFocus); }
            catch (RuntimeException error) { Log.w(TAG, "Audio focus was already unavailable"); }
        }
    }

    @Override protected void onResume() {
        super.onResume();
        resumed = true;
        trace("resume", "focused=" + windowFocused);
        setWebPaused(!windowFocused);
        requestAudio();
        syncHostState();
        updateTimeouts();
        if (fatal) showFatalDialog();
    }

    @Override protected void onPause() {
        resumed = false;
        trace("pause", "focused=" + windowFocused);
        resetBackGate("pause");
        updateTimeouts();
        releaseInput();
        syncHostState();
        setWebPaused(true);
        abandonAudio();
        super.onPause();
    }

    @Override public void onWindowFocusChanged(boolean focused) {
        super.onWindowFocusChanged(focused);
        windowFocused = focused;
        trace("focus", "focused=" + focused + " resumed=" + resumed);
        if (!focused) { resetBackGate("focus_lost"); releaseInput(); }
        else if (resumed) { hideSystemBars(); requestAudio(); }
        syncHostState();
        setWebPaused(!focused || !resumed);
        updateTimeouts();
        if (focused && resumed && exitFailurePending) handler.post(this::showExitFailure);
        if (focused && resumed && fatal) handler.post(this::showFatalDialog);
    }

    @Override public void onConfigurationChanged(Configuration configuration) {
        super.onConfigurationChanged(configuration);
        trace("configuration", "orientation=" + configuration.orientation
                + " widthDp=" + configuration.screenWidthDp + " heightDp=" + configuration.screenHeightDp);
        resetBackGate("configuration");
        releaseInput();
        if (viewport != null) { viewport.requestApplyInsets(); viewport.requestLayout(); }
        syncViewportInsets();
        hideSystemBars();
    }

    private void fail(Throwable error) {
        if (destroyed || fatal || finishingToLobby) return;
        fatal = true;
        handler.removeCallbacks(startupTimeout);
        handler.removeCallbacks(exitTimeout);
        pendingExit = null;
        exitFailurePending = false;
        releaseInput();
        syncHostState();
        LevelDevilBridge.reportError(session, this, error);
        if (loading != null) { loading.setText("游戏暂时无法打开"); loading.setVisibility(View.VISIBLE); }
        showFatalDialog();
    }

    private void showFatalDialog() {
        if (!fatal || !resumed || destroyed || isFinishing() || errorDialog != null) return;
        if (exitDialog != null) { exitDialog.dismiss(); exitDialog = null; }
        errorDialog = new AlertDialog.Builder(this).setTitle("暂时无法打开")
                .setMessage("游戏页面无法继续运行，请返回大厅后重试。")
                .setCancelable(false).setPositiveButton("返回大厅", (dialog, which) -> finishToLobby()).create();
        errorDialog.show();
    }

    private void finishToLobby() {
        if (destroyed || finishingToLobby) return;
        finishingToLobby = true;
        handler.removeCallbacks(exitTimeout);
        handler.removeCallbacks(startupTimeout);
        // A process-restored game can become the task root. Recreate the real Unity
        // hall explicitly before removing it; the launcher trampoline itself would
        // otherwise see this Activity underneath and correctly decline a second hall.
        if (isTaskRoot()) {
            try {
                Bundle metadata = getPackageManager().getActivityInfo(new ComponentName(this,
                        "com.duanyao.xanaduyonder.XanaduLauncherActivity"), PackageManager.GET_META_DATA).metaData;
                String unity = metadata == null ? null : metadata.getString("xanadu.unity_activity");
                if (unity == null) unity = "com.unity3d.player.UnityPlayerActivity";
                startActivity(new Intent().setClassName(this, unity)
                        .addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP | Intent.FLAG_ACTIVITY_SINGLE_TOP));
            } catch (PackageManager.NameNotFoundException | RuntimeException error) {
                Log.e(TAG, "Unable to restore the hall task", error);
            }
        }
        finish();
    }

    private void disposeWebView() {
        WebView previous = web;
        web = null;
        if (previous == null) return;
        if (viewport != null) viewport.removeView(previous);
        if (listenerInstalled) {
            try { WebViewCompat.removeWebMessageListener(previous, "LevelDevilNative"); }
            catch (RuntimeException error) { Log.w(TAG, "Message listener was already unavailable"); }
        }
        listenerInstalled = false;
        previous.stopLoading();
        previous.setWebChromeClient(null);
        previous.setWebViewClient(null);
        previous.destroy();
    }

    private void setWebPaused(boolean paused) {
        if (web == null || webPaused == paused) return;
        webPaused = paused;
        if (paused) web.onPause(); else web.onResume();
    }

    @Override protected void onDestroy() {
        destroyed = true;
        handler.removeCallbacksAndMessages(null);
        try {
            if (Build.VERSION.SDK_INT >= 33 && backCallback != null) {
                getOnBackInvokedDispatcher().unregisterOnBackInvokedCallback(backCallback);
            }
        } catch (RuntimeException error) { Log.w(TAG, "Back callback was already unavailable", error); }
        try {
            if (exitDialog != null) { exitDialog.dismiss(); exitDialog = null; }
            if (errorDialog != null) { errorDialog.dismiss(); errorDialog = null; }
        } catch (RuntimeException error) { Log.w(TAG, "Game dialogs were already unavailable", error); }
        abandonAudio();
        try {
            disposeWebView();
        } catch (RuntimeException error) {
            Log.w(TAG, "Game host cleanup was interrupted", error);
        }
        finally {
            LevelDevilBridge.stopped(session, this);
            super.onDestroy();
        }
    }

}
