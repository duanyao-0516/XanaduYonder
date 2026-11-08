#import <UIKit/UIKit.h>
#import <WebKit/WebKit.h>
#import <AVFoundation/AVFoundation.h>
#import <fcntl.h>
#import <unistd.h>
#import "XanaduWebAssetServer.h"

extern "C" UIViewController *UnityGetGLViewController(void);
extern "C" void UnityPause(int pause);
extern "C" int UnityIsPaused(void);
extern "C" int UnityShouldAutorotate(void);
extern "C" int UnityRequestedScreenOrientation(void);
extern "C" int XanaduAudio_PrepareSession(void);

@class XanaduWebGameController;
static XanaduWebGameController *CurrentGame;
static BOOL Running;
static NSString *LastError;

static NSString *JSONString(id value)
{
    NSData *data = [NSJSONSerialization dataWithJSONObject:value options:NSJSONWritingFragmentsAllowed error:nil];
    return data ? [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding] : @"null";
}

// Cut the Rope's existing synchronous API must acknowledge the completed write,
// not an enqueued WKScriptMessage. Only that game's exact serialized strings live here.
static NSDictionary *CutSave(NSDictionary *request)
{
    NSString *operation = request[@"operation"], *key = request[@"key"], *value = request[@"value"];
    if (![operation isKindOfClass:NSString.class] || ![key isKindOfClass:NSString.class] || !key.length || key.length > 128)
        return @{ @"ok":@NO, @"error":@"Invalid save operation or key" };
    NSURL *support = [NSFileManager.defaultManager URLsForDirectory:NSApplicationSupportDirectory inDomains:NSUserDomainMask].firstObject;
    NSString *directory = [[support URLByAppendingPathComponent:@"XanaduWebGames/cut-the-rope" isDirectory:YES] path];
    NSError *error = nil;
    if (![NSFileManager.defaultManager createDirectoryAtPath:directory withIntermediateDirectories:YES attributes:nil error:&error])
        return @{ @"ok":@NO, @"error":error.localizedDescription ?: @"Cannot open save directory" };
    NSString *name = [[[key dataUsingEncoding:NSUTF8StringEncoding] base64EncodedStringWithOptions:0] stringByReplacingOccurrencesOfString:@"/" withString:@"_"];
    NSString *path = [directory stringByAppendingPathComponent:name];
    if ([operation isEqual:@"read"]) {
        if (![NSFileManager.defaultManager fileExistsAtPath:path]) return @{ @"ok":@YES, @"value":NSNull.null };
        NSString *saved = [NSString stringWithContentsOfFile:path encoding:NSUTF8StringEncoding error:&error];
        return saved ? @{ @"ok":@YES, @"value":saved } : @{ @"ok":@NO, @"error":error.localizedDescription ?: @"Cannot read save" };
    }
    BOOL success = NO;
    if ([operation isEqual:@"write"] && [value isKindOfClass:NSString.class] && value.length <= 1048576) {
        NSData *bytes = [value dataUsingEncoding:NSUTF8StringEncoding];
        NSString *temporary = [directory stringByAppendingPathComponent:[@".pending-" stringByAppendingString:NSUUID.UUID.UUIDString]];
        int file = open(temporary.fileSystemRepresentation, O_WRONLY | O_CREAT | O_EXCL, 0600);
        if (file >= 0) {
            NSUInteger written = 0;
            while (written < bytes.length) {
                ssize_t count = write(file, ((const char *)bytes.bytes) + written, bytes.length - written);
                if (count < 0 && errno == EINTR) continue;
                if (count <= 0) break;
                written += (NSUInteger)count;
            }
            success = written == bytes.length && fsync(file) == 0;
            if (close(file) != 0) success = NO;
            if (success) success = rename(temporary.fileSystemRepresentation, path.fileSystemRepresentation) == 0;
            if (!success) unlink(temporary.fileSystemRepresentation);
        }
    } else if ([operation isEqual:@"remove"]) {
        success = unlink(path.fileSystemRepresentation) == 0 || errno == ENOENT;
    }
    if (success) {
        int directoryFD = open(directory.fileSystemRepresentation, O_RDONLY);
        success = directoryFD >= 0 && fsync(directoryFD) == 0;
        if (directoryFD >= 0) close(directoryFD);
    }
    return success ? @{ @"ok":@YES } : @{ @"ok":@NO, @"error":@"Save did not complete; existing data was retained where possible" };
}

