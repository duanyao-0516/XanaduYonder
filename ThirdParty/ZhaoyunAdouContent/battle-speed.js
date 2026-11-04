/* Battle-only clock for the pinned Laya release. System timers and input stay on real time. */
(function () {
  'use strict';
  const I = window.__XanaduZhaoyunInternals;
  let active = false;
  let rate = 1;
  // A zero Aw means the unit can attack immediately. Keep the same positive epoch
  // for the lifetime of the original unit pool, including across battle scenes.
  const attackEpoch = Date.now();
  let normalFrame = null;
  let normalSpineUpdate = null;

  function attackNow() {
    const time = I.TimeMgr.instance();
    // R9 runs inside TimeMgr.update, before that method adds the current delta to pr.
    return attackEpoch + time.pr + time.delta;
  }

  function fastFrame(timestamp) {
    const gameTimer = Laya.timer;
    const time = I.TimeMgr.instance();
    const elapsed = timestamp - gameTimer._lastTimer;
    if (time.ur) {
      // PauseDialog keeps UI timers running. Discard that elapsed time from the next combat step.
      normalFrame.call(this, timestamp);
      if (time.ur) time.gr = gameTimer.currTimer;
      return;
    }
    if (gameTimer.scale !== 1 || !(elapsed > 0)) {
      return normalFrame.call(this, timestamp);
    }

    // Same ordering as Stage._updateTimers in the pinned engine. Only the gameplay
    // timer and its tweens get extra updates. Give projectiles small collision steps
    // and discard long stalls instead of running an accumulated backlog on resume.
    const frameRate = rate;
    const realMs = Math.min(elapsed, 64);
    const gameMs = realMs * frameRate;
    const steps = Math.max(frameRate, Math.ceil(gameMs / 20));
    const stepMs = gameMs / steps;
    Laya.systemTimer._update(timestamp);
    Laya.physicsTimer._update(timestamp);
    let advanced = 0;
    for (let index = 0; index < steps && rate === frameRate && !time.ur; index++) {
      // Keep _lastTimer in the real clock domain: timer.once uses it to schedule new callbacks.
      gameTimer._lastTimer = timestamp - stepMs;
      gameTimer._update(timestamp);
      advanced += gameTimer.delta;
      gameTimer.unscaledDelta = realMs / steps;
      Laya.Tweener._runAll();
    }
    gameTimer._lastTimer = timestamp;
    gameTimer.delta = advanced; // rendered components (including Spine) update once per display frame
    gameTimer.unscaledDelta = realMs;
  }

  function stopFastFrames() {
    rate = 1;
    if (normalFrame) {
      Laya.stage._updateTimers = normalFrame;
      Laya.Spine2DRenderNode.prototype.onUpdate = normalSpineUpdate;
      normalFrame = null;
      normalSpineUpdate = null;
    }
  }
  function updateFastSpine() {
    // Spine consumes the accumulated delta once per render. Keep its normal
    // real-frame limit (64 ms by default) while advancing the clip at the selected speed.
    const keeper = this._timeKeeper; // not created until this node's Spine template has loaded
    if (!this._needUpdate || !keeper || I.TimeMgr.instance().ur) return normalSpineUpdate.call(this);
    const limit = keeper.maxDelta;
    keeper.maxDelta = limit * rate;
    try { return normalSpineUpdate.call(this); }
    finally { keeper.maxDelta = limit; } // a clip event can reset or replace this node's keeper
  }
  function end() {
    stopFastFrames();
    active = false;
  }
  function begin() {
    end();
    active = true;
  }
  function toggle() {
    const status = window.XanaduZhaoyun.status();
    if (!active || !status.active || !status.battle || status.paused) return rate;
    if (rate === 3) {
      stopFastFrames();
      return rate;
    }
    if (rate === 1) {
      normalFrame = Laya.stage._updateTimers;
      normalSpineUpdate = Laya.Spine2DRenderNode.prototype.onUpdate;
      Laya.stage._updateTimers = fastFrame;
      Laya.Spine2DRenderNode.prototype.onUpdate = updateFastSpine;
    }
    rate += 1;
    return rate;
  }
  const originalGameOver = I.GameMgr.prototype.gameOver;
  I.GameMgr.prototype.gameOver = function (...args) {
    end(); // settlement and its animations use standard time, even if the battle is still displayed
    return originalGameOver.apply(this, args);
  };
  window.XanaduBattleSpeed = Object.freeze({begin, end, toggle, attackNow});
})();
