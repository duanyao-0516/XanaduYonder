#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

namespace XanaduYonder.Runtime.GoldMiner
{
    // Validation builds only. Measures real rendered foreground frames, including save stalls.
    internal sealed class GoldMinerDiagnostics
    {
        private readonly List<double> milliseconds = new List<double>(4096);
        private readonly string mode;
        private readonly int collections = GC.CollectionCount(0);
        public GoldMinerDiagnostics(string mode) { this.mode = mode; }
        public void Frame(double delta)
        {
            if (delta > 0) milliseconds.Add(delta * 1000);
        }
        public void Finish()
        {
            if (milliseconds.Count == 0) return;
            milliseconds.Sort();
            double total = 0;
            foreach (var value in milliseconds) total += value;
            Debug.Log("GOLD_MINER_FRAME_PROFILE mode=" + mode + " frames=" + milliseconds.Count +
                " meanMs=" + (total / milliseconds.Count).ToString("F2", System.Globalization.CultureInfo.InvariantCulture) +
                " p95Ms=" + milliseconds[(int)((milliseconds.Count - 1) * .95)].ToString("F2", System.Globalization.CultureInfo.InvariantCulture) +
                " p99Ms=" + milliseconds[(int)((milliseconds.Count - 1) * .99)].ToString("F2", System.Globalization.CultureInfo.InvariantCulture) +
                " gc0=" + (GC.CollectionCount(0) - collections) + " unityAllocatedBytes=" + Profiler.GetTotalAllocatedMemoryLong());
            milliseconds.Clear();
        }
    }
}
#endif
