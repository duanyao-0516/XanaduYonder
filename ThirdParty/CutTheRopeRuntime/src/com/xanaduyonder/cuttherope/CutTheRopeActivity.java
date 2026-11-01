package com.xanaduyonder.cuttherope;

import android.annotation.SuppressLint;
import android.app.Activity;
import android.content.Context;
import android.content.SharedPreferences;
import android.content.res.Configuration;
import android.graphics.Color;
import android.graphics.drawable.ColorDrawable;
import android.media.AudioAttributes;
import android.media.AudioFocusRequest;
import android.media.AudioManager;
import android.net.http.SslError;
import android.os.Build;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.os.Message;
import android.os.SystemClock;
import android.util.Log;
import android.view.MotionEvent;
import android.view.View;
import android.view.ViewGroup;
import android.view.WindowInsets;
import android.view.WindowInsetsController;
import android.view.WindowManager;
import android.webkit.ConsoleMessage;
import android.webkit.CookieManager;
import android.webkit.JavascriptInterface;
import android.webkit.PermissionRequest;
import android.webkit.RenderProcessGoneDetail;
import android.webkit.SslErrorHandler;
import android.webkit.WebChromeClient;
import android.webkit.WebResourceError;
import android.webkit.WebResourceRequest;
import android.webkit.WebResourceResponse;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.widget.FrameLayout;
import android.widget.Toast;
import android.window.OnBackInvokedCallback;
import android.window.OnBackInvokedDispatcher;
import java.io.ByteArrayInputStream;
import java.io.IOException;
import java.io.InputStream;
import java.nio.charset.StandardCharsets;
import java.util.HashMap;
import java.util.Map;

/** Offline game host. No Unity UI or other game's runtime state is shared here. */
public final class CutTheRopeActivity extends Activity {
    private static final long START_TIMEOUT_MS = 60000L;
    private static final long FLUSH_TIMEOUT_MS = 3000L;
    private final Handler main = new Handler(Looper.getMainLooper());
    private final CompletedBackPolicy backPolicy = new CompletedBackPolicy();
    private WebView web;
    private FrameLayout content;
    private AudioManager audio;
    private AudioFocusRequest audioRequest;
    private Object backRegistration;
    private Toast backToast;
    private boolean ownsSession;
    private boolean ready;
    private boolean resumed;
    private boolean focused;
    private boolean soundEnabled;
    private boolean audioFocused;
    private boolean closing;
    private volatile boolean destroyed;
    private int lifecycleRevision;
    private final Runnable startupTimeout = () -> fail("割绳子加载超时：离线游戏未报告 ready");
    private final Runnable flushTimeout = () -> fail("割绳子退出保存失败：游戏未在 3 秒内完成保存");

    @Override protected void onCreate(Bundle savedState) {
        super.onCreate(savedState);
        ownsSession = CutTheRopeBridge.started(this);
        if (!ownsSession) { finish(); return; }
        soundEnabled = getIntent().getBooleanExtra("soundEnabled", true);
        try {
            getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
            getWindow().setBackgroundDrawable(new ColorDrawable(Color.BLACK));
            if (Build.VERSION.SDK_INT >= 28) {
                WindowManager.LayoutParams attributes = getWindow().getAttributes();
                attributes.layoutInDisplayCutoutMode = Build.VERSION.SDK_INT >= 30
                        ? WindowManager.LayoutParams.LAYOUT_IN_DISPLAY_CUTOUT_MODE_ALWAYS
                        : WindowManager.LayoutParams.LAYOUT_IN_DISPLAY_CUTOUT_MODE_SHORT_EDGES;
                getWindow().setAttributes(attributes);
            }
            setupAudio();
            setupContent();
            if (Build.VERSION.SDK_INT >= 33) backRegistration = BackApi33.register(this, this::completedBack);
            applyFullscreen();
            main.postDelayed(startupTimeout, START_TIMEOUT_MS);
            web.loadUrl(OfflineAssetPolicy.START_URL);
        } catch (RuntimeException | LinkageError error) {
            fail("割绳子启动失败：" + error);
        }
    }

