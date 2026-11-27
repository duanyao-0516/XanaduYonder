namespace XanaduYonder.Runtime
{
    public sealed partial class AppController
    {
        private const string FeedbackGuidance =
            "所有游戏共用这两个开关。声音大小由系统媒体音量控制。触感需要设备支持，只在特定动作触发，例如合并数字、跳跃落地、扫雷插旗或飞鸟得分。"
#if UNITY_IOS
            + "\n\n在系统「设置 → 声音与触感」打开「系统触感」，并在「辅助功能 → 触控」打开「振动」。"
#elif UNITY_ANDROID
            + "\n\n触感偏弱时，可在系统设置中检查触摸反馈开关和强度。不同机型的选项名称可能不同。"
#endif
            ;
    }
}
