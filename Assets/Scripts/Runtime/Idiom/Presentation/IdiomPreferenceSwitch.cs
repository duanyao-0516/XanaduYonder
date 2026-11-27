using System;
using UnityEngine;

namespace XanaduYonder.Runtime.Idiom
{
    internal static class IdiomPreferenceSwitch
    {
        public static void Create(Transform parent, string caption, float y, bool initial, bool enabled, Action<bool> changed)
        {
            var row = DialogUi.SwitchRow(parent, caption, 32, y, 532, 64, initial, changed, enabled, 24);
            row.name = caption + " switch";
        }
    }
}