@interface XanaduWebGameController : UIViewController <WKNavigationDelegate, WKUIDelegate, WKScriptMessageHandler>
@property(nonatomic, copy) NSString *game;
@property(nonatomic, strong) XanaduWebAssetServer *server;
@property(nonatomic, strong) WKWebView *web;
@property(nonatomic, strong) WKNavigation *navigation;
@property(nonatomic, copy) NSString *bridgeSource;
@property(nonatomic, strong) UILabel *loading;
@property(nonatomic, strong) UIButton *closeButton;
@property(nonatomic, strong) NSTimer *timer;
@property(nonatomic, strong) UIAlertController *failureAlert;
@property(nonatomic, copy) NSString *pendingFailure;
@property(nonatomic, copy) NSString *exitRequest;
@property(nonatomic) BOOL sound;
@property(nonatomic) BOOL active;
@property(nonatomic) BOOL interrupted;
@property(nonatomic) BOOL configured;
@property(nonatomic) BOOL ready;
@property(nonatomic) BOOL finished;
@property(nonatomic) BOOL fault;
@property(nonatomic) BOOL ownsUnityPause;
@property(nonatomic) BOOL previousIdleTimerDisabled;
@property(nonatomic) BOOL polling;
@property(nonatomic) BOOL mediaPlaybackSuspended;
@property(nonatomic) CGRect viewportBounds;
@property(nonatomic) UIEdgeInsets viewportInsets;
@property(nonatomic) NSInteger generation;
@property(nonatomic) NSInteger lifecycleRevision;
@property(nonatomic) double lastTick;
@property(nonatomic) double loadingElapsed;
@property(nonatomic) double exitElapsed;
@property(nonatomic) double statusElapsed;
- (void)open;
- (void)finish:(NSString *)error;
@end

