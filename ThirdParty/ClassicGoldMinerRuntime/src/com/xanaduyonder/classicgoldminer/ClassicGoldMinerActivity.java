package com.xanaduyonder.classicgoldminer;

import android.annotation.SuppressLint;
import android.app.Activity;
import android.app.AlertDialog;
import android.graphics.Color;
import android.graphics.Insets;
import android.net.http.SslError;
import android.os.Build;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.os.SystemClock;
import android.view.DisplayCutout;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.view.WindowInsets;
import android.view.WindowInsetsController;
import android.view.WindowManager;
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
import android.widget.TextView;
import android.widget.Toast;
import android.window.OnBackInvokedCallback;
import android.window.OnBackInvokedDispatcher;
import java.io.ByteArrayInputStream;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.util.HashMap;
import java.util.Map;

/** Dedicated system-WebView shell. All callbacks/state below are confined to the main thread. */
public final class ClassicGoldMinerActivity extends Activity {
    private static final int BACKGROUND_TIMEOUT_MS = 1500;
    private static final int EXIT_TIMEOUT_MS = 5000;
    private final Handler main = new Handler(Looper.getMainLooper());
    private final BackPressGate backGate = new BackPressGate();
    private FrameLayout root;
    private WebView web;
    private TextView loading;
    private ClassicSaveStore saves;
    private Runnable unregisterModernBack;
    private AlertDialog dialog;
    private boolean foreground;
    private boolean pageReady;
    private boolean resumeOverlay;
    private boolean exiting;
    private boolean exitCommitted;
    private boolean destroyed;
    private int lifecycleEpoch;
    private int pageEpoch;
    private int exitAttempt;
    private String documentUrl = LocalAssetPolicy.ENTRY;
    private int safeLeft, safeTop, safeRight, safeBottom;

    @Override public void onCreate(Bundle state) {
        super.onCreate(state);
        if (!ClassicGoldMinerBridge.started(this, getIntent().getLongExtra("launch", 0))) {
            finish();
            return;
        }
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
        root = new FrameLayout(this);
        root.setBackgroundColor(Color.rgb(25, 19, 14));
        setContentView(root);
        root.setOnApplyWindowInsetsListener((view, insets) -> {
            int min = Math.round(8 * getResources().getDisplayMetrics().density);
            int left = min, top = min, right = min, bottom = min;
            if (Build.VERSION.SDK_INT >= 28) {
                DisplayCutout cutout = insets.getDisplayCutout();
                if (cutout != null) {
                    left = Math.max(left, cutout.getSafeInsetLeft());
                    top = Math.max(top, cutout.getSafeInsetTop());
                    right = Math.max(right, cutout.getSafeInsetRight());
                    bottom = Math.max(bottom, cutout.getSafeInsetBottom());
                }
            }
            if (Build.VERSION.SDK_INT >= 29) {
                Insets gestures = insets.getSystemGestureInsets();
                left = Math.max(left, gestures.left);
                top = Math.max(top, gestures.top);
                right = Math.max(right, gestures.right);
                bottom = Math.max(bottom, gestures.bottom);
            } else {
                left = Math.max(left, insets.getSystemWindowInsetLeft());
                right = Math.max(right, insets.getSystemWindowInsetRight());
                bottom = Math.max(bottom, insets.getSystemWindowInsetBottom());
            }
            // The mine paints the entire screen. Only HUD and touch overlays
            // avoid cutouts/gesture edges; padding the WebView shrinks the game.
            boolean changed = safeLeft != left || safeTop != top || safeRight != right || safeBottom != bottom;
            safeLeft = left;
            safeTop = top;
            safeRight = right;
            safeBottom = bottom;
            if (changed) publishSafeInsets();
            return insets;
        });
        saves = new ClassicSaveStore(this, getIntent().getBooleanExtra("sound", true));
        if (Build.VERSION.SDK_INT >= 33) {
            // Only invoked after a predictive Back gesture is committed, never on cancellation.
            unregisterModernBack = Api33Back.register(this, this::committedBack);
        }
        if (state != null) {
            documentUrl = recoveryUrl(state.getString("document", LocalAssetPolicy.ENTRY));
            resumeOverlay = true;
        }
        immersive();
        createWebView(documentUrl);
    }