    private void setupAudio() {
        audio = (AudioManager) getSystemService(Context.AUDIO_SERVICE);
        if (audio == null) throw new IllegalStateException("Android audio service unavailable");
        audioRequest = new AudioFocusRequest.Builder(AudioManager.AUDIOFOCUS_GAIN)
                .setAudioAttributes(new AudioAttributes.Builder().setUsage(AudioAttributes.USAGE_GAME)
                        .setContentType(AudioAttributes.CONTENT_TYPE_MUSIC).build())
                .setAcceptsDelayedFocusGain(false)
                .setWillPauseWhenDucked(true)
                .setOnAudioFocusChangeListener(change -> {
                    audioFocused = change == AudioManager.AUDIOFOCUS_GAIN;
                    if (!audioFocused) { backPolicy.reset(); cancelTouches(); }
                    applyHostState();
                }, main).build();
    }

    @SuppressLint({"SetJavaScriptEnabled", "AddJavascriptInterface"})
    private void setupContent() {
        content = new FrameLayout(this);
        content.setBackgroundColor(Color.BLACK);
        // The game lays out its scene proportionally in the full viewport. System gesture
        // regions stay owned by Android; they must not become margins around the canvas.
        web = new WebView(this);
        web.setBackgroundColor(Color.BLACK);
        web.setHorizontalScrollBarEnabled(false);
        web.setVerticalScrollBarEnabled(false);
        web.setOverScrollMode(View.OVER_SCROLL_NEVER);
        web.setLongClickable(false);
        web.setOnLongClickListener(view -> true);
        web.setHapticFeedbackEnabled(false);
        WebSettings settings = web.getSettings();
        settings.setJavaScriptEnabled(true);
        settings.setDomStorageEnabled(true);
        settings.setBlockNetworkLoads(true);
        settings.setAllowFileAccess(false);
        settings.setAllowContentAccess(false);
        settings.setAllowFileAccessFromFileURLs(false);
        settings.setAllowUniversalAccessFromFileURLs(false);
        settings.setMixedContentMode(WebSettings.MIXED_CONTENT_NEVER_ALLOW);
        settings.setSupportMultipleWindows(false);
        settings.setJavaScriptCanOpenWindowsAutomatically(false);
        settings.setSupportZoom(false);
        settings.setBuiltInZoomControls(false);
        settings.setDisplayZoomControls(false);
        settings.setTextZoom(100);
        settings.setMediaPlaybackRequiresUserGesture(true);
        settings.setCacheMode(WebSettings.LOAD_NO_CACHE);
        CookieManager.getInstance().setAcceptThirdPartyCookies(web, false);
        web.addJavascriptInterface(new NativeBridge(getSharedPreferences("cut-the-rope-offline-v1", MODE_PRIVATE)), "CutTheRopeNative");
        web.setWebViewClient(new OfflineClient());
        web.setWebChromeClient(new WebChromeClient() {
            @Override public boolean onCreateWindow(WebView view, boolean dialog, boolean gesture, Message result) { return false; }
            @Override public void onPermissionRequest(PermissionRequest request) { request.deny(); }
            @Override public boolean onConsoleMessage(ConsoleMessage message) {
                if (message.messageLevel() == ConsoleMessage.MessageLevel.ERROR) {
                    Log.e("CutTheRope", message.message() + " (" + message.sourceId() + ":" + message.lineNumber() + ")");
                }
                return true;
            }
        });
        web.setDownloadListener((url, agent, disposition, mime, length) -> Log.w("CutTheRope", "Blocked download: " + url));
        content.addView(web, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));
        setContentView(content);
        content.requestApplyInsets();
    }

    @SuppressWarnings("deprecation")
    private void applyFullscreen() {
        if (Build.VERSION.SDK_INT >= 30) {
            getWindow().setDecorFitsSystemWindows(false);
            WindowInsetsController controller = getWindow().getInsetsController();
            if (controller != null) {
                controller.setSystemBarsBehavior(WindowInsetsController.BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE);
                controller.hide(WindowInsets.Type.systemBars());
            }
        } else {
            getWindow().getDecorView().setSystemUiVisibility(View.SYSTEM_UI_FLAG_LAYOUT_STABLE
                    | View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION | View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN
                    | View.SYSTEM_UI_FLAG_HIDE_NAVIGATION | View.SYSTEM_UI_FLAG_FULLSCREEN
                    | View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY);
        }
    }

    @Override protected void onResume() {
        super.onResume();
        resumed = true;
        lifecycleRevision++;
        backPolicy.reset();
        if (web != null) web.onResume();
        main.removeCallbacks(startupTimeout);
        if (ownsSession && !ready && !closing) main.postDelayed(startupTimeout, START_TIMEOUT_MS);
        requestAudioIfActive();
        applyFullscreen();
        applyHostState();
    }

    @Override public void onWindowFocusChanged(boolean hasFocus) {
        super.onWindowFocusChanged(hasFocus);
        focused = hasFocus;
        if (!hasFocus) {
            backPolicy.reset();
            cancelTouches();
            releaseAudio();
        } else {
            applyFullscreen();
            requestAudioIfActive();
        }
        applyHostState();
    }

    @Override protected void onPause() {
        resumed = false;
        main.removeCallbacks(startupTimeout);
        backPolicy.reset();
        cancelBackToast();
        cancelTouches();
        releaseAudio();
        int revision = ++lifecycleRevision;
        if (web != null && ready && !destroyed) {
            // Let the synchronous JS pause/flush call finish before pausing its WebView.
            evaluate("window.CutTheRopeOffline.setHostPaused(true);window.CutTheRopeOffline.setSoundEnabled(false);window.CutTheRopeOffline.flush();", ignored -> {
                if (web != null && !resumed && lifecycleRevision == revision) web.onPause();
            });
        } else if (web != null) web.onPause();
        super.onPause();
    }

    @Override public void onConfigurationChanged(Configuration configuration) {
        super.onConfigurationChanged(configuration);
        backPolicy.reset();
        cancelTouches();
        applyFullscreen();
        if (content != null) content.requestApplyInsets();
    }

    private void requestAudioIfActive() {
        if (audio != null && soundEnabled && resumed && focused && !closing && !audioFocused) {
            audioFocused = audio.requestAudioFocus(audioRequest) == AudioManager.AUDIOFOCUS_REQUEST_GRANTED;
        }
    }

    private void releaseAudio() {
        audioFocused = false;
        if (audio != null && audioRequest != null) audio.abandonAudioFocusRequest(audioRequest);
    }

    private void applyHostState() {
        if (!ready || web == null || destroyed || closing) return;
        boolean paused = !resumed || !focused || (soundEnabled && !audioFocused);
        boolean audible = soundEnabled && !paused;
        evaluate("window.CutTheRopeOffline.setHostPaused(" + paused + ");window.CutTheRopeOffline.setSoundEnabled(" + audible + ");", null);
    }

    private void cancelTouches() {
        if (web == null || destroyed) return;
        long now = SystemClock.uptimeMillis();
        MotionEvent cancel = MotionEvent.obtain(now, now, MotionEvent.ACTION_CANCEL, 0f, 0f, 0);
        try { web.dispatchTouchEvent(cancel); } finally { cancel.recycle(); }
    }

    // API 26–32 Activity dispatch only calls this after a completed key/gesture Back.
    @Override public void onBackPressed() { completedBack(); }

    private void completedBack() {
        if (!ownsSession || destroyed || closing) return;
        if (!backPolicy.onCompletedBack(SystemClock.elapsedRealtime())) {
            cancelBackToast();
            backToast = Toast.makeText(this, "再返回一次退出割绳子", Toast.LENGTH_SHORT);
            backToast.show();
            return;
        }
        cancelBackToast();
        closing = true;
        cancelTouches();
        main.removeCallbacks(startupTimeout);
        if (!ready || web == null) { finish(); return; }
        main.postDelayed(flushTimeout, FLUSH_TIMEOUT_MS);
        evaluate("window.CutTheRopeOffline.setHostPaused(true);window.CutTheRopeOffline.setSoundEnabled(false);return window.CutTheRopeOffline.flush() === true;", result -> {
            main.removeCallbacks(flushTimeout);
            if (destroyed || isFinishing()) return;
            if (!"true".equals(result)) {
                fail("割绳子退出保存失败：离线保存未确认成功（" + result + "）");
            } else finish();
        });
    }

    private void evaluate(String body, android.webkit.ValueCallback<String> callback) {
        if (web == null || destroyed) return;
        // A thrown exception is sent through the real error path, never swallowed as success.
        String script = "(function(){try{" + body + "}catch(e){CutTheRopeNative.reportError('Host bridge: '+(e.stack||e));return false;}})()";
        try { web.evaluateJavascript(script, callback); }
        catch (RuntimeException error) { fail("割绳子页面调用失败：" + error); }
    }

    private void cancelBackToast() {
        if (backToast != null) { backToast.cancel(); backToast = null; }
    }

    private void fail(String error) {
        if (Looper.myLooper() != Looper.getMainLooper()) { main.post(() -> fail(error)); return; }
        if (destroyed || isFinishing()) return;
        CutTheRopeBridge.reportError(error);
        closing = true;
        main.removeCallbacks(startupTimeout);
        main.removeCallbacks(flushTimeout);
        cancelBackToast();
        releaseAudio();
        finish();
    }

    @Override protected void onDestroy() {
        destroyed = true;
        main.removeCallbacksAndMessages(null);
        backPolicy.reset();
        cancelBackToast();
        releaseAudio();
        if (Build.VERSION.SDK_INT >= 33 && backRegistration != null) BackApi33.unregister(this, backRegistration);
        try {
            if (web != null) {
                web.removeJavascriptInterface("CutTheRopeNative");
                web.stopLoading();
                if (content != null) content.removeView(web);
                web.destroy();
                web = null;
            }
        } catch (RuntimeException error) {
            CutTheRopeBridge.reportError("割绳子关闭页面失败：" + error);
        } finally {
            if (ownsSession) CutTheRopeBridge.stopped(this);
            super.onDestroy();
        }
    }

    private static final class BackApi33 {
        static Object register(Activity activity, Runnable completed) {
            OnBackInvokedCallback callback = completed::run;
            activity.getOnBackInvokedDispatcher().registerOnBackInvokedCallback(OnBackInvokedDispatcher.PRIORITY_DEFAULT, callback);
            return callback;
        }
        static void unregister(Activity activity, Object registration) {
            activity.getOnBackInvokedDispatcher().unregisterOnBackInvokedCallback((OnBackInvokedCallback) registration);
        }
    }

    public final class NativeBridge {
        private final SharedPreferences saves;
        NativeBridge(SharedPreferences saves) { this.saves = saves; }

        @JavascriptInterface public void ready() {
            main.post(() -> {
                if (destroyed || closing || ready) return;
                ready = true;
                main.removeCallbacks(startupTimeout);
                applyHostState();
            });
        }
        @JavascriptInterface public void reportError(String error) { fail("割绳子游戏错误：" + error); }
        @JavascriptInterface public String readSave(String key) {
            try {
                validateKey(key);
                return saves.getString(key, null);
            } catch (RuntimeException error) {
                fail("割绳子读取进度失败：" + error);
                return null;
            }
        }
        @JavascriptInterface public boolean writeSave(String key, String value) {
            try {
                validateKey(key);
                if (value == null || value.length() > 4 * 1024 * 1024) throw new IllegalArgumentException("Invalid save value size");
                boolean durable = saves.edit().putString(key, value).commit();
                if (!durable) fail("割绳子保存进度失败：SharedPreferences commit 返回 false");
                return durable;
            } catch (RuntimeException error) {
                fail("割绳子保存进度失败：" + error);
                return false;
            }
        }
        @JavascriptInterface public boolean removeSave(String key) {
            try {
                validateKey(key);
                boolean durable = saves.edit().remove(key).commit();
                if (!durable) fail("割绳子清除进度失败：SharedPreferences commit 返回 false");
                return durable;
            } catch (RuntimeException error) {
                fail("割绳子清除进度失败：" + error);
                return false;
            }
        }
        private void validateKey(String key) {
            if (destroyed) throw new IllegalStateException("Game host already destroyed");
            if (key == null || key.isEmpty() || key.length() > 200) throw new IllegalArgumentException("Invalid save key");
        }
    }

    private final class OfflineClient extends WebViewClient {
        @Override public WebResourceResponse shouldInterceptRequest(WebView view, WebResourceRequest request) {
            String path = OfflineAssetPolicy.assetPath(request.getUrl().toString());
            if (!"GET".equals(request.getMethod()) || path == null) return denied(403, "Forbidden");
            try {
                InputStream source = getAssets().open(path);
                Map<String, String> headers = new HashMap<>();
                headers.put("Cache-Control", "no-store");
                headers.put("X-Content-Type-Options", "nosniff");
                headers.put("Content-Security-Policy", "default-src 'none'; script-src 'self' 'unsafe-inline' 'unsafe-eval'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:; media-src 'self' data: blob:; font-src 'self' data:; connect-src 'self' blob: data:; worker-src 'none'; frame-src 'none'; object-src 'none'; base-uri 'none'; form-action 'none'");
                String mime = OfflineAssetPolicy.mimeType(path);
                String encoding = mime.startsWith("text/") || mime.equals("application/javascript") || mime.equals("application/json") ? "UTF-8" : null;
                return new WebResourceResponse(mime, encoding, 200, "OK", headers, source);
            } catch (IOException error) {
                fail("割绳子离线资源缺失：" + path + "（" + error.getMessage() + "）");
                return denied(404, "Not Found");
            }
        }
        @Override public boolean shouldOverrideUrlLoading(WebView view, WebResourceRequest request) { return true; }
        @Override public boolean shouldOverrideUrlLoading(WebView view, String url) { return true; }
        @Override public void onPageStarted(WebView view, String url, android.graphics.Bitmap icon) {
            if (!OfflineAssetPolicy.isEntryUrl(url)) fail("割绳子拒绝页面导航：" + url);
        }
        @Override public void onReceivedError(WebView view, WebResourceRequest request, WebResourceError error) {
            if (request.isForMainFrame() || OfflineAssetPolicy.assetPath(request.getUrl().toString()) != null) {
                fail("割绳子资源加载失败：" + request.getUrl() + "（" + error.getErrorCode() + ": " + error.getDescription() + "）");
            }
        }
        @Override public void onReceivedHttpError(WebView view, WebResourceRequest request, WebResourceResponse response) {
            if (request.isForMainFrame() || OfflineAssetPolicy.assetPath(request.getUrl().toString()) != null) {
                fail("割绳子资源响应失败：" + request.getUrl() + "（HTTP " + response.getStatusCode() + "）");
            }
        }
        @Override public void onReceivedSslError(WebView view, SslErrorHandler handler, SslError error) {
            handler.cancel();
            fail("割绳子拒绝非离线 TLS 请求：" + error);
        }
        @Override public boolean onRenderProcessGone(WebView view, RenderProcessGoneDetail detail) {
            fail("割绳子 WebView 渲染进程" + (detail.didCrash() ? "崩溃" : "被系统终止"));
            return true;
        }
    }

    private static WebResourceResponse denied(int code, String reason) {
        return new WebResourceResponse("text/plain", "UTF-8", code, reason, null,
                new ByteArrayInputStream(reason.getBytes(StandardCharsets.UTF_8)));
    }
}
