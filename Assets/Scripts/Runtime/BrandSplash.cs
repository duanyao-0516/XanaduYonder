using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace XanaduYonder.Runtime
{
    // A normal UI image keeps the owner's complete square artwork and its white margins.
    // Unity's built-in splash sizes logos automatically and offers no fixed-size setting.
    [DefaultExecutionOrder(-10000)]
    public sealed class BrandSplash : MonoBehaviour
    {
        [SerializeField] private Texture2D logo;
        private GameObject overlay;
        private RectTransform artwork;
        private CanvasGroup opacity;
        private int lastShortSide;

        public Texture2D Logo { get => logo; set => logo = value; }
        internal bool BlocksInput => overlay != null && overlay.activeInHierarchy;

        private void Awake()
        {
            if (logo == null)
            {
                Debug.LogError("The app launch artwork is missing from the bootstrap scene.");
                enabled = false;
                return;
            }

            overlay = new GameObject("App launch artwork", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasGroup), typeof(GraphicRaycaster));
            // Keep ScreenSpaceOverlay at the scene root per Unity's canvas contract.
            var canvas = overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32760;
            opacity = overlay.GetComponent<CanvasGroup>();

            var background = new GameObject("White background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(overlay.transform, false);
            UiFactory.Stretch(background.GetComponent<RectTransform>());
            background.GetComponent<Image>().color = Color.white;
            background.GetComponent<Image>().raycastTarget = true;

            var image = new GameObject("Complete XY artwork", typeof(RectTransform), typeof(RawImage));
            image.transform.SetParent(overlay.transform, false);
            artwork = image.GetComponent<RectTransform>();
            UiFactory.Center(artwork);
            image.GetComponent<RawImage>().texture = logo;
            image.GetComponent<RawImage>().raycastTarget = false;
            UpdateSize();
        }

        private IEnumerator Start()
        {
            if (overlay == null) yield break;
            // Later canvas creation can leave native drawing order stale after a
            // render-mode change. Reapply once after all Awake calls, before reveal.
            var canvas = overlay.GetComponent<Canvas>();
            canvas.sortingOrder = 32760;
            // Start the visible hold after the first rendered frame, not during scene setup.
            yield return new WaitForEndOfFrame();
            yield return new WaitForSecondsRealtime(1.75f);
            var app = GetComponent<AppController>();
            do
            {
                while (app == null || !app.LobbyReadyForInput) yield return null;
                // Finish rendering the ready lobby underneath the opaque artwork.
                // Recheck after this frame in case focus or input changed meanwhile.
                yield return new WaitForEndOfFrame();
            }
            while (app == null || !app.LobbyReadyForInput);
            // Reveal and unblock together. Destroy alone takes effect later in the frame.
            RemoveOverlay();
            enabled = false;
            Debug.Log("XanaduYonder splash dismissed; lobby input ready, no fade or retained touch blocker.");
        }

        private void Update() { UpdateSize(); }

        private void UpdateSize()
        {
            var shortSide = Mathf.Min(Screen.width, Screen.height);
            if (artwork == null || shortSide == lastShortSide) return;
            lastShortSide = shortSide;
            var width = shortSide * .5f;
            artwork.sizeDelta = new Vector2(width, width * logo.height / logo.width);
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            RemoveOverlay();
        }

        private void OnDestroy() { RemoveOverlay(); }

        private void RemoveOverlay()
        {
            if (overlay == null) return;
            opacity.blocksRaycasts = false;
            opacity.interactable = false;
            overlay.SetActive(false);
            Destroy(overlay);
            overlay = null;
        }
    }
}
