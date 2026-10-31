using System;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Runtime.AircraftFire;

namespace XanaduYonder.Runtime
{
    public sealed partial class AppController
    {
        private AircraftFirePage aircraftFirePage;
        private long aircraftFireBest;
        private string aircraftFireLoadWarning;

        private void LoadAircraftFireRecord()
        {
            aircraftFireBest = new AircraftFireSaveStore(Application.persistentDataPath)
                .Load(out aircraftFireLoadWarning).BestScore;
        }

        private static void AddAircraftFirePreview(Transform parent)
        {
            var maskImage = UiFactory.Panel(parent, "Aircraft Fire logo mask", 0, 0, 96, 96, Color.white);
            maskImage.pixelsPerUnitMultiplier = 16f / 18f;
            var mask = maskImage.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            var artwork = UiFactory.Rect(maskImage.transform, "Aircraft Fire original launcher icon", 0, 0, 96, 96)
                .gameObject.AddComponent<RawImage>();
            artwork.texture = Resources.Load<Texture2D>("AircraftFire/Logo");
            artwork.raycastTarget = false;
        }

        private void EnterAircraftFire()
        {
            if (activePage != Page.Lobby || paused || !focused || modal != null || IsKingdomRushRunning) return;
            StopAnimation();
            CloseModal();
            Screen.orientation = ScreenOrientation.Portrait;
            RectTransform createdRoot = null;
            try
            {
                if (aircraftFirePage == null)
                {
                    // The battle fills the canvas; the page applies the safe area to its HUD only.
                    createdRoot = UiFactory.Rect(canvasRoot, "Aircraft Fire page", 0, 0, 720, 1320);
                    UiFactory.Stretch(createdRoot);
                    Canvas.ForceUpdateCanvases();
                    aircraftFirePage = createdRoot.gameObject.AddComponent<AircraftFirePage>();
                    aircraftFirePage.Initialize(createdRoot, ShowLobby, after => OpenSettings(after),
                        () => activePage == Page.AircraftFire && modal == null && !paused && focused,
                        () => data.SoundEnabled, () => data.HapticsEnabled, feedback);
                    aircraftFirePage.Changed += RefreshAircraftFireRecord;
                }
                activePage = Page.AircraftFire;
                lobbyPage.SetActive(false);
                gamePage.SetActive(false);
                frame.gameObject.SetActive(false);
                aircraftFirePage.SetForeground(!paused && focused);
                aircraftFirePage.SetHostSaveWarning(saveFailed ? saveWarning : null);
                aircraftFirePage.Enter();
                RefreshAircraftFireRecord();
            }
            catch (Exception error)
            {
                if (aircraftFirePage != null) aircraftFirePage.Changed -= RefreshAircraftFireRecord;
                if (createdRoot != null) { createdRoot.gameObject.SetActive(false); Destroy(createdRoot.gameObject); }
                else if (aircraftFirePage != null) { aircraftFirePage.gameObject.SetActive(false); Destroy(aircraftFirePage.gameObject); }
                aircraftFirePage = null;
                ShowLobby();
                SetStatus("飞机大战暂时无法打开，请重试。");
                Debug.LogException(error);
            }
        }

        private void LeaveAircraftFire()
        {
            aircraftFirePage?.Leave();
            if (activePage == Page.AircraftFire && frame != null) frame.gameObject.SetActive(true);
        }

        private void RefreshAircraftFireRecord()
        {
            if (lobbyView == null) return;
            if (aircraftFirePage != null)
            {
                aircraftFireBest = Math.Max(aircraftFireBest, aircraftFirePage.BestScore);
                aircraftFireLoadWarning = aircraftFirePage.SaveError;
            }
            string warning = aircraftFireLoadWarning == null ? "" : " · 未保存";
            lobbyView.SetRecord("aircraft-fire", "最高纪录 " + aircraftFireBest + warning,
                (aircraftFireLoadWarning == null ? "" : "未保存 · ") + "最高 " + aircraftFireBest + " 分");
        }

        private void ReleaseAircraftFire()
        {
            if (aircraftFirePage == null) return;
            aircraftFirePage.Changed -= RefreshAircraftFireRecord;
            aircraftFirePage.Suspend();
            aircraftFirePage.Save();
        }
    }
}
