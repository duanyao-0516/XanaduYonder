// Unity I/O is the only test double: resource parser and cache are production code.
namespace UnityEngine
{
    public sealed class TextAsset
    {
        public readonly byte[] bytes;
        public TextAsset(byte[] value) { bytes = value; }
    }
    public sealed class ResourceRequest { public object asset; }
    public static class Resources
    {
        public static readonly System.Collections.Generic.Dictionary<string, TextAsset> Assets = new System.Collections.Generic.Dictionary<string, TextAsset>();
        public static ResourceRequest LoadAsync<T>(string path) { return new ResourceRequest { asset = Load<T>(path) }; }
        public static T Load<T>(string path) { TextAsset asset; return Assets.TryGetValue(path, out asset) ? (T)(object)asset : default(T); }
        public static void UnloadAsset(object value) { }
    }
    public static class Debug { public static void LogWarning(string message) { } }
}
