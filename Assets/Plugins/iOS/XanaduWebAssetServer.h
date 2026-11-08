#import <Foundation/Foundation.h>

// One immutable, manifest-verified game directory at one stable loopback origin.
@interface XanaduWebAssetServer : NSObject
@property(nonatomic, readonly) NSURL *entryURL;
- (instancetype)initWithRoot:(NSString *)root port:(uint16_t)port;
- (BOOL)start:(NSError **)error;
- (void)stop;
@end
