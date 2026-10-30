using System;
using UnityEngine;

namespace XanaduYonder.Runtime.AgeOfWar
{
    internal sealed partial class AgeOfWarPage
    {
        private AgeOfWarLoadResult menuSave;
        private AgeOfWarMenuDialog menuDialog;
        private Func<bool> hapticsEnabled=()=>false;
        private Action<bool> setHaptics=_=>{};
        private int suppressMenuInputUntilFrame=-1;

        private void ShowMainMenu()
        {
            view.ShowScene("menu");RefreshMenuProgress();
            // Select a different clip from the battlefield so Continue can start battle music again.
            audioPlayer.SelectMusic(edition==2?"music/2":null);
        }

        private void RefreshMenuProgress()
        {
            menuSave=saveStore==null||clearPending?null:saveStore.Load();
            view.SetContinueAvailable(menuSave!=null&&menuSave.CanWrite&&menuSave.HasBattle&&!menuSave.Battle.Capture().Ended);
            if(clearPending)return;
            if(menuSave!=null&&!menuSave.CanWrite)view.SaveWarning(menuSave.Warning,true);
            else view.SaveWarning(null);
        }

        private bool CanStartNewBattle()
        {
            if(clearPending&&!PersistProgress())return false;
            // Choosing a difficulty is the point at which a new run may replace the old slot.
            // Merely opening the title/difficulty menu never changes that slot.
            if(saveStore==null)return true;
            RefreshMenuProgress();
            return menuSave!=null&&menuSave.CanWrite;
        }

        private void ContinueBattle()
        {
            if(battle!=null||view.SceneKey!="menu")return;
            if(clearPending&&!PersistProgress())return;
            // Re-read the authoritative file instead of reviving an earlier menu snapshot.
            RefreshMenuProgress();
            if(menuSave==null||!menuSave.CanWrite||!menuSave.HasBattle||menuSave.Battle.Capture().Ended)return;
            CancelPointer();view.BeginTransition(true);
            var saved=menuSave;menuSave=null;RestoreBattle(saved);
        }

        private void RetryProgress()
        {
            if((battle!=null||clearPending)&&!PersistProgress())return;
            if(battle==null)RefreshMenuProgress();
        }

        private void OpenMenuDialog(bool help)
        {
            if(battle!=null||view.SceneKey!="menu"||view.Transitioning||menuDialog==null)return;
            CancelPointer();view.HidePointer();stageClock.Reset();
            if(help)menuDialog.ShowHelp();else menuDialog.ShowSettings();
        }

        private void MenuDialogClosed()
        {
            CancelPointer();view?.HidePointer();stageClock.Reset();
            // UGUI may close during its Update before this page observes the same release.
            suppressMenuInputUntilFrame=Time.frameCount+1;
            audioPlayer?.Refresh();
        }
    }
}
