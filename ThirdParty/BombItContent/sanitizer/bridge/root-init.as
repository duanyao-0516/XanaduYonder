// Appended after the original root frame 1, preserving its initialization.
// Outbound changes use a fixed FSCommand without eval; host pulls current state.
xanaduBridgeVersion = 1;
xanaduBooted = false;
xanaduManualPaused = false;
xanaduHostPaused = false;
xanaduLastPublished = "";
xanaduSaveObject = SharedObject.getLocal("bomb_it");
xanaduSaveOk = true;
xanaduSaveError = null;
xanaduSaveRevision = 0;
xanaduInvalidCurrentCheckpoint = false;
xanaduRestoring = false;
xanaduRestore = null;
xanaduDesiredSound = xanaduSoundEnabled != "0";
function xanaduInteger(value)
{
    return typeof value == "number" && isFinite(value) && value == Math.floor(value) && Math.abs(value) <= 9007199254740991;
}
function xanaduCheckpointValid(value)
{
    if(value == undefined || value.schema != 1 || value.source != "a2b4cc65115e4b6eb813962d80e91f1e65746a91f27e91d5e86fa1963ed5b54d") return false;
    if(value.mode != 1 && value.mode != 2) return false;
    var limit = value.difficulty == "easy" ? 15 : value.difficulty == "normal" ? 20 : value.difficulty == "hard" ? 25 : 0;
    if(!xanaduInteger(value.stage) || value.stage < 1 || value.stage > limit) return false;
    if(!(value.heroes instanceof Array) || value.heroes.length != 4) return false;
    var used = {};
    var living = 0;
    for(var i = 0; i < 4; i++)
    {
        var hero = value.heroes[i];
        if(hero == undefined || !xanaduInteger(hero.heroId) || hero.heroId < 1 || hero.heroId > 4 || used[hero.heroId]) return false;
        if(!xanaduInteger(hero.life) || hero.life < 0 || !xanaduInteger(hero.totalScore)) return false;
        used[hero.heroId] = true;
        if(i < value.mode && hero.life > 0) living++;
    }
    return living > 0;
}
function xanaduContinueInfo()
{
    if(!xanaduCheckpointValid(xanaduCheckpoint)) return null;
    return {mode:xanaduCheckpoint.mode,difficulty:xanaduCheckpoint.difficulty,stage:xanaduCheckpoint.stage,
        heroId1:xanaduCheckpoint.heroes[0].heroId,heroId2:xanaduCheckpoint.mode == 2 ? xanaduCheckpoint.heroes[1].heroId : null,
        totalStages:xanaduCheckpoint.difficulty == "easy" ? 15 : xanaduCheckpoint.difficulty == "normal" ? 20 : 25,
        life1:xanaduCheckpoint.heroes[0].life,life2:xanaduCheckpoint.mode == 2 ? xanaduCheckpoint.heroes[1].life : 0,
        totalScore1:xanaduCheckpoint.heroes[0].totalScore,totalScore2:xanaduCheckpoint.mode == 2 ? xanaduCheckpoint.heroes[1].totalScore : 0};
}
function xanaduWriteSave()
{
    if(xanaduInvalidCurrentCheckpoint) return false;
    xanaduSaveOk = false;
    try
    {
        xanaduSaveOk = xanaduSaveObject.flush() == true;
    }
    catch(error)
    {
        xanaduSaveOk = false;
    }
    xanaduSaveError = xanaduSaveOk ? null : "关卡进度未能写入，请重试保存。";
    return xanaduSaveOk;
}
function xanaduFlushSave()
{
    xanaduWriteSave();
    return xanaduPublish();
}
function xanaduRecordCheckpoint(stage)
{
    var heroes = [];
    for(var i = 1; i <= 4; i++)
    {
        var hero = this["heroObj" + i];
        heroes.push({heroId:hero.heroId,life:hero.life,totalScore:hero.totalScore});
    }
    var record = {schema:1,source:"a2b4cc65115e4b6eb813962d80e91f1e65746a91f27e91d5e86fa1963ed5b54d",mode:totalPlayer,difficulty:varHard,stage:stage,heroes:heroes};
    if(!xanaduCheckpointValid(record))
    {
        xanaduSaveOk = false;
        xanaduSaveError = "关卡进度无效，未覆盖已有存档。";
        xanaduInvalidCurrentCheckpoint = true;
        xanaduPublish();
        return false;
    }
    xanaduInvalidCurrentCheckpoint = false;
    xanaduCheckpoint = record;
    xanaduSaveObject.data.xanaduCheckpoint = record;
    xanaduSaveRevision++;
    xanaduWriteSave();
    xanaduPublish();
    return xanaduSaveOk;
}
function xanaduClearCheckpoint()
{
    xanaduInvalidCurrentCheckpoint = false;
    xanaduCheckpoint = null;
    delete xanaduSaveObject.data.xanaduCheckpoint;
    xanaduSaveRevision++;
    xanaduWriteSave();
    xanaduPublish();
}
function xanaduFinishRound()
{
    var victory = game.youWin.varWin.indexOf("win") >= 0;
    var living = false;
    for(var i = 1; i <= totalPlayer; i++) if(this["heroObj" + i].life > 0) living = true;
    if((victory && stageNum >= totalStage) || !living) xanaduClearCheckpoint();
    else xanaduRecordCheckpoint(victory ? stageNum + 1 : stageNum);
}
function xanaduContinue()
{
    if(!xanaduBooted || _currentframe >= 9 || xanaduRestoring || !xanaduCheckpointValid(xanaduCheckpoint)) return xanaduState();
    xanaduRestore = xanaduCheckpoint;
    xanaduRestoring = true;
    totalPlayer = xanaduRestore.mode;
    varHard = xanaduRestore.difficulty;
    xanaduManualPaused = false;
    gotoAndStop("select");
    return xanaduPublish();
}
function xanaduSetSoundEnabled(value)
{
    var enabled = value == true;
    var changed = soundon != enabled;
    xanaduDesiredSound = enabled;
    soundon = enabled;
    if(changed)
    {
        if(bgMusic_sound != undefined) bgMusic_sound.stop();
        if(game != undefined && game.bgMusic_sound != undefined) game.bgMusic_sound.stop();
        if(enabled)
        {
            if(_currentframe == 9) game.bgMusic_sound.start();
            else if(_currentframe >= 3 && _currentframe <= 8) bgMusic_sound.start();
        }
    }
    soundon_mc.gotoAndStop(enabled ? 1 : 2);
    return xanaduPublish();
}
xanaduCheckpoint = xanaduCheckpointValid(xanaduSaveObject.data.xanaduCheckpoint) ? xanaduSaveObject.data.xanaduCheckpoint : null;
function xanaduState()
{
    var phase = "menu";
    if(!xanaduBooted) phase = "loading";
    else if(_currentframe == 5) phase = "difficulty";
    else if(_currentframe == 8) phase = "select";
    else if(_currentframe == 9)
    {
        phase = timer.xanaduInitialized && !timer.xanaduRunning ? "gameover" : "game";
    }
    else if(_currentframe >= 10) phase = "gameover";
    var remaining = null;
    if(_currentframe == 9 && timer.xanaduInitialized)
    {
        remaining = timer.t;
        if(timer.xanaduRunning)
        {
            var current = timer.pausing ? timer.xanaduPauseStarted : getTimer() / 1000;
            remaining = Math.max(0, remaining - (current - timer.t0));
        }
    }
    return {version:1,session:String(xanaduSession),booted:xanaduBooted,phase:phase,mode:Number(totalPlayer),manualPaused:xanaduManualPaused,hostPaused:xanaduHostPaused,paused:xanaduManualPaused || xanaduHostPaused,remainingTime:remaining,rootFrame:_currentframe,stage:Number(stageNum),continueInfo:xanaduContinueInfo(),saveOk:xanaduSaveOk,saveError:xanaduSaveError,restoring:xanaduRestoring,soundEnabled:soundon != false};
}
function xanaduPublish()
{
    var state = xanaduState();
    var signature = state.phase + ":" + state.mode + ":" + state.manualPaused + ":" + state.hostPaused + ":" + state.stage + ":" + state.booted + ":" + state.soundEnabled + ":" + state.restoring + ":" + xanaduSaveRevision + ":" + state.saveOk + ":" + state.saveError;
    if(signature != xanaduLastPublished)
    {
        xanaduLastPublished = signature;
        fscommand("xanadu-state","");
    }
    return state;
}
function xanaduApplyPause()
{
    var paused = xanaduManualPaused || xanaduHostPaused;
    if(_currentframe == 9 && timer.xanaduRunning)
    {
        timer.xanaduSetPaused(paused);
        if(game.pausing != paused) game.pause();
        // Original pause_mc is a two-frame, stopped timeline: pause / resume.
        pause_mc.gotoAndStop(paused ? 2 : 1);
    }
    return xanaduPublish();
}
function xanaduSetHostPaused(value)
{
    xanaduHostPaused = value == true;
    return xanaduApplyPause();
}
function xanaduSetManualPaused(value)
{
    xanaduManualPaused = value == true;
    return xanaduApplyPause();
}
function xanaduToggleManualPaused()
{
    return xanaduSetManualPaused(!xanaduManualPaused);
}
flash.external.ExternalInterface.addCallback("xanaduState",this,xanaduState);
flash.external.ExternalInterface.addCallback("xanaduContinue",this,xanaduContinue);
flash.external.ExternalInterface.addCallback("xanaduFlushSave",this,xanaduFlushSave);
flash.external.ExternalInterface.addCallback("xanaduSetSoundEnabled",this,xanaduSetSoundEnabled);
flash.external.ExternalInterface.addCallback("xanaduSetHostPaused",this,xanaduSetHostPaused);
flash.external.ExternalInterface.addCallback("xanaduSetManualPaused",this,xanaduSetManualPaused);
// The original root has no onEnterFrame handler. Publish transitions only,
// never a stream of per-frame messages or a second gameplay timer.
this.onEnterFrame = function() { xanaduPublish(); };
xanaduPublish();
