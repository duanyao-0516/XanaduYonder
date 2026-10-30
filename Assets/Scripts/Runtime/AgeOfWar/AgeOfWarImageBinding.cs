using UnityEngine;

namespace XanaduYonder.Runtime.AgeOfWar
{
    // Resampling changes sprite bounds as well as pixels. Rebind the original key/frame while
    // retaining live HUD transforms, animation alpha and the current action's playback position.
    internal sealed class AgeOfWarImageBinding : MonoBehaviour
    {
        internal AgeOfWarImage Owner;
        internal static void RebindAll(Transform stage)
        {
            foreach(var binding in stage.GetComponentsInChildren<AgeOfWarImageBinding>(true))
                if(binding.Owner!=null)binding.Owner.Rebind();
        }
        private void OnDestroy(){Owner=null;}
    }
}