    private void publishSafeInsets() {
        if (web == null || destroyed) return;
        web.evaluateJavascript("(function(){var s=document.documentElement.style,d=window.devicePixelRatio||1;"
                + "s.setProperty('--classic-safe-left',(" + safeLeft + "/d)+'px');"
                + "s.setProperty('--classic-safe-top',(" + safeTop + "/d)+'px');"
                + "s.setProperty('--classic-safe-right',(" + safeRight + "/d)+'px');"
                + "s.setProperty('--classic-safe-bottom',(" + safeBottom + "/d)+'px');"
                + "window.dispatchEvent(new Event('classic-insets'));})()", null);
    }

    @SuppressLint("SetJavaScriptEnabled")
    private void createWebView(String url) {
        if (destroyed || isFinishing()) return;
        pageReady = false;
        try {
            WebView candidate = new WebView(this);
            web = candidate;
            candidate.setBackgroundColor(Color.rgb(25, 19, 14));
            WebSettings settings = candidate.getSettings();
            settings.setJavaScriptEnabled(true);
            settings.setDomStorageEnabled(true);
            settings.setAllowFileAccess(false);
            settings.setAllowContentAccess(false);
            settings.setAllowFileAccessFromFileURLs(false);
            settings.setAllowUniversalAccessFromFileURLs(false);
            settings.setBlockNetworkLoads(true);
            settings.setSafeBrowsingEnabled(false); // Only APK assets exist; no remote reputation-service requests.
            settings.setMixedContentMode(WebSettings.MIXED_CONTENT_NEVER_ALLOW);
            settings.setJavaScriptCanOpenWindowsAutomatically(false);
            settings.setSupportMultipleWindows(true);
            settings.setGeolocationEnabled(false);
            settings.setMediaPlaybackRequiresUserGesture(true);
            settings.setSaveFormData(false);
            candidate.addJavascriptInterface(saves, "ClassicGoldMinerNative");
            candidate.setWebViewClient(new LocalClient());
            candidate.setWebChromeClient(new WebChromeClient() {
                @Override public boolean onCreateWindow(WebView view, boolean dialog, boolean gesture, android.os.Message result) { return false; }
                @Override public void onPermissionRequest(PermissionRequest request) { request.deny(); }
            });
            candidate.setDownloadListener((a, b, c, d, e) -> Toast.makeText(this, "此游戏仅使用内置资源。", Toast.LENGTH_SHORT).show());
            root.addView(candidate, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));
            loading = new TextView(this);
            loading.setText("正在打开经典黄金矿工…");
            loading.setTextColor(Color.WHITE);
            loading.setGravity(Gravity.CENTER);
            loading.setBackgroundColor(Color.rgb(25, 19, 14));
            root.addView(loading, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));
            documentUrl = LocalAssetPolicy.isDocument(url) ? url : LocalAssetPolicy.ENTRY;
            candidate.loadUrl(documentUrl);
        } catch (RuntimeException unavailable) {
            destroyWebView();
            recoverOrReturn("系统 WebView 无法启动。请启用或更新系统 WebView 后重试。", false);
        }
    }

    private final class LocalClient extends WebViewClient {
        @Override public WebResourceResponse shouldInterceptRequest(WebView view, WebResourceRequest request) {
            String path = "GET".equals(request.getMethod()) ? LocalAssetPolicy.assetPath(request.getUrl().toString()) : null;
            if (path == null) return denied(403, "Forbidden");
            try {
                Map<String, String> headers = new HashMap<>();
                headers.put("Content-Security-Policy", LocalAssetPolicy.CSP);
                headers.put("X-Content-Type-Options", "nosniff");
                headers.put("Referrer-Policy", "no-referrer");
                headers.put("Cache-Control", "no-store");
                headers.put("Cross-Origin-Resource-Policy", "same-origin");
                String mime = LocalAssetPolicy.mimeType(path);
                String encoding = mime.startsWith("text/") || mime.equals("application/javascript")
                        || mime.equals("application/json") || mime.equals("application/xml") ? "UTF-8" : null;
                return new WebResourceResponse(mime, encoding, 200, "OK", headers, getAssets().open(path));
            } catch (IOException missing) {
                return denied(404, "Not Found");
            }
        }

        @Override public boolean shouldOverrideUrlLoading(WebView view, WebResourceRequest request) {
            return !request.isForMainFrame() || !"GET".equals(request.getMethod()) || !LocalAssetPolicy.isDocument(request.getUrl().toString());
        }

        @Override public boolean shouldOverrideUrlLoading(WebView view, String url) { return !LocalAssetPolicy.isDocument(url); }

        @Override public void onPageStarted(WebView view, String url, android.graphics.Bitmap icon) {
            if (view != web) return;
            pageReady = false;
            if (!LocalAssetPolicy.isDocument(url)) {
                view.stopLoading();
                recoverOrReturn("已拒绝非内置游戏页面。", false);
                return;
            }
            documentUrl = url;
            int load = ++pageEpoch;
            main.postDelayed(() -> {
                if (!destroyed && view == web && !pageReady && load == pageEpoch) {
                    view.stopLoading();
                    recoverOrReturn("游戏页面未完成加载。可以重新加载，或返回大厅。", false);
                }
            }, 20000);
        }

        @Override public void onPageFinished(WebView view, String url) {
            if (view != web || !LocalAssetPolicy.isDocument(url) || !url.equals(documentUrl)) return;
            publishSafeInsets();
            int finishedPage = pageEpoch;
            view.evaluateJavascript("typeof ClassicHost !== 'undefined' && typeof ClassicHost.pauseAndSave === 'function'", result -> {
                if (view != web || destroyed || finishedPage != pageEpoch) return;
                if (!"true".equals(result)) {
                    recoverOrReturn("游戏接口未能加载，无法安全保存。", false);
                    return;
                }
                pageReady = true;
                ClassicGoldMinerBridge.reportError("");
                if (loading != null) { root.removeView(loading); loading = null; }
                if (!foreground) pauseForBackground();
                else if (resumeOverlay) showForegroundOverlay();
            });
        }

        @Override public void onReceivedError(WebView view, WebResourceRequest request, WebResourceError error) {
            if (view == web && request.isForMainFrame()) recoverOrReturn("游戏页面加载失败，请重试。", false);
        }

        @Override public void onReceivedSslError(WebView view, SslErrorHandler handler, SslError error) { handler.cancel(); }

        @Override public boolean onRenderProcessGone(WebView view, RenderProcessGoneDetail detail) {
            if (view == web) {
                exitAttempt++;
                exiting = false;
                backGate.reset();
                destroyWebView(true);
                recoverOrReturn("系统 WebView 已中断。最近一次成功保存的进度仍在，之后的操作可能丢失。", true);
            } else {
                view.destroy();
            }
            return true;
        }
    }

    private static WebResourceResponse denied(int status, String reason) {
        Map<String, String> headers = new HashMap<>();
        headers.put("Content-Security-Policy", "default-src 'none'");
        headers.put("Cache-Control", "no-store");
        return new WebResourceResponse("text/plain", "UTF-8", status, reason, headers,
                new ByteArrayInputStream("Resource unavailable".getBytes(StandardCharsets.UTF_8)));
    }

    @Override public void onBackPressed() { committedBack(); }

    private void committedBack() {
        if (!foreground || destroyed || exitCommitted || exiting) return;
        if (backGate.commit(SystemClock.elapsedRealtime())) requestExit();
        else Toast.makeText(this, "2 秒内再返回一次，保存并回到大厅", Toast.LENGTH_SHORT).show();
    }

    private void requestExit() {
        if (exiting || destroyed || !foreground) return;
        backGate.reset();
        if (web == null || !pageReady) {
            recoverOrReturn("游戏尚未就绪，无法确认当前进度已保存。", true);
            return;
        }
        exiting = true;
        int attempt = ++exitAttempt;
        int epoch = lifecycleEpoch;
        WebView target = web;
        target.setEnabled(false);
        target.evaluateJavascript("typeof ClassicHost !== 'undefined' && ClassicHost.pauseAndSave('exit') === true", result -> {
            if (attempt != exitAttempt || epoch != lifecycleEpoch || target != web || destroyed || !foreground) return;
            exiting = false;
            target.setEnabled(true);
            if ("true".equals(result)) {
                exitCommitted = true;
                ClassicGoldMinerBridge.reportError("");
                finish();
            } else {
                showSaveFailure();
            }
        });
        main.postDelayed(() -> {
            if (attempt == exitAttempt && exiting && !destroyed && target == web) {
                exitAttempt++;
                exiting = false;
                target.setEnabled(true);
                showSaveFailure();
            }
        }, EXIT_TIMEOUT_MS);
    }

    private void showSaveFailure() {
        if (destroyed || isFinishing() || !foreground) return;
        String error = saves.getLastSaveError();
        AlertDialog next = new AlertDialog.Builder(this).setTitle("尚未保存，未退出")
                .setMessage(error.isEmpty() ? "未收到保存成功确认。可以重试，或取消退出返回游戏。" : error)
                .setPositiveButton("重试保存并退出", (d, w) -> requestExit())
                .setNegativeButton("取消退出", (d, w) -> showForegroundOverlay())
                .setOnCancelListener(d -> showForegroundOverlay()).create();
        presentDialog(next);
    }

    private void recoverOrReturn(String message, boolean latestSaveOnly) {
        if (destroyed || isFinishing()) return;
        ClassicGoldMinerBridge.reportError(message);
        // Preserve an explicit recovery choice until foreground; never finish silently on a crash.
        AlertDialog next = new AlertDialog.Builder(this).setTitle("经典黄金矿工")
                .setMessage(message + (latestSaveOnly ? "\n返回大厅不会覆盖最近的存档。" : ""))
                .setPositiveButton("重新加载最近存档", (d, w) -> {
                    String target = recoveryUrl(documentUrl);
                    destroyWebView();
                    resumeOverlay = true;
                    createWebView(target);
                })
                .setNegativeButton("返回大厅", (d, w) -> { exitCommitted = true; finish(); })
                .setCancelable(false).create();
        presentDialog(next);
    }

    private static String recoveryUrl(String previous) {
        if (previous != null && previous.startsWith(LocalAssetPolicy.ORIGIN + "solo/index.html")) return LocalAssetPolicy.ORIGIN + "solo/index.html?resume=1";
        if (previous != null && previous.startsWith(LocalAssetPolicy.ORIGIN + "duo/index.html")) return LocalAssetPolicy.ORIGIN + "duo/index.html?resume=1";
        return LocalAssetPolicy.ENTRY;
    }

    @Override protected void onResume() {
        super.onResume();
        foreground = true;
        lifecycleEpoch++;
        immersive();
        if (web != null) {
            web.onResume();
            if (resumeOverlay && pageReady) showForegroundOverlay();
        }
        if (dialog != null && !dialog.isShowing()) dialog.show();
    }

    @Override protected void onPause() {
        foreground = false;
        lifecycleEpoch++;
        backGate.reset();
        exitAttempt++;
        exiting = false;
        if (web != null) web.setEnabled(true);
        if (!exitCommitted) {
            resumeOverlay = true;
            pauseForBackground();
        } else if (web != null) web.onPause();
        super.onPause();
    }

    private void pauseForBackground() {
        WebView target = web;
        if (target == null || destroyed) return;
        int epoch = lifecycleEpoch;
        // The JS call first cancels input/audio/timers and synchronously commits its snapshot.
        // Only then suspend this WebView; pauseTimers() is global and must never be used here.
        target.evaluateJavascript("typeof ClassicHost !== 'undefined' && ClassicHost.pauseAndSave('background') === true", result -> {
            if (target != web || destroyed || foreground || epoch != lifecycleEpoch) return;
            if (!"true".equals(result)) ClassicGoldMinerBridge.reportError("后台保存未成功，返回后请重试保存。");
            else ClassicGoldMinerBridge.reportError("");
            target.onPause();
        });
        main.postDelayed(() -> {
            if (target == web && !destroyed && !foreground && epoch == lifecycleEpoch) target.onPause();
        }, BACKGROUND_TIMEOUT_MS);
    }

    private void showForegroundOverlay() {
        resumeOverlay = true;
        WebView target = web;
        if (target == null || !pageReady || !foreground || destroyed) return;
        // A delayed/missing background callback must not let animation resume before the overlay.
        target.evaluateJavascript("if (typeof ClassicHost !== 'undefined') { ClassicHost.pauseAndSave('background'); ClassicHost.foreground(); }", result -> { });
        resumeOverlay = false;
    }

    @Override protected void onSaveInstanceState(Bundle out) {
        out.putString("document", documentUrl);
        super.onSaveInstanceState(out);
    }

    @Override public void onWindowFocusChanged(boolean hasFocus) {
        super.onWindowFocusChanged(hasFocus);
        if (hasFocus) immersive();
    }

    private void immersive() {
        if (root == null) return;
        if (Build.VERSION.SDK_INT >= 28) {
            WindowManager.LayoutParams attributes = getWindow().getAttributes();
            attributes.layoutInDisplayCutoutMode = WindowManager.LayoutParams.LAYOUT_IN_DISPLAY_CUTOUT_MODE_SHORT_EDGES;
            getWindow().setAttributes(attributes);
        }
        if (Build.VERSION.SDK_INT >= 30) {
            getWindow().setDecorFitsSystemWindows(false);
            WindowInsetsController controller = getWindow().getInsetsController();
            if (controller != null) {
                controller.setSystemBarsBehavior(WindowInsetsController.BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE);
                controller.hide(WindowInsets.Type.systemBars());
            }
        } else {
            getWindow().getDecorView().setSystemUiVisibility(View.SYSTEM_UI_FLAG_FULLSCREEN
                    | View.SYSTEM_UI_FLAG_HIDE_NAVIGATION | View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY
                    | View.SYSTEM_UI_FLAG_LAYOUT_STABLE | View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN
                    | View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION);
        }
        root.requestApplyInsets();
    }

    private void presentDialog(AlertDialog next) {
        dismissDialog();
        dialog = next;
        next.setOnDismissListener(ignored -> {
            // Button/cancel dismissal is asynchronous. An action may already have created a
            // replacement (for example, another renderer failure while reloading).
            if (dialog == next) dialog = null;
        });
        // Keep a background-created recovery dialog pending until onResume shows it.
        if (foreground) next.show();
    }

    private void dismissDialog() {
        AlertDialog previous = dialog;
        dialog = null;
        if (previous != null) previous.dismiss();
    }

    private void destroyWebView() { destroyWebView(false); }

    private void destroyWebView(boolean rendererGone) {
        pageReady = false;
        pageEpoch++;
        WebView old = web;
        web = null;
        if (old != null) {
            root.removeView(old);
            if (!rendererGone) {
                old.stopLoading();
                old.removeJavascriptInterface("ClassicGoldMinerNative");
                old.setWebChromeClient(null);
            }
            old.destroy();
        }
        if (loading != null) { root.removeView(loading); loading = null; }
    }

    // Loaded only on API 33+, so API 26 never verifies fields/signatures using absent classes.
    private static final class Api33Back {
        static Runnable register(Activity activity, Runnable committed) {
            OnBackInvokedCallback callback = committed::run;
            activity.getOnBackInvokedDispatcher().registerOnBackInvokedCallback(
                    OnBackInvokedDispatcher.PRIORITY_DEFAULT, callback);
            return () -> activity.getOnBackInvokedDispatcher().unregisterOnBackInvokedCallback(callback);
        }
    }

    @Override protected void onDestroy() {
        destroyed = true;
        lifecycleEpoch++;
        exitAttempt++;
        main.removeCallbacksAndMessages(null);
        dismissDialog();
        if (unregisterModernBack != null) unregisterModernBack.run();
        destroyWebView();
        ClassicGoldMinerBridge.ended(this);
        super.onDestroy();
    }
}