@implementation XanaduWebGameController
- (BOOL)isZhaoyun { return [self.game isEqual:@"zhaoyun-adou"]; }
- (BOOL)isCut { return [self.game isEqual:@"cut-the-rope"]; }
- (UIInterfaceOrientationMask)supportedInterfaceOrientations
{
    if (self.isZhaoyun) return UIInterfaceOrientationMaskPortrait;
    // Unity ScreenOrientation.LandscapeLeft maps to UIKit LandscapeRight.
    return self.isCut ? UIInterfaceOrientationMaskAllButUpsideDown : UIInterfaceOrientationMaskLandscapeRight;
}
- (UIInterfaceOrientation)preferredInterfaceOrientationForPresentation
{
    return self.isZhaoyun || self.isCut ? UIInterfaceOrientationPortrait : UIInterfaceOrientationLandscapeRight;
}
- (BOOL)shouldAutorotate { return YES; }
- (BOOL)prefersStatusBarHidden { return YES; }
- (BOOL)prefersHomeIndicatorAutoHidden { return YES; }
- (void)open
{
    self.modalPresentationStyle = UIModalPresentationFullScreen;
    self.modalInPresentation = YES;
    self.active = UIApplication.sharedApplication.applicationState == UIApplicationStateActive;
    self.previousIdleTimerDisabled = UIApplication.sharedApplication.idleTimerDisabled;
    UIApplication.sharedApplication.idleTimerDisabled = YES;
    NSNotificationCenter *center = NSNotificationCenter.defaultCenter;
    [center addObserver:self selector:@selector(resigned:) name:UIApplicationWillResignActiveNotification object:nil];
    [center addObserver:self selector:@selector(activated:) name:UIApplicationDidBecomeActiveNotification object:nil];
    [center addObserver:self selector:@selector(audioInterrupted:) name:AVAudioSessionInterruptionNotification object:nil];
    [self presentWhenOrientationReady:0];
}
- (void)presentWhenOrientationReady:(NSInteger)attempt
{
    if (self.finished) return;
    UIViewController *presenter = UnityGetGLViewController();
    if (!presenter || presenter.presentedViewController) { [self finish:@"另一个系统页面尚未关闭，请稍后重试。"]; return; }
    // Screen.orientation is applied on a later Unity frame. UIKit rejects a
    // fullscreen controller if its orientation and the app's current mask clash.
    UIInterfaceOrientationMask required = (UIInterfaceOrientationMask)(1 << self.preferredInterfaceOrientationForPresentation);
    UIApplication *application = UIApplication.sharedApplication;
    UIInterfaceOrientationMask rootMask = presenter.supportedInterfaceOrientations;
    // Calling UIApplication directly reads the plist defaults, bypassing Unity's
    // delegate implementation that owns its dynamically requested orientation.
    id<UIApplicationDelegate> delegate = application.delegate;
    UIInterfaceOrientationMask appMask = [delegate respondsToSelector:@selector(application:supportedInterfaceOrientationsForWindow:)]
        ? [delegate application:application supportedInterfaceOrientationsForWindow:presenter.view.window]
        : [application supportedInterfaceOrientationsForWindow:presenter.view.window];
    UIInterfaceOrientationMask allowed = appMask & rootMask;
    if (attempt == 0 || attempt % 60 == 0)
        NSLog(@"[XanaduWebGames] %s orientation attempt=%ld required=%lu app=%lu root=%lu scene=%ld active=%d transition=%d",
            self.game.UTF8String, (long)attempt, (unsigned long)required, (unsigned long)appMask,
            (unsigned long)rootMask, (long)presenter.view.window.windowScene.interfaceOrientation,
            self.active, presenter.transitionCoordinator != nil);
    if (!self.active || !(allowed & required) || presenter.transitionCoordinator) {
        if (attempt >= 300) { [self finish:@"屏幕方向尚未切换完成，请重新进入游戏。"]; return; }
        dispatch_after(dispatch_time(DISPATCH_TIME_NOW, NSEC_PER_SEC / 60), dispatch_get_main_queue(), ^{ [self presentWhenOrientationReady:attempt + 1]; });
        return;
    }
    NSLog(@"[XanaduWebGames] %s presenting required=%lu allowed=%lu", self.game.UTF8String, (unsigned long)required, (unsigned long)allowed);
    [presenter presentViewController:self animated:NO completion:nil];
}
- (void)viewDidLoad
{
    [super viewDidLoad];
    self.view.backgroundColor = UIColor.blackColor;
    self.closeButton = [UIButton buttonWithType:UIButtonTypeSystem]; self.closeButton.translatesAutoresizingMaskIntoConstraints = NO;
    [self.closeButton setTitle:@"‹ 返回大厅" forState:UIControlStateNormal];
    [self.closeButton setTitleColor:UIColor.whiteColor forState:UIControlStateNormal];
    self.closeButton.titleLabel.font = [UIFont systemFontOfSize:16 weight:UIFontWeightSemibold];
    self.closeButton.accessibilityLabel = @"保存并返回游戏大厅";
    [self.closeButton addTarget:self action:@selector(requestClose) forControlEvents:UIControlEventTouchUpInside];
    // Every game fills the display. Level Devil provides its own title-screen exit.
    WKWebViewConfiguration *configuration = [[WKWebViewConfiguration alloc] init];
    configuration.websiteDataStore = WKWebsiteDataStore.defaultDataStore;
    configuration.allowsInlineMediaPlayback = YES;
    configuration.mediaTypesRequiringUserActionForPlayback = WKAudiovisualMediaTypeNone;
    configuration.allowsAirPlayForMediaPlayback = NO;
    configuration.preferences.javaScriptCanOpenWindowsAutomatically = NO;
    [configuration.userContentController addScriptMessageHandler:self name:@"xanadu"];
    NSString *bridge = self.isCut ?
      @"(function(){function send(type,message){webkit.messageHandlers.xanadu.postMessage({type:type,message:message,generation:__XANADU_GENERATION__});}"
       "function save(operation,key,value){var r=JSON.parse(window.prompt('__xanadu_cut_save__',JSON.stringify({operation:operation,key:key,value:value,generation:__XANADU_GENERATION__})));"
       "if(!r||!r.ok)throw Error(r&&r.error||'Native save failed');return r;}"
       "window.CutTheRopeNative=Object.freeze({ready:function(){send('ready');},reportError:function(e){send('error',String(e));},"
       "readSave:function(k){return save('read',k).value;},writeSave:function(k,v){return save('write',k,v).ok;},removeSave:function(k){return save('remove',k).ok;}});})();" :
      @"window.LevelDevilNative=Object.freeze({postMessage:function(value){webkit.messageHandlers.xanadu.postMessage(Object.assign(JSON.parse(value),{generation:__XANADU_GENERATION__}));}});";
    // WKWebView owns a separate web audio session; match the Unity playback policy.
    self.bridgeSource = @"(function(){if(navigator.audioSession){try{navigator.audioSession.type='playback';}catch(e){console.warn('Web audio session:',e.message);}}})();";
    if (!self.isZhaoyun) self.bridgeSource = [self.bridgeSource stringByAppendingString:bridge];
    self.web = [[WKWebView alloc] initWithFrame:CGRectZero configuration:configuration];
    self.web.translatesAutoresizingMaskIntoConstraints = NO;
    self.web.navigationDelegate = self; self.web.UIDelegate = self;
    self.web.opaque = YES; self.web.backgroundColor = UIColor.blackColor;
    self.web.scrollView.backgroundColor = UIColor.blackColor;
    self.web.scrollView.scrollEnabled = NO; self.web.scrollView.bounces = NO;
    self.web.scrollView.contentInsetAdjustmentBehavior = UIScrollViewContentInsetAdjustmentNever;
    self.web.allowsBackForwardNavigationGestures = NO;
    [self.view addSubview:self.web];
    [NSLayoutConstraint activateConstraints:@[
        [self.web.leadingAnchor constraintEqualToAnchor:self.view.leadingAnchor],
        [self.web.trailingAnchor constraintEqualToAnchor:self.view.trailingAnchor],
        [self.web.topAnchor constraintEqualToAnchor:self.view.topAnchor],
        [self.web.bottomAnchor constraintEqualToAnchor:self.view.bottomAnchor]
    ]];
    if (self.isZhaoyun || self.isCut) {
        self.closeButton.hidden = YES;
        self.closeButton.backgroundColor = [UIColor colorWithWhite:0 alpha:.62];
        self.closeButton.layer.cornerRadius = 12;
        [self.view addSubview:self.closeButton];
        [NSLayoutConstraint activateConstraints:@[
            [self.closeButton.trailingAnchor constraintEqualToAnchor:self.view.safeAreaLayoutGuide.trailingAnchor constant:-12],
            [self.closeButton.topAnchor constraintEqualToAnchor:self.view.safeAreaLayoutGuide.topAnchor constant:8],
            [self.closeButton.widthAnchor constraintEqualToConstant:112],
            [self.closeButton.heightAnchor constraintEqualToConstant:44]
        ]];
    }
    self.loading = [[UILabel alloc] init]; self.loading.translatesAutoresizingMaskIntoConstraints = NO;
    self.loading.text = @"正在打开离线游戏…"; self.loading.textColor = UIColor.whiteColor;
    self.loading.backgroundColor = UIColor.blackColor; self.loading.textAlignment = NSTextAlignmentCenter;
    [self.view addSubview:self.loading];
    [NSLayoutConstraint activateConstraints:@[
        [self.loading.leadingAnchor constraintEqualToAnchor:self.web.leadingAnchor], [self.loading.trailingAnchor constraintEqualToAnchor:self.web.trailingAnchor],
        [self.loading.topAnchor constraintEqualToAnchor:self.web.topAnchor], [self.loading.bottomAnchor constraintEqualToAnchor:self.web.bottomAnchor]
    ]];
    [self setMediaSuspended:YES];
    NSString *origin = [NSString stringWithFormat:@"^http://127\\.0\\.0\\.1:%@/", self.server.entryURL.port];
    NSString *rules = JSONString(@[
        @{ @"trigger":@{@"url-filter":@"^https?://"}, @"action":@{@"type":@"block"} },
        @{ @"trigger":@{@"url-filter":origin}, @"action":@{@"type":@"ignore-previous-rules"} }
    ]);
    __weak XanaduWebGameController *weakSelf = self;
    [WKContentRuleListStore.defaultStore compileContentRuleListForIdentifier:[@"xanadu-offline-" stringByAppendingString:self.game]
        encodedContentRuleList:rules completionHandler:^(WKContentRuleList *list, NSError *error) {
        XanaduWebGameController *owner = weakSelf;
        if (!owner || owner.finished) return;
        if (!list || error) { [owner showFailure:error.localizedDescription ?: @"无法配置离线资源规则。"]; return; }
        [owner.web.configuration.userContentController addContentRuleList:list]; [owner reloadPage];
    }];
    self.lastTick = NSProcessInfo.processInfo.systemUptime;
    self.timer = [NSTimer scheduledTimerWithTimeInterval:.25 repeats:YES block:^(__unused NSTimer *timer) { [weakSelf tick]; }];
}
- (void)viewDidAppear:(BOOL)animated
{
    [super viewDidAppear:animated];
    if (!UnityIsPaused()) { self.ownsUnityPause = YES; UnityPause(1); }
    if (@available(iOS 16.0, *)) {
        [self setNeedsUpdateOfSupportedInterfaceOrientations];
        UIInterfaceOrientationMask initial = self.isCut ? UIInterfaceOrientationMaskPortrait : self.supportedInterfaceOrientations;
        [self.view.window.windowScene requestGeometryUpdateWithPreferences:
            [[UIWindowSceneGeometryPreferencesIOS alloc] initWithInterfaceOrientations:initial]
            errorHandler:^(NSError *error) { NSLog(@"[XanaduWebGames] Orientation request: %@", error.localizedDescription); }];
    } else [UIViewController attemptRotationToDeviceOrientation];
}
- (void)viewSafeAreaInsetsDidChange
{
    [super viewSafeAreaInsetsDidChange];
    [self updateGameViewport];
}
- (void)viewDidLayoutSubviews
{
    [super viewDidLayoutSubviews];
    [self updateGameViewport];
}
- (void)updateGameViewport
{
    if (!self.ready || self.isZhaoyun || self.isCut || !self.web.window) return;
    CGRect bounds = self.web.bounds;
    UIEdgeInsets insets = self.view.safeAreaInsets;
    if (bounds.size.width <= 0 || bounds.size.height <= 0 ||
        (CGRectEqualToRect(bounds, self.viewportBounds) && UIEdgeInsetsEqualToEdgeInsets(insets, self.viewportInsets))) return;
    self.viewportBounds = bounds; self.viewportInsets = insets;
    NSString *script = [NSString stringWithFormat:
        @"window.LevelDevilHost.releaseInput();window.LevelDevilHost.setHallButtonFirstScreenOnly(true);"
         "window.LevelDevilHost.setViewportInsets(%g*innerWidth/%g,%g*innerHeight/%g,%g*innerWidth/%g,%g*innerHeight/%g);true;",
         insets.left, bounds.size.width, insets.top, bounds.size.height,
         insets.right, bounds.size.width, insets.bottom, bounds.size.height];
    [self evaluate:script completion:nil];
}
- (void)setMediaSuspended:(BOOL)suspended
{
    if (!self.web || self.mediaPlaybackSuspended == suspended) return;
    // WebKit counts suspend/resume requests. Repeated YES calls are not idempotent.
    self.mediaPlaybackSuspended = suspended;
    NSLog(@"[XanaduWebGames] %s media suspended=%d", self.game.UTF8String, suspended);
    [self.web setAllMediaPlaybackSuspended:suspended completionHandler:nil];
}
- (void)reloadPage
{
    self.generation++; self.lifecycleRevision++; self.ready = NO; self.configured = NO; self.polling = NO;
    self.fault = NO; self.pendingFailure = nil; self.exitRequest = nil;
    self.viewportBounds = CGRectZero;
    self.loadingElapsed = 0; self.statusElapsed = 0; self.loading.hidden = NO; self.closeButton.enabled = YES;
    self.closeButton.hidden = YES;
    [self.web.configuration.userContentController removeAllUserScripts];
    if (self.bridgeSource) {
        NSString *source = [self.bridgeSource stringByReplacingOccurrencesOfString:@"__XANADU_GENERATION__" withString:[@(self.generation) stringValue]];
        [self.web.configuration.userContentController addUserScript:[[WKUserScript alloc] initWithSource:source injectionTime:WKUserScriptInjectionTimeAtDocumentStart forMainFrameOnly:YES]];
    }
    [self setMediaSuspended:YES];
    self.navigation = [self.web loadRequest:[NSURLRequest requestWithURL:self.server.entryURL cachePolicy:NSURLRequestReloadIgnoringLocalCacheData timeoutInterval:30]];
}
- (BOOL)trustedFrame:(WKFrameInfo *)frame
{
    return frame.isMainFrame && [frame.securityOrigin.protocol isEqual:@"http"] &&
        [frame.securityOrigin.host isEqual:@"127.0.0.1"] && frame.securityOrigin.port == self.server.entryURL.port.integerValue;
}
- (void)userContentController:(WKUserContentController *)controller didReceiveScriptMessage:(WKScriptMessage *)message
{
    if (self.finished || message.webView != self.web || ![self trustedFrame:message.frameInfo] || ![message.body isKindOfClass:NSDictionary.class]) return;
    NSDictionary *body = message.body;
    if (![body[@"generation"] isKindOfClass:NSNumber.class] || [body[@"generation"] integerValue] != self.generation) return;
    NSString *type = body[@"type"];
    if ([type isEqual:@"ready"]) { [self becameReady]; return; }
    if ([type isEqual:@"exit-intent"]) { [self requestClose]; return; }
    if ([type isEqual:@"exit-ready"] && self.exitRequest && [body[@"requestId"] isEqual:self.exitRequest]) { [self finish:nil]; return; }
    if ([type isEqual:@"exit-error"] && self.exitRequest && [body[@"requestId"] isEqual:self.exitRequest]) {
        [self showFailure:[body[@"message"] isKindOfClass:NSString.class] ? body[@"message"] : @"退出保存尚未完成。"]; return;
    }
    if ([type isEqual:@"error"] && ![body[@"recoverable"] boolValue])
        [self showFailure:[body[@"message"] isKindOfClass:NSString.class] ? body[@"message"] : @"游戏报告运行错误。"];
}
- (void)webView:(WKWebView *)webView runJavaScriptTextInputPanelWithPrompt:(NSString *)prompt defaultText:(NSString *)defaultText
    initiatedByFrame:(WKFrameInfo *)frame completionHandler:(void (^)(NSString *))completionHandler
{
    if (self.finished || webView != self.web || !self.isCut || ![self trustedFrame:frame] || ![prompt isEqual:@"__xanadu_cut_save__"] || defaultText.length > 2097152) { completionHandler(nil); return; }
    id request = [NSJSONSerialization JSONObjectWithData:[defaultText dataUsingEncoding:NSUTF8StringEncoding] options:0 error:nil];
    BOOL current = [request isKindOfClass:NSDictionary.class] && [request[@"generation"] isKindOfClass:NSNumber.class] && [request[@"generation"] integerValue] == self.generation;
    completionHandler(JSONString(current ? CutSave(request) : @{ @"ok":@NO, @"error":@"Invalid or expired save request" }));
}
- (void)webView:(WKWebView *)webView decidePolicyForNavigationAction:(WKNavigationAction *)action decisionHandler:(void (^)(WKNavigationActionPolicy))decisionHandler
{
    NSURL *url = action.request.URL;
    BOOL allowed = !self.finished && action.targetFrame.isMainFrame && [url.scheme isEqual:@"http"] &&
        [url.host isEqual:@"127.0.0.1"] && [url.port isEqual:self.server.entryURL.port] &&
        [url.path isEqual:@"/index.html"] && !url.user && !url.password;
    decisionHandler(allowed ? WKNavigationActionPolicyAllow : WKNavigationActionPolicyCancel);
}
- (WKWebView *)webView:(WKWebView *)webView createWebViewWithConfiguration:(WKWebViewConfiguration *)configuration forNavigationAction:(WKNavigationAction *)action windowFeatures:(WKWindowFeatures *)features { return nil; }
- (void)webView:(WKWebView *)webView didFinishNavigation:(WKNavigation *)navigation
{
    if (self.finished || webView != self.web || navigation != self.navigation) return;
    self.configured = YES;
    if (self.isZhaoyun) {
        NSString *script = [NSString stringWithFormat:@"window.XanaduZhaoyun.configure({soundEnabled:%@});", self.sound ? @"true" : @"false"];
        [self evaluate:script completion:^(id result, NSError *error) {
            if (error || ![result isKindOfClass:NSDictionary.class] || ![result[@"ok"] boolValue]) [self showFailure:error.localizedDescription ?: @"赵云与阿斗初始化失败。"];
            else [self applyState];
        }];
    } else [self applyState];
}
- (void)webView:(WKWebView *)webView didFailNavigation:(WKNavigation *)navigation withError:(NSError *)error { if (navigation == self.navigation && error.code != NSURLErrorCancelled) [self showFailure:error.localizedDescription]; }
- (void)webView:(WKWebView *)webView didFailProvisionalNavigation:(WKNavigation *)navigation withError:(NSError *)error { if (navigation == self.navigation && error.code != NSURLErrorCancelled) [self showFailure:error.localizedDescription]; }
- (void)webViewWebContentProcessDidTerminate:(WKWebView *)webView { self.ready = NO; [self showFailure:@"游戏页面已被系统停止。重新加载会恢复最近一次已保存的进度。"]; }
- (void)evaluate:(NSString *)script completion:(void (^)(id, NSError *))completion
{
    if (self.finished || !self.web) return;
    NSInteger epoch = self.generation; WKWebView *page = self.web;
    __weak XanaduWebGameController *weakSelf = self;
    [page evaluateJavaScript:script completionHandler:^(id result, NSError *error) {
        XanaduWebGameController *owner = weakSelf;
        if (!owner || owner.finished || epoch != owner.generation || page != owner.web) return;
        if (completion) completion(result, error);
        else if (error) [owner showFailure:error.localizedDescription];
    }];
}
- (void)becameReady
{
    if (self.ready || self.finished) return;
    self.ready = YES; self.loading.hidden = YES;
    NSLog(@"[XanaduWebGames] %s ready", self.game.UTF8String);
    [self updateGameViewport];
    [self applyState];
}
- (void)tick
{
    double now = NSProcessInfo.processInfo.systemUptime, delta = MIN(1, MAX(0, now - self.lastTick)); self.lastTick = now;
    if (self.finished || !self.active || self.interrupted) return;
    if (self.pendingFailure && !self.failureAlert && self.view.window) { [self showFailure:self.pendingFailure]; return; }
    if (self.fault) return;
    if (!self.ready) {
        self.loadingElapsed += delta;
        if (self.loadingElapsed > 60) { [self showFailure:@"离线游戏加载超时，请重试。"]; return; }
    }
    if (self.exitRequest) {
        self.exitElapsed += delta;
        if (self.exitElapsed > (self.isCut ? 3 : 10)) { [self showFailure:@"退出保存尚未确认，请重试；原有进度不会被清除。"]; return; }
    }
    if ((!self.isZhaoyun && !self.isCut) || !self.configured || self.polling || self.exitRequest || (self.isCut && !self.ready)) return;
    self.statusElapsed += delta;
    if (self.ready && self.statusElapsed < .25) return;
    self.statusElapsed = 0; self.polling = YES;
    [self evaluate:self.isZhaoyun ? @"window.XanaduZhaoyun.status();" : @"window.CutTheRopeOffline.status();" completion:^(id result, NSError *error) {
        self.polling = NO;
        if (error || ![result isKindOfClass:NSDictionary.class]) { [self showFailure:error.localizedDescription ?: @"游戏状态无效。"]; return; }
        self.closeButton.hidden = ![result[@"canReturnToHall"] boolValue];
        if (self.isCut) return;
        BOOL pageReady = [result[@"ready"] boolValue];
        NSString *failure = [result[@"error"] isKindOfClass:NSString.class] ? result[@"error"] : nil;
        if (failure.length) { self.ready = pageReady; [self showFailure:failure]; return; }
        if (pageReady) [self becameReady];
    }];
}
- (void)resigned:(NSNotification *)notification { self.active = NO; [self applyState]; }
- (void)activated:(NSNotification *)notification { self.active = YES; self.lastTick = NSProcessInfo.processInfo.systemUptime; [self applyState]; }
- (void)audioInterrupted:(NSNotification *)notification
{
    dispatch_async(dispatch_get_main_queue(), ^{
        if (self.finished) return;
        self.interrupted = [notification.userInfo[AVAudioSessionInterruptionTypeKey] unsignedIntegerValue] == AVAudioSessionInterruptionTypeBegan;
        [self applyState];
    });
}
- (void)applyState
{
    if (self.finished || !self.web) return;
    BOOL active = self.active && !self.interrupted && !self.fault;
    // A pending Level Devil exit must keep ticking its original save/transition.
    BOOL running = active && (!self.exitRequest || (!self.isZhaoyun && !self.isCut));
    BOOL audible = running && self.sound && !self.exitRequest;
    [self setMediaSuspended:!audible];
    if (audible) XanaduAudio_PrepareSession();
    if (!self.configured && !self.ready) return;
    NSInteger revision = ++self.lifecycleRevision;
    NSString *script;
    if (self.isZhaoyun) script = [NSString stringWithFormat:@"window.XanaduZhaoyun.setActive(%@,%@);", running ? @"true" : @"false", self.fault ? @"false" : @"true"];
    else if (self.isCut) script = [NSString stringWithFormat:@"window.CutTheRopeOffline.setHostPaused(%@);window.CutTheRopeOffline.setSoundEnabled(%@);%@",
        running ? @"false" : @"true", audible ? @"true" : @"false", running || self.fault ? @"true;" : @"window.CutTheRopeOffline.flush();"];
    else script = [NSString stringWithFormat:@"window.LevelDevilHost.setHallButtonFirstScreenOnly(true);window.LevelDevilHost.setHostState(%@,%@);true;", running ? @"true" : @"false", audible ? @"true" : @"false"];
    [self evaluate:script completion:^(id result, NSError *error) {
        if (revision != self.lifecycleRevision || self.fault) return;
        BOOL okay = self.isZhaoyun ? [result isKindOfClass:NSDictionary.class] && [result[@"ok"] boolValue] : [result isEqual:@YES];
        if (error || !okay) [self showFailure:error.localizedDescription ?: @"保存或暂停尚未完成，请重试。"];
    }];
}
- (void)requestClose
{
    if (self.finished || self.exitRequest || self.fault) return;
    if (!self.ready) { [self finish:nil]; return; }
    self.exitRequest = NSUUID.UUID.UUIDString; self.exitElapsed = 0; self.closeButton.enabled = NO;
    NSString *request = self.exitRequest;
    [self applyState];
    if (self.isZhaoyun || self.isCut) {
        NSString *script = self.isZhaoyun ? @"window.XanaduZhaoyun.exit();" :
            @"window.CutTheRopeOffline.setHostPaused(true);window.CutTheRopeOffline.setSoundEnabled(false);window.CutTheRopeOffline.flush();";
        [self evaluate:script completion:^(id result, NSError *error) {
            if (![self.exitRequest isEqual:request]) return;
            BOOL okay = self.isZhaoyun ? [result isKindOfClass:NSDictionary.class] && [result[@"ok"] boolValue] : [result isEqual:@YES];
            if (error || !okay) [self showFailure:error.localizedDescription ?: @"退出保存失败，请重试保存。"];
            else [self finish:nil];
        }];
    } else [self evaluate:[NSString stringWithFormat:@"window.LevelDevilHost.requestExit(%@);true;", JSONString(request)] completion:nil];
}
- (void)retry
{
    self.failureAlert = nil; self.pendingFailure = nil; self.fault = NO;
    if (!self.ready) { [self reloadPage]; return; }
    BOOL closing = self.exitRequest != nil;
    self.exitRequest = nil; self.closeButton.enabled = YES;
    // Clear only the failed host transaction. Preserve original gameplay state.
    if (!self.isZhaoyun && !self.isCut) {
        [self evaluate:@"window.LevelDevilHost.cancelExit();true;" completion:^(__unused id result, NSError *error) {
            if (error) [self showFailure:error.localizedDescription];
            else { [self applyState]; if (closing) [self requestClose]; }
        }];
    } else {
        NSString *flush = self.isZhaoyun ? @"window.XanaduZhaoyun.setActive(false);" : @"window.CutTheRopeOffline.flush();";
        [self evaluate:flush completion:^(id result, NSError *error) {
            BOOL okay = self.isZhaoyun ? [result isKindOfClass:NSDictionary.class] && [result[@"ok"] boolValue] : [result isEqual:@YES];
            if (error || !okay) { if (closing) self.exitRequest = NSUUID.UUID.UUIDString; [self showFailure:error.localizedDescription ?: @"保存仍未完成，请重试。"]; }
            else if (closing) [self requestClose];
            else [self applyState];
        }];
    }
}
- (void)showFailure:(NSString *)message
{
    if (self.finished) return;
    BOOL firstFailure = !self.fault;
    self.fault = YES; self.pendingFailure = message ?: @"游戏暂时无法继续。";
    // Failure reporting pauses once. It must not implicitly retry the failed write.
    if (firstFailure) {
        [self setMediaSuspended:YES];
        [self applyState];
    }
    if (self.failureAlert || !self.active || !self.view.window) return;
    UIAlertController *alert = [UIAlertController alertControllerWithTitle:@"游戏尚未完成保存或加载" message:self.pendingFailure preferredStyle:UIAlertControllerStyleAlert];
    self.failureAlert = alert;
    __weak XanaduWebGameController *weakSelf = self;
    [alert addAction:[UIAlertAction actionWithTitle:self.ready ? @"重试保存" : @"重新加载" style:UIAlertActionStyleDefault handler:^(__unused UIAlertAction *action) { [weakSelf retry]; }]];
    [alert addAction:[UIAlertAction actionWithTitle:self.ready ? @"返回大厅（未确认本次保存）" : @"返回大厅" style:UIAlertActionStyleDestructive handler:^(__unused UIAlertAction *action) {
        XanaduWebGameController *owner = weakSelf; if (owner) [owner finish:owner.pendingFailure];
    }]];
    if (self.exitRequest && !self.isZhaoyun && !self.isCut) [alert addAction:[UIAlertAction actionWithTitle:@"留在游戏" style:UIAlertActionStyleCancel handler:^(__unused UIAlertAction *action) {
        XanaduWebGameController *owner = weakSelf;
        owner.failureAlert = nil; owner.pendingFailure = nil; owner.fault = NO; owner.exitRequest = nil; owner.closeButton.enabled = YES;
        [owner evaluate:@"window.LevelDevilHost.cancelExit();true;" completion:^(__unused id result, NSError *error) { if (error) [owner showFailure:error.localizedDescription]; else [owner applyState]; }];
    }]];
    [self presentViewController:alert animated:YES completion:nil];
}
- (void)finish:(NSString *)error
{
    if (self.finished) return;
    self.finished = YES; self.generation++; self.lifecycleRevision++;
    [self.timer invalidate]; self.timer = nil; [NSNotificationCenter.defaultCenter removeObserver:self];
    [self setMediaSuspended:YES];
    [self.web stopLoading]; self.web.navigationDelegate = nil; self.web.UIDelegate = nil;
    [self.web.configuration.userContentController removeScriptMessageHandlerForName:@"xanadu"];
    [self.web removeFromSuperview]; self.web = nil;
    [self.server stop];
    UIApplication.sharedApplication.idleTimerDisabled = self.previousIdleTimerDisabled;
    void (^complete)(void) = ^{
        LastError = error; Running = NO;
        if (self.ownsUnityPause) { self.ownsUnityPause = NO; UnityPause(0); }
        if (CurrentGame == self) CurrentGame = nil;
        NSLog(@"[XanaduWebGames] %@ closed%@", self.game, error ? [@": " stringByAppendingString:error] : @"");
    };
    if (self.presentingViewController) [self.presentingViewController dismissViewControllerAnimated:NO completion:complete];
    else complete();
}
@end

