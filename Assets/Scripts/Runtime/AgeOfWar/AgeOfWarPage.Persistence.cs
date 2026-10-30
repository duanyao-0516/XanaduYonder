using System;
using UnityEngine;
using XanaduYonder.Core.AgeOfWar;
using XanaduYonder.Core.AgeOfWar.Two;

namespace XanaduYonder.Runtime.AgeOfWar
{
    internal sealed partial class AgeOfWarPage
    {
        private AgeOfWarSaveStore saveStore;
        private bool clearPending;
        private double nextAutoSave;
        private string saveWarning;

        private void RestoreBattle(AgeOfWarLoadResult saved)
        {
            var data=saved.Data;
            battle=saved.Battle;
            // Restoring never advances time or repeats a command. The user explicitly resumes.
            if(!battle.Capture().Paused)battle.Execute(new AgeOfWarCommand(AgeOfWarAction.Pause));
            clock=new AgeOfWarClock(battle.TickRate);stageClock.Reset();
            cameraX=AgeOfWarCameraMath.Clamp(edition,data.CameraX,view.LogicalWidth);cameraY=(float)data.CameraY;
            showDamage=data.ShowDamage;requestedFps=battle.TickRate;
            audioPlayer.MusicVolume=data.MusicVolume;audioPlayer.SoundVolume=data.SoundVolume;
            quality=appliedQuality=data.Quality;
            if(edition==2)
            {
                assets.SetQuality(quality,()=>AgeOfWarImageBinding.RebindAll(stage));
                ((AgeOfWarTwoBattle)battle).SetShowDamage(showDamage);Application.targetFrameRate=requestedFps;
                ResetPauseValues();
            }
            view.CameraX=(float)cameraX;view.CameraY=cameraY;
            view.StartBattle(battle.Capture(),battle);view.Menu(data.Menu);RenderPauseValues();
            audioPlayer.SelectMusic("music/1");nextAutoSave=Time.realtimeSinceStartupAsDouble+1;
            if(!string.IsNullOrEmpty(saved.Warning))
                view.SaveWarning("已恢复最近一次可用的战场备份。",false);
        }

        private void AutoSaveProgress()
        {
            if((battle==null&&!clearPending)||Time.realtimeSinceStartupAsDouble<nextAutoSave)return;
            PersistProgress();
        }

        // All writes run on Unity's thread, so lifecycle callbacks cannot overtake a newer snapshot.
        // Failure leaves the battle in memory; navigation retries instead of destroying unsaved state.
        private bool PersistProgress()
        {
            if(saveStore==null||(battle==null&&!clearPending))return true;
            nextAutoSave=Time.realtimeSinceStartupAsDouble+1;
            try
            {
                if(battle!=null&&battle.Capture().Ended)clearPending=true;
                string error;bool written;
                if(clearPending)
                {
                    written=saveStore.TryClear(out error);
                    if(written)clearPending=false;
                }
                else
                {
                    var data=new AgeOfWarSaveData
                    {
                        Edition=edition,BattleState=battle.SaveState(),CameraX=cameraX,CameraY=cameraY,
                        Menu=view==null?"main":view.MenuMode,Quality=appliedQuality,
                        MusicVolume=audioPlayer==null?1:audioPlayer.MusicVolume,
                        SoundVolume=audioPlayer==null?1:audioPlayer.SoundVolume,
                        FrameRate=battle.TickRate,ShowDamage=showDamage
                    };
                    written=saveStore.TrySave(data,out error);
                }
                if(written)
                {
                    saveWarning=null;view?.SaveWarning(null);
                    if(battle==null&&view!=null&&view.SceneKey=="menu")RefreshMenuProgress();
                    return true;
                }
                ShowSaveFailure(error);return false;
            }
            catch(Exception error)
            {
                ShowSaveFailure(error.Message);return false;
            }
        }
        private void ShowSaveFailure(string detail)
        {
            if(saveWarning!=detail)Debug.LogWarning("Age of War progress was not saved: "+detail);
            saveWarning=detail;
            view?.SaveWarning("进度保存失败，当前战场仍保留在本次游戏中。",true);
        }
    }
}
