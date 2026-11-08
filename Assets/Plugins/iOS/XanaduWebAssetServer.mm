#import "XanaduWebAssetServer.h"
#import <CommonCrypto/CommonDigest.h>
#import <arpa/inet.h>
#import <sys/socket.h>
#import <fcntl.h>
#import <unistd.h>

static NSError *AssetError(NSString *message)
{
    return [NSError errorWithDomain:@"XanaduWebAssets" code:1 userInfo:@{NSLocalizedDescriptionKey:message}];
}

static BOOL SafeAssetPath(NSString *path)
{
    if (![path isKindOfClass:NSString.class] || path.length == 0 || [path hasPrefix:@"/"] ||
        [path rangeOfCharacterFromSet:NSCharacterSet.controlCharacterSet].location != NSNotFound ||
        [path containsString:@"\\"] || [path containsString:@"%"] || [path isEqualToString:@"manifest.json"]) return NO;
    for (NSString *part in [path componentsSeparatedByString:@"/"])
        if (!part.length || [part isEqualToString:@"."] || [part isEqualToString:@".."]) return NO;
    return YES;
}

static NSString *Digest(NSData *data)
{
    unsigned char bytes[CC_SHA256_DIGEST_LENGTH];
    CC_SHA256(data.bytes, (CC_LONG)data.length, bytes);
    NSMutableString *result = [NSMutableString stringWithCapacity:64];
    for (NSUInteger i = 0; i < sizeof(bytes); ++i) [result appendFormat:@"%02x", bytes[i]];
    return result;
}

static NSString *Mime(NSString *path)
{
    static NSDictionary *types;
    static dispatch_once_t once;
    dispatch_once(&once, ^{
        types = @{@"html":@"text/html", @"js":@"application/javascript", @"css":@"text/css",
                  @"json":@"application/json", @"png":@"image/png", @"jpg":@"image/jpeg",
                  @"jpeg":@"image/jpeg", @"webp":@"image/webp", @"gif":@"image/gif",
                  @"mp3":@"audio/mpeg", @"ogg":@"audio/ogg", @"wav":@"audio/wav",
                  @"m4a":@"audio/mp4", @"aac":@"audio/aac", @"mp4":@"audio/mp4",
                  @"woff":@"font/woff", @"woff2":@"font/woff2", @"ttf":@"font/ttf"};
    });
    return types[path.pathExtension.lowercaseString] ?: @"application/octet-stream";
}

static BOOL SendBytes(int socket, const void *bytes, NSUInteger count)
{
    const char *cursor = (const char *)bytes;
    while (count) {
        ssize_t sent = send(socket, cursor, count, 0);
        if (sent < 0 && errno == EINTR) continue;
        if (sent <= 0) return NO;
        cursor += sent; count -= (NSUInteger)sent;
    }
    return YES;
}

static void Respond(int socket, int status, NSString *mime, NSData *data, NSUInteger offset,
                    NSUInteger count, BOOL head, NSString *extra)
{
    NSString *reason = @{@200:@"OK", @206:@"Partial Content", @400:@"Bad Request", @403:@"Forbidden",
                          @404:@"Not Found", @405:@"Method Not Allowed", @416:@"Range Not Satisfiable",
                          @500:@"Internal Server Error"}[@(status)] ?: @"Error";
    NSString *header = [NSString stringWithFormat:
        @"HTTP/1.1 %d %@\r\nContent-Type: %@\r\nContent-Length: %lu\r\nConnection: close\r\n"
         "Cache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\nAccept-Ranges: bytes\r\n%@\r\n",
        status, reason, mime ?: @"text/plain", (unsigned long)count, extra ?: @""];
    NSData *encoded = [header dataUsingEncoding:NSUTF8StringEncoding];
    if (SendBytes(socket, encoded.bytes, encoded.length) && !head && data && count)
        SendBytes(socket, ((const char *)data.bytes) + offset, count);
}

@interface XanaduWebAssetServer ()
@property(nonatomic, copy) NSString *root;
@property(nonatomic) uint16_t port;
@property(nonatomic, copy) NSDictionary<NSString *, NSDictionary *> *files;
@property(nonatomic, strong) dispatch_source_t acceptSource;
@property(nonatomic, strong) dispatch_queue_t acceptQueue;
@property(nonatomic) int listener;
@end