static int OnMain(int (^operation)(void))
{
    if (NSThread.isMainThread) return operation();
    __block int value; dispatch_sync(dispatch_get_main_queue(), ^{ value = operation(); }); return value;
}
extern "C" int XanaduWebGame_Open(const char *identifier, const char *root, int soundEnabled)
{
    NSString *game = identifier ? [NSString stringWithUTF8String:identifier] : nil;
    NSString *path = root ? [NSString stringWithUTF8String:root] : nil;
    return OnMain(^int {
        if (Running) return 0;
        NSDictionary *ports = @{ @"zhaoyun-adou":@24681, @"cut-the-rope":@24682, @"level-devil":@24683 };
        NSNumber *port = ports[game ?: @""];
        if (!port || !path.length || UIApplication.sharedApplication.applicationState != UIApplicationStateActive) { LastError = @"游戏参数无效或应用不在前台。"; return 0; }
        Running = YES; LastError = nil;
        XanaduWebGameController *owner = [[XanaduWebGameController alloc] init];
        owner.game = game; owner.sound = soundEnabled != 0;
        owner.server = [[XanaduWebAssetServer alloc] initWithRoot:path port:port.unsignedShortValue];
        CurrentGame = owner;
        dispatch_async(dispatch_get_global_queue(QOS_CLASS_USER_INITIATED, 0), ^{
            @autoreleasepool {
                NSError *error = nil; BOOL ready = [owner.server start:&error];
                dispatch_async(dispatch_get_main_queue(), ^{
                    if (CurrentGame != owner) { [owner.server stop]; return; }
                    if (!ready) { LastError = error.localizedDescription; Running = NO; CurrentGame = nil; return; }
                    [owner open];
                });
            }
        });
        return 1;
    });
}
extern "C" int XanaduWebGame_GetRequestedOrientation(void)
{
    // 5 is managed ScreenOrientation.AutoRotation; Unity's native fixed enum has no auto value.
    return OnMain(^int { return UnityShouldAutorotate() ? 5 : UnityRequestedScreenOrientation(); });
}
extern "C" int XanaduWebGame_IsRunning(void) { return OnMain(^int { return Running ? 1 : 0; }); }
extern "C" const char *XanaduWebGame_LastError(void) { return LastError.UTF8String; }
