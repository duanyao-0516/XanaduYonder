#include <jni.h>
#include <lua.h>
#include <lauxlib.h>
#include <stdbool.h>
#include <stdint.h>
#include <string.h>

extern void *SDL_GetAndroidJNIEnv(void);
extern void *SDL_GetAndroidActivity(void);

/* Public SDL3/OpenAL C ABI from the pinned runtime. Only the leading Uint32
 * event type is read; SDL owns the complete event union and its lifetime. */
typedef union SDL_Event SDL_Event;
typedef bool (*SDL_EventFilter)(void *, SDL_Event *);
extern bool SDL_AddEventWatch(SDL_EventFilter filter, void *userdata);
extern void SDL_RemoveEventWatch(SDL_EventFilter filter, void *userdata);
typedef struct ALCcontext_struct ALCcontext;
typedef struct ALCdevice_struct ALCdevice;
extern ALCcontext *alcGetCurrentContext(void);
extern ALCdevice *alcGetContextsDevice(ALCcontext *context);
extern void alcDevicePauseSOFT(ALCdevice *device);
extern void alcDeviceResumeSOFT(ALCdevice *device);

enum {
    SDL_EVENT_WILL_ENTER_BACKGROUND = 0x103,
    SDL_EVENT_DID_ENTER_BACKGROUND = 0x104,
    SDL_EVENT_DID_ENTER_FOREGROUND = 0x106
};

typedef struct {
    bool registered;
    bool paused;
} AudioLifecycle;

static const char *AUDIO_LIFECYCLE_KEY = "XanaduYonder.KingdomRush.AudioLifecycle";

static bool watch_audio_lifecycle(void *userdata, SDL_Event *event) {
    uint32_t type;
    memcpy(&type, event, sizeof(type));
    AudioLifecycle *lifecycle = userdata;
    bool background = type == SDL_EVENT_WILL_ENTER_BACKGROUND || type == SDL_EVENT_DID_ENTER_BACKGROUND;
    if (!background && type != SDL_EVENT_DID_ENTER_FOREGROUND) return true;
    if (background == lifecycle->paused) return true;

    /* SDL's Android JNI callbacks enqueue lifecycle work; Android_PumpEvents
     * emits these app events on the LÖVE game thread before blocking it. The
     * pinned LÖVE uses alcMakeContextCurrent, not a UI-thread-local context.
     * Resolve the live device each time, never retain one across Lua teardown. */
    ALCcontext *context = alcGetCurrentContext();
    ALCdevice *device = context ? alcGetContextsDevice(context) : NULL;
    if (background) {
        if (device) {
            alcDevicePauseSOFT(device);
            lifecycle->paused = true;
        }
    } else {
        if (device) alcDeviceResumeSOFT(device);
        lifecycle->paused = false;
    }
    return true;
}

static int release_audio_lifecycle(lua_State *L) {
    AudioLifecycle *lifecycle = lua_touserdata(L, 1);
    if (lifecycle && lifecycle->registered) {
        /* SDL synchronizes removal with dispatch. Do not resume audio while
         * a background game is closing; OpenAL owns device destruction. */
        SDL_RemoveEventWatch(watch_audio_lifecycle, lifecycle);
        lifecycle->registered = false;
    }
    return 0;
}

static void install_audio_lifecycle(lua_State *L) {
    lua_getfield(L, LUA_REGISTRYINDEX, AUDIO_LIFECYCLE_KEY);
    bool installed = lua_isuserdata(L, -1);
    lua_pop(L, 1);
    if (installed) return;

    AudioLifecycle *lifecycle = lua_newuserdata(L, sizeof(*lifecycle));
    memset(lifecycle, 0, sizeof(*lifecycle));
    if (luaL_newmetatable(L, AUDIO_LIFECYCLE_KEY)) {
        lua_pushcfunction(L, release_audio_lifecycle);
        lua_setfield(L, -2, "__gc");
    }
    lua_setmetatable(L, -2);
    lifecycle->registered = SDL_AddEventWatch(watch_audio_lifecycle, lifecycle);
    if (!lifecycle->registered) luaL_error(L, "Cannot register Kingdom Rush audio lifecycle");
    /* Keep the watcher alive until this actual Lua runtime closes. */
    lua_setfield(L, LUA_REGISTRYINDEX, AUDIO_LIFECYCLE_KEY);
}

static int call_java(lua_State *L, const char *method, const char *signature, int returns_string) {
    const char *argument = luaL_checkstring(L, 1);
    JNIEnv *env = (JNIEnv *) SDL_GetAndroidJNIEnv();
    if (!env) return luaL_error(L, "Android JNI environment unavailable");
    jobject activity = (jobject) SDL_GetAndroidActivity();
    if (!activity) return luaL_error(L, "Kingdom Rush Activity unavailable");
    jclass type = (*env)->GetObjectClass(env, activity);
    jmethodID method_id = type ? (*env)->GetMethodID(env, type, method, signature) : NULL;
    jstring input = method_id ? (*env)->NewStringUTF(env, argument) : NULL;
    jstring result = NULL;
    if (input) {
        if (returns_string) result = (jstring) (*env)->CallObjectMethod(env, activity, method_id, input);
        else (*env)->CallVoidMethod(env, activity, method_id, input);
    }
    int failed = !input || (*env)->ExceptionCheck(env);
    if ((*env)->ExceptionCheck(env)) {
        (*env)->ExceptionDescribe(env);
        (*env)->ExceptionClear(env);
    }
    if (input) (*env)->DeleteLocalRef(env, input);
    if (type) (*env)->DeleteLocalRef(env, type);
    (*env)->DeleteLocalRef(env, activity);
    if (failed) {
        if (result) (*env)->DeleteLocalRef(env, result);
        return luaL_error(L, "Android method %s failed for %s", method, argument);
    }
    if (returns_string) {
        if (!result) return luaL_error(L, "Android property %s is unavailable", argument);
        const char *utf = (*env)->GetStringUTFChars(env, result, NULL);
        if (!utf) {
            (*env)->ExceptionClear(env);
            (*env)->DeleteLocalRef(env, result);
            return luaL_error(L, "Cannot read Android property %s", argument);
        }
        lua_pushstring(L, utf);
        (*env)->ReleaseStringUTFChars(env, result, utf);
        (*env)->DeleteLocalRef(env, result);
        return 1;
    }
    return 0;
}

static int get_system_property(lua_State *L) {
    return call_java(L, "getSystemProperty", "(Ljava/lang/String;)Ljava/lang/String;", 1);
}

static int report_error(lua_State *L) {
    // Preserve the caller's diagnostic text locally; do not terminate the shared Unity process.
    return call_java(L, "reportGameError", "(Ljava/lang/String;)V", 0);
}

__attribute__((visibility("default"))) int luaopen_libjni_android(lua_State *L) {
    install_audio_lifecycle(L);
    static const luaL_Reg functions[] = {
        {"get_system_property", get_system_property},
        {"crashlytics_log_and_crash", report_error},
        {NULL, NULL}
    };
    lua_newtable(L);
    luaL_register(L, NULL, functions);
    return 1;
}