@implementation XanaduWebAssetServer
- (instancetype)initWithRoot:(NSString *)root port:(uint16_t)port
{
    if ((self = [super init])) {
        _root = [root stringByStandardizingPath]; _port = port; _listener = -1;
        _acceptQueue = dispatch_queue_create("com.xanaduyonder.web-assets", DISPATCH_QUEUE_SERIAL);
    }
    return self;
}
- (NSURL *)entryURL { return [NSURL URLWithString:[NSString stringWithFormat:@"http://127.0.0.1:%u/index.html", self.port]]; }
- (BOOL)start:(NSError **)error
{
    if (self.listener >= 0) return YES;
    NSData *manifest = [NSData dataWithContentsOfFile:[self.root stringByAppendingPathComponent:@"manifest.json"] options:0 error:error];
    if (!manifest) return NO;
    id document = [NSJSONSerialization JSONObjectWithData:manifest options:0 error:error];
    id rows = [document isKindOfClass:NSDictionary.class] ? document[@"files"] : nil;
    if (![rows isKindOfClass:NSArray.class] || ![rows count]) { if (error) *error = AssetError(@"游戏资源清单无效。"); return NO; }
    NSMutableDictionary *files = [NSMutableDictionary dictionary];
    for (id row in rows) {
        if (![row isKindOfClass:NSDictionary.class]) { if (error) *error = AssetError(@"游戏资源条目无效。"); return NO; }
        NSString *path = row[@"path"], *hash = row[@"sha256"], *mime = row[@"mime"];
        NSNumber *size = row[@"bytes"];
        if (!SafeAssetPath(path) || files[path] || ![hash isKindOfClass:NSString.class] || hash.length != 64 ||
            ![size isKindOfClass:NSNumber.class] || size.longLongValue < 0 ||
            (mime && (![mime isKindOfClass:NSString.class] || [mime rangeOfCharacterFromSet:NSCharacterSet.controlCharacterSet].location != NSNotFound))) {
            if (error) *error = AssetError(@"游戏资源清单包含非法路径或字段。"); return NO;
        }
        NSString *absolute = [self.root stringByAppendingPathComponent:path];
        NSString *resolved = absolute.stringByResolvingSymlinksInPath;
        if (![resolved hasPrefix:[self.root.stringByResolvingSymlinksInPath stringByAppendingString:@"/"]] ||
            ![[NSFileManager.defaultManager attributesOfItemAtPath:absolute error:nil][NSFileType] isEqual:NSFileTypeRegular]) {
            if (error) *error = AssetError(@"游戏资源路径越界或文件缺失。"); return NO;
        }
        NSData *content = [NSData dataWithContentsOfFile:absolute options:NSDataReadingMappedIfSafe error:error];
        if (!content) return NO;
        if (content.length != size.unsignedLongLongValue || ![Digest(content) isEqualToString:hash]) {
            if (error) *error = AssetError([@"游戏资源校验失败：" stringByAppendingString:path]); return NO;
        }
        files[path] = @{ @"path":absolute, @"bytes":size, @"mime":mime ?: Mime(path) };
    }
    if (!files[@"index.html"]) { if (error) *error = AssetError(@"游戏入口不存在。"); return NO; }
    self.files = files;
    int socketFD = socket(AF_INET, SOCK_STREAM, 0);
    if (socketFD < 0) { if (error) *error = AssetError(@"无法创建本地资源服务。"); return NO; }
    int yes = 1;
    setsockopt(socketFD, SOL_SOCKET, SO_REUSEADDR, &yes, sizeof(yes));
    struct sockaddr_in address = {};
    address.sin_len = sizeof(address); address.sin_family = AF_INET;
    address.sin_addr.s_addr = htonl(INADDR_LOOPBACK); address.sin_port = htons(self.port);
    if (bind(socketFD, (struct sockaddr *)&address, sizeof(address)) != 0 || listen(socketFD, 16) != 0 ||
        fcntl(socketFD, F_SETFL, O_NONBLOCK) != 0) {
        close(socketFD); if (error) *error = AssetError(@"本地游戏端口不可用，请关闭后重试。"); return NO;
    }
    self.listener = socketFD;
    self.acceptSource = dispatch_source_create(DISPATCH_SOURCE_TYPE_READ, socketFD, 0, self.acceptQueue);
    __weak XanaduWebAssetServer *weakSelf = self;
    dispatch_source_set_event_handler(self.acceptSource, ^{
        XanaduWebAssetServer *owner = weakSelf;
        if (!owner) return;
        while (true) {
            int client = accept(socketFD, NULL, NULL);
            if (client < 0) break;
            // Darwin inherits O_NONBLOCK from the listening socket. Each worker
            // uses bounded blocking I/O so large PAK/audio responses cannot truncate at EAGAIN.
            int flags = fcntl(client, F_GETFL, 0);
            if (flags < 0 || fcntl(client, F_SETFL, flags & ~O_NONBLOCK) != 0) { close(client); continue; }
            struct timeval timeout = {5, 0};
            setsockopt(client, SOL_SOCKET, SO_RCVTIMEO, &timeout, sizeof(timeout));
            setsockopt(client, SOL_SOCKET, SO_SNDTIMEO, &timeout, sizeof(timeout));
            setsockopt(client, SOL_SOCKET, SO_NOSIGPIPE, &yes, sizeof(yes));
            dispatch_async(dispatch_get_global_queue(QOS_CLASS_USER_INITIATED, 0), ^{
                @autoreleasepool { [owner serve:client]; close(client); }
            });
        }
    });
    dispatch_source_set_cancel_handler(self.acceptSource, ^{ close(socketFD); });
    dispatch_resume(self.acceptSource);
    return YES;
}
- (void)stop
{
    if (self.acceptSource) { dispatch_source_cancel(self.acceptSource); self.acceptSource = nil; }
    self.listener = -1;
}
- (void)dealloc { [self stop]; }
- (void)serve:(int)socketFD
{
    NSMutableData *request = [NSMutableData data];
    NSData *terminator = [@"\r\n\r\n" dataUsingEncoding:NSUTF8StringEncoding];
    while ([request rangeOfData:terminator options:0 range:NSMakeRange(0, request.length)].location == NSNotFound) {
        char bytes[2048]; ssize_t count = recv(socketFD, bytes, sizeof(bytes), 0);
        if (count < 0 && errno == EINTR) continue;
        if (count <= 0) return;
        [request appendBytes:bytes length:(NSUInteger)count];
        if (request.length > 16384) { Respond(socketFD, 400, nil, nil, 0, 0, NO, nil); return; }
    }
    NSString *text = [[NSString alloc] initWithData:request encoding:NSUTF8StringEncoding];
    NSArray *lines = [text componentsSeparatedByString:@"\r\n"];
    NSArray *first = [lines.firstObject componentsSeparatedByString:@" "];
    if (first.count != 3 || ![first[2] hasPrefix:@"HTTP/1."]) { Respond(socketFD, 400, nil, nil, 0, 0, NO, nil); return; }
    NSString *method = first[0], *target = first[1];
    BOOL head = [method isEqual:@"HEAD"];
    if (!head && ![method isEqual:@"GET"]) { Respond(socketFD, 405, nil, nil, 0, 0, head, @"Allow: GET, HEAD\r\n"); return; }
    NSMutableDictionary *headers = [NSMutableDictionary dictionary];
    for (NSUInteger i = 1; i < lines.count && [lines[i] length]; ++i) {
        NSRange colon = [lines[i] rangeOfString:@":"];
        if (colon.location == NSNotFound) { Respond(socketFD, 400, nil, nil, 0, 0, head, nil); return; }
        NSString *name = [[lines[i] substringToIndex:colon.location] lowercaseString];
        if (headers[name]) { Respond(socketFD, 400, nil, nil, 0, 0, head, nil); return; }
        headers[name] = [[lines[i] substringFromIndex:colon.location + 1] stringByTrimmingCharactersInSet:NSCharacterSet.whitespaceCharacterSet];
    }
    NSString *expectedHost = [NSString stringWithFormat:@"127.0.0.1:%u", self.port];
    NSString *rawPath = [[target componentsSeparatedByString:@"?"] firstObject];
    NSString *path = [rawPath stringByRemovingPercentEncoding];
    if (![headers[@"host"] isEqual:expectedHost] || ![rawPath hasPrefix:@"/"] ||
        [rawPath.lowercaseString containsString:@"%2f"] || [rawPath.lowercaseString containsString:@"%5c"] ||
        [target containsString:@"#"] || !path) { Respond(socketFD, 403, nil, nil, 0, 0, head, nil); return; }
    path = [path isEqual:@"/"] ? @"index.html" : [path substringFromIndex:1];
    if (!SafeAssetPath(path)) { Respond(socketFD, 403, nil, nil, 0, 0, head, nil); return; }
    NSDictionary *row = self.files[path];
    if (!row) { Respond(socketFD, 404, nil, nil, 0, 0, head, nil); return; }
    NSData *content = [NSData dataWithContentsOfFile:row[@"path"] options:NSDataReadingMappedIfSafe error:nil];
    if (!content || content.length != [row[@"bytes"] unsignedLongLongValue]) { Respond(socketFD, 500, nil, nil, 0, 0, head, nil); return; }
    NSUInteger offset = 0, count = content.length;
    int status = 200; NSString *extra = nil, *range = headers[@"range"];
    if (range) {
        NSRegularExpression *pattern = [NSRegularExpression regularExpressionWithPattern:@"^bytes=([0-9]*)-([0-9]*)$" options:0 error:nil];
        NSTextCheckingResult *match = [pattern firstMatchInString:range options:0 range:NSMakeRange(0, range.length)];
        BOOL valid = match != nil && count > 0;
        if (valid) {
            NSString *a = [range substringWithRange:[match rangeAtIndex:1]], *b = [range substringWithRange:[match rangeAtIndex:2]];
            valid = (a.length || b.length) && a.length < 19 && b.length < 19;
            if (!a.length) {
                NSUInteger suffix = (NSUInteger)b.longLongValue;
                valid = valid && suffix > 0; offset = count - MIN(count, suffix);
            } else {
                offset = (NSUInteger)a.longLongValue;
                NSUInteger end = b.length ? MIN((NSUInteger)b.longLongValue, count - 1) : count - 1;
                valid = valid && offset < count && end >= offset;
                if (valid) count = end + 1;
            }
            if (valid) { count -= offset; status = 206; extra = [NSString stringWithFormat:@"Content-Range: bytes %lu-%lu/%lu\r\n", (unsigned long)offset, (unsigned long)(offset + count - 1), (unsigned long)content.length]; }
        }
        if (!valid) { Respond(socketFD, 416, nil, nil, 0, 0, head, [NSString stringWithFormat:@"Content-Range: bytes */%lu\r\n", (unsigned long)content.length]); return; }
    }
    Respond(socketFD, status, row[@"mime"], content, offset, count, head, extra);
}
@end
