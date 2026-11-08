#import <AVFoundation/AVFoundation.h>
#import <UIKit/UIKit.h>
#import <dispatch/dispatch.h>

static int OnMainThread(int (^operation)(void))
{
    if ([NSThread isMainThread]) return operation();
    __block int result = 0;
    dispatch_sync(dispatch_get_main_queue(), ^{ result = operation(); });
    return result;
}

extern "C" int XanaduAudio_IsApplicationActive()
{
    return OnMainThread(^int {
        return [UIApplication sharedApplication].applicationState == UIApplicationStateActive ? 1 : 0;
    });
}

extern "C" int XanaduAudio_PrepareSession()
{
    return OnMainThread(^int {
        @autoreleasepool
        {
            if ([UIApplication sharedApplication].applicationState != UIApplicationStateActive) return 0;

            AVAudioSession *session = [AVAudioSession sharedInstance];
            AVAudioSessionCategoryOptions options = AVAudioSessionCategoryOptionMixWithOthers;
            NSError *error = nil;
            if (![session.category isEqualToString:AVAudioSessionCategoryPlayback] ||
                ![session.mode isEqualToString:AVAudioSessionModeDefault] || session.categoryOptions != options)
            {
                if (![session setCategory:AVAudioSessionCategoryPlayback mode:AVAudioSessionModeDefault
                                  options:options error:&error])
                {
                    NSLog(@"[XanaduAudio] setCategory failed: %@/%ld %@", error.domain,
                        (long)error.code, error.localizedDescription);
                    return -1;
                }
            }

            if (![session setActive:YES error:&error])
            {
                NSLog(@"[XanaduAudio] setActive failed: %@/%ld %@", error.domain,
                    (long)error.code, error.localizedDescription);
                return -2;
            }
            return 1;
        }
    });
}
