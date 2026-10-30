#include <jni.h>
#include <lua.h>
#include <lauxlib.h>

extern void *SDL_GetAndroidJNIEnv(void);
extern void *SDL_GetAndroidActivity(void);

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
    static const luaL_Reg functions[] = {
        {"get_system_property", get_system_property},
        {"crashlytics_log_and_crash", report_error},
        {NULL, NULL}
    };
    lua_newtable(L);
    luaL_register(L, NULL, functions);
    return 1;
}
