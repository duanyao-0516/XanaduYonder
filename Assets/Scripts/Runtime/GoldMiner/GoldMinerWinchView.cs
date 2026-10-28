using System;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.GoldMiner;

namespace XanaduYonder.Runtime.GoldMiner
{
    /// <summary>One complete miner/arms/crank/winch illustration per frame, anchored by its measured rope outlet.</summary>
    internal sealed class GoldMinerWinchView
    {
        private const float LeadLength = 8;
        private readonly Image image;
        private readonly Sprite[] frames = new Sprite[GoldMinerReelPlayback.FrameCount];
        private readonly GoldMinerHempRope lead;
        private readonly GoldMinerReelPlayback playback = new GoldMinerReelPlayback();
        private readonly float ropePixelsPerUnit;

        public GoldMinerWinchView(Transform parent, GoldMinerArtwork artwork, Vector2 origin, int playerId,
            bool cooperative, float availableHeight, float worldScale)
        {
            ropePixelsPerUnit = worldScale;
            var root = UiFactory.Rect(parent, "Miner mechanism P" + (playerId + 1), origin.x, origin.y, 0, 0);
            // Both approved sheets face right. P2 mirrors the complete illustration,
            // including the measured outlet, without mirroring any gameplay coordinate.
            root.localScale = new Vector3(playerId == 0 ? 1 : -1, 1, 1);
            string resource = "characters/miner-p" + (playerId + 1) + "-reel-v4";
            float aboveOutlet = 0;
            for (int frame = 0; frame < frames.Length; frame++)
            {
                frames[frame] = artwork.Get(resource, "frame_" + frame);
                if (frame > 0 && frames[frame].rect.size != frames[0].rect.size)
                    throw new InvalidOperationException("Whole miner frames require equal slice dimensions.");
                aboveOutlet = Mathf.Max(aboveOutlet, 1 - frames[frame].pivot.y / frames[frame].rect.height);
            }
            // Fit the full source rectangle, including its transparent top margin. A short
            // landscape HUD may impose a smaller illustration than the preferred height.
            float height = Mathf.Min(cooperative ? 128 : 150, Mathf.Max(1, availableHeight - LeadLength) / Mathf.Max(.01f, aboveOutlet));
            float width = height * frames[0].rect.width / frames[0].rect.height;
            lead = UiFactory.Rect(root, "Winch rope lead P" + (playerId + 1), 0, -LeadLength, 4.4f, LeadLength)
                .gameObject.AddComponent<GoldMinerHempRope>();
            lead.rectTransform.pivot = new Vector2(.5f, 1);
            lead.raycastTarget = false;
            image = GoldMinerSceneView.Picture(root, "Whole miner frame P" + (playerId + 1), frames[0],
                0, -LeadLength, width, height);
            SetFrame(0);
        }

        public void Render(GoldMinerHook hook, GoldMinerPhase gamePhase, bool celebrating, float delta, double retractionSpeed)
        {
            playback.Observe(hook, gamePhase, delta, retractionSpeed);
            SetFrame(playback.FrameIndex);
            lead.SetTravel((float)hook.Length * ropePixelsPerUnit);
            // Celebration/result art belongs to the surrounding page. Replacing or bobbing
            // this whole assembly would disconnect its fixed outlet from the hemp rope.
        }

        private void SetFrame(int frame)
        {
            var sprite = frames[frame];
            image.sprite = sprite;
            image.rectTransform.pivot = new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height);
        }
    }

    /// <summary>Presentation-only six-frame clock driven by actual simulated retrieval, independent of render FPS.</summary>
    internal sealed class GoldMinerReelPlayback
    {
        public const int FrameCount = 6;
        private bool hasLength;
        private double previousLength, cycle;
        public int FrameIndex { get { return Math.Min(FrameCount - 1, (int)(cycle * FrameCount)); } }

        public void Observe(GoldMinerHook hook, GoldMinerPhase gamePhase, float delta, double retractionSpeed)
        {
            double length = hook.Length;
            if (double.IsNaN(length) || double.IsInfinity(length)) { hasLength = false; return; }
            double travel = hasLength ? previousLength - length : 0;
            previousLength = length; hasLength = true;
            bool pulling = hook.State == GoldMinerHookState.RetractingEmpty || hook.State == GoldMinerHookState.RetractingLoaded;
            bool active = gamePhase == GoldMinerPhase.Running || gamePhase == GoldMinerPhase.Draining;
            // Consume every observation, including paused ones, so resuming cannot replay
            // a position jump or a new checkpoint as accumulated animation movement.
            if (!active || !pulling || travel <= 0 || delta <= 0 || float.IsNaN(delta) || float.IsInfinity(delta) ||
                retractionSpeed <= 0 || double.IsNaN(retractionSpeed) || double.IsInfinity(retractionSpeed)) return;
            double lightness = Math.Max(0, Math.Min(1, (retractionSpeed - .48) / (2.4 - .48)));
            double secondsPerCycle = .95 - .35 * lightness;
            double motionSeconds = travel / retractionSpeed;
            cycle = (cycle + motionSeconds / secondsPerCycle) % 1;
        }
    }
}
