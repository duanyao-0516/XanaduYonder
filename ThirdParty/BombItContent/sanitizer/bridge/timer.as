// Appended to timer symbol 519. Original formatting/update functions remain.
xanaduOriginalTimeInit = timeInit;
xanaduOriginalTimeStop = timeStop;
timeInit = function()
{
    this.xanaduOriginalTimeInit();
    xanaduInitialized = true;
    xanaduRunning = false;
    xanaduPauseStarted = 0;
    _root.xanaduManualPaused = false;
};
function xanaduSetPaused(value)
{
    var next = value == true;
    if(next == pausing) return;
    var now = getTimer() / 1000;
    if(next)
    {
        xanaduPauseStarted = now;
    }
    else
    {
        // Shift only the paused interval. The active fraction since the last
        // 500ms tick remains in now - t0 and is not granted back to the player.
        t0 += now - xanaduPauseStarted;
    }
    pausing = next;
}
timePause = function() { _root.xanaduToggleManualPaused(); };
timeStart = function()
{
    xanaduRunning = true;
    _root.xanaduRestoring = false;
    _root.xanaduRestore = null;
    _root.xanaduRecordCheckpoint(_root.stageNum);
    _root.xanaduApplyPause();
    timerInterval = setInterval(function()
    {
        if(pausing == false)
        {
            var now = getTimer() / 1000;
            t -= now - t0;
            t0 = now;
            if(t < 1)
            {
                t = 0;
                timeStop();
                _parent.game.gameover("timeup");
            }
            upDate();
        }
        // While paused t0 stays fixed; compensation occurs once on resume.
    },500);
};
timeStop = function()
{
    this.xanaduOriginalTimeStop();
    xanaduRunning = false;
    _root.xanaduPublish();
};
