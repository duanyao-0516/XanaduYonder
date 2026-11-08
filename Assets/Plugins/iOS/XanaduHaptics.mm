#import <UIKit/UIKit.h>
#import <dispatch/dispatch.h>
#include "UnityInterface.h"

static void PulseOnMainThread()
{
    @autoreleasepool
    {
        // Check when the queued request executes, not on its originating thread.
        if ([UIApplication sharedApplication].applicationState != UIApplicationStateActive) return;
        UIView *view = UnityGetGLView();
        if (view == nil || view.window == nil) return;

        if (@available(iOS 17.5, *))
        {
            UIImpactFeedbackGenerator *generator = [UIImpactFeedbackGenerator
                feedbackGeneratorWithStyle:UIImpactFeedbackStyleLight forView:view];
            [generator impactOccurred];
            // The factory attaches an interaction; one-shot pulses must not accumulate them.
            [view removeInteraction:generator];
        }
        else
        {
            UIImpactFeedbackGenerator *generator = [[UIImpactFeedbackGenerator alloc]
                initWithStyle:UIImpactFeedbackStyleLight];
            [generator impactOccurred];
#if !__has_feature(objc_arc)
            [generator release];
#endif
        }
    }
}

extern "C" void XanaduHaptics_Pulse()
{
    if ([NSThread isMainThread]) PulseOnMainThread();
    else dispatch_async(dispatch_get_main_queue(), ^{ PulseOnMainThread(); });
}
