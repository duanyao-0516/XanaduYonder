#import "../../Assets/Plugins/iOS/XanaduWebGames.mm"

extern "C" UIViewController *UnityGetGLViewController(void) { return nil; }
extern "C" void UnityPause(int) {}
extern "C" int UnityIsPaused(void) { return 1; }
extern "C" int UnityShouldAutorotate(void) { return 1; }
extern "C" int UnityRequestedScreenOrientation(void) { return 0; }
extern "C" int XanaduAudio_PrepareSession(void) { return 1; }

// Exercise the shipping controller and UIKit hit testing without starting a Unity or web game.
@interface Fixture : XanaduWebGameController
@end
@implementation Fixture
- (void)viewDidLoad { self.view.backgroundColor = UIColor.lightGrayColor; }
- (void)viewDidAppear:(BOOL)animated {}
- (void)evaluate:(NSString *)script completion:(void (^)(id, NSError *))completion {
    NSCAssert([script containsString:@"cancelExit"], @"An outside tap issued an unexpected game operation");
    if (completion) completion(@YES, nil);
}
@end

@interface Host : UIResponder <UIApplicationDelegate>
@property(nonatomic, strong) UIWindow *window;
@end
@implementation Host
- (BOOL)application:(UIApplication *)app didFinishLaunchingWithOptions:(NSDictionary *)options {
    self.window = [[UIWindow alloc] initWithFrame:UIScreen.mainScreen.bounds];
    self.window.rootViewController = [UIViewController new];
    [self.window makeKeyAndVisible];
    dispatch_async(dispatch_get_main_queue(), ^{ [self run]; });
    return YES;
}
- (void)run {
    NSURL *docs = [NSFileManager.defaultManager URLsForDirectory:NSDocumentDirectory inDomains:NSUserDomainMask].firstObject;
    NSString *outcome = @"PASS";
    @try {
        for (NSString *game in @[@"bomb-it", @"level-devil", @"zhaoyun-adou", @"cut-the-rope"]) {
            for (NSNumber *recoverable in @[@NO, @YES]) {
                Fixture *controller = [Fixture new];
                controller.game = game;
                controller.active = YES;
                controller.ready = recoverable.boolValue;
                if (recoverable.boolValue) controller.exitRequest = @"test-pending-save";
                UIView *root = self.window.rootViewController.view;
                [root addSubview:controller.view];
                // Use the game's supported orientation in phone points.
                CGSize screen = root.bounds.size;
                BOOL landscape = controller.isBombIt || controller.isLevelDevil;
                controller.view.frame = CGRectMake(0, 0, landscape ? MAX(screen.width,screen.height) : MIN(screen.width,screen.height),
                    landscape ? MIN(screen.width,screen.height) : MAX(screen.width,screen.height));
                [controller showFailure:@"当前操作尚未完成。"];
                UIView *overlay = controller.failurePanel;
                [overlay layoutIfNeeded];
                UIScrollView *panel = (UIScrollView *)overlay.subviews.lastObject;
                NSAssert([panel isKindOfClass:UIScrollView.class] && panel.layer.cornerRadius == 16, @"Card shape");
                UIStackView *items = (UIStackView *)panel.subviews.firstObject;
                UILabel *heading = (UILabel *)items.arrangedSubviews[0], *body = (UILabel *)items.arrangedSubviews[1];
                UIButton *action = (UIButton *)items.arrangedSubviews[2];
                CGFloat headingSize = [[UIFontMetrics metricsForTextStyle:UIFontTextStyleHeadline] scaledValueForValue:16];
                CGFloat bodySize = [[UIFontMetrics metricsForTextStyle:UIFontTextStyleSubheadline] scaledValueForValue:14];
                CGFloat actionSize = [[UIFontMetrics metricsForTextStyle:UIFontTextStyleBody] scaledValueForValue:14];
                NSAssert(fabs(heading.font.pointSize-headingSize)<.5 && fabs(body.font.pointSize-bodySize)<.5
                    && fabs(action.titleLabel.font.pointSize-actionSize)<.5, @"Mobile text sizes");
                NSAssert(heading.adjustsFontForContentSizeCategory && body.adjustsFontForContentSizeCategory
                    && action.titleLabel.adjustsFontForContentSizeCategory, @"Dynamic Type");
                NSAssert(CGRectContainsRect(overlay.bounds, panel.frame) && panel.frame.size.height >= 180, @"Card inside viewport");
                NSAssert([overlay hitTest:panel.center withEvent:nil] != overlay.subviews.firstObject, @"Card intercepts taps");
                UIButton *shade = (UIButton *)[overlay hitTest:CGPointMake(4, 4) withEvent:nil];
                NSAssert(shade == overlay.subviews.firstObject && [shade isKindOfClass:UIButton.class], @"Exterior hits the scrim");
                UIGraphicsImageRenderer *renderer = [[UIGraphicsImageRenderer alloc] initWithBounds:controller.view.bounds];
                NSData *capture = [renderer PNGDataWithActions:^(UIGraphicsImageRendererContext *context) {
                    [controller.view.layer renderInContext:context.CGContext];
                }];
                NSString *name = [NSString stringWithFormat:@"%@-%@.png", game, recoverable.boolValue ? @"save" : @"load"];
                NSAssert([capture writeToURL:[docs URLByAppendingPathComponent:name] atomically:YES], @"Write capture");
                [shade sendActionsForControlEvents:UIControlEventTouchUpInside];
                NSAssert(!controller.finished && !controller.failurePanel, @"Exterior closes only the error");
                if (recoverable.boolValue && (controller.isBombIt || controller.isLevelDevil)) {
                    NSAssert(!controller.exitRequest && !controller.fault, @"Pending exit cancelled");
                } else {
                    NSAssert(controller.fault && controller.failureDetails, @"Error remains accessible");
                    [controller.failureDetails sendActionsForControlEvents:UIControlEventTouchUpInside];
                    NSAssert(controller.failurePanel && !controller.failureDetails, @"Details reopen");
                }
                [controller.view removeFromSuperview];
            }
        }
    } @catch (NSException *exc) {
        outcome = [@"FAIL " stringByAppendingString:exc.reason];
    }
    [outcome writeToURL:[docs URLByAppendingPathComponent:@"result.txt"] atomically:YES encoding:NSUTF8StringEncoding error:nil];
}
@end
int main(int argc, char **argv) {
    @autoreleasepool { return UIApplicationMain(argc, argv, nil, NSStringFromClass(Host.class)); }
}
