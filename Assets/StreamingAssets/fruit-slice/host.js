/* First-party lifecycle and progress boundary for the bundled Fruit Slice game. */
(function (window, document) {
  'use strict';
  let engine, cancelGameInput, stopped = false, nativePaused = false, hidden = false;
  let paused = false, pauseAt = 0, excluded = 0, soundEnabled = true, readySent = false;
  const timers = new Map();
  function now() { return (paused ? pauseAt : Date.now()) - excluded; }
  function cancelInput() {
    if (cancelGameInput) cancelGameInput();
    if (!engine) return;
    // This pinned Laya version routes touchcancel through onMouseUp, which otherwise
    // synthesizes CLICK. Remove pending click records first, then release UI/scroll
    // listeners through the actual TouchManager path before dropping the DOM owner.
    const touch = engine.TouchManager && engine.TouchManager.I;
    if (touch) {
      const left = touch.preDowns.splice(0);
      const right = touch.preRightDowns.splice(0);
      left.forEach(function (record) { touch.onMouseUp(record.tar, record.id, true); });
      right.forEach(function (record) { touch.onMouseUp(record.tar, record.id, false); });
      touch.stageMouseOut();
    }
    if (engine.MouseManager && engine.MouseManager.instance) {
      engine.MouseManager.instance.releaseCapture();
      engine.MouseManager.instance._curTouchID = NaN;
    }
  }
  function apply() {
    if (!engine) return;
    [engine.timer, engine.physicsTimer, engine.updateTimer, engine.lateTimer].forEach(function (timer) {
      if (!timer) return;
      if (paused) {
        if (!timers.has(timer)) timers.set(timer, timer.scale);
        timer.scale = 0;
      } else if (timers.has(timer)) {
        timer._lastTimer = Date.now();
        timer._delta = 0;
        timer.scale = timers.get(timer);
        timers.delete(timer);
      }
    });
    engine.MouseManager.enabled = !paused;
    engine.SoundManager.muted = paused || !soundEnabled;
    if (paused || !soundEnabled) engine.SoundManager.stopAll();
  }
  function reconcile() {
    const next = stopped || nativePaused || hidden;
    if (next !== paused) {
      if (next) { pauseAt = Date.now(); paused = true; cancelInput(); }
      else { excluded += Date.now() - pauseAt; paused = false; }
    }
    apply();
  }
  function reportError(error) {
    const message = String(error && error.message || error).slice(0, 1000);
    if (window.XanaduFruitNative) window.XanaduFruitNative.reportError(message);
  }
  function readSave(text, defaults) {
    let raw = {};
    try { const parsed = JSON.parse(text); if (parsed && !Array.isArray(parsed) && typeof parsed === 'object') raw = parsed; } catch (_) {}
    const result = JSON.parse(JSON.stringify(defaults));
    Object.keys(defaults).forEach(function (key) {
      const value = raw[key], fallback = defaults[key];
      if (Array.isArray(fallback)) {
        if (Array.isArray(value)) result[key] = fallback.map(function (v, i) {
          const n = value[i], max = key === 'guanka' ? 3 : 1;
          return Number.isInteger(n) && n >= 0 && n <= max ? n : v;
        });
      } else if (typeof fallback === 'number') {
        if (typeof value === 'number' && Number.isFinite(value) && value >= 0 && value <= Number.MAX_SAFE_INTEGER)
          result[key] = key === 'yinliang' ? Math.min(1, value) : Math.floor(value);
      } else if (typeof fallback === 'boolean' && typeof value === 'boolean') result[key] = value;
      // zuiJiaTime is unused legacy data, intentionally restored to an empty object.
    });
    // Every blade is available; a legacy ownership array must never revoke an equipped blade.
    if (!Number.isInteger(raw.useDaoId) || raw.useDaoId < 0 || raw.useDaoId >= 27) result.useDaoId = 11;
    if ('guankaIndex' in result) result.guankaIndex = Math.min(49, result.guankaIndex);
    return result;
  }
  window.XanaduFruitHost = Object.freeze({
    setPaused: function (value) { nativePaused = !!value; reconcile(); },
    setSoundEnabled: function (value) { soundEnabled = !!value; apply(); },
    cancelInput: cancelInput,
    shutdown: function () { stopped = true; reconcile(); cancelInput(); }
  });
  window.XanaduFruitContent = Object.freeze({
    now: now, readSave: readSave, reportError: reportError,
    isPaused: function () { return paused; },
    bind: function (value, cancel) {
      engine = value; cancelGameInput = cancel;
      engine.MouseManager.multiTouchEnabled = false;
      if (engine.loader) engine.loader.on(engine.Event.ERROR, null, function (url) {
        reportError('本地资源加载失败：' + url);
      });
      apply();
    },
    ready: function () {
      if (!readySent && !stopped) { readySent = true; if (window.XanaduFruitNative) window.XanaduFruitNative.ready(); }
    },
    once: function (view, key) {
      if (!view._claimed) view._claimed = new Set();
      if (view._claimed.has(key)) return false;
      view._claimed.add(key); return true;
    }
  });
  window.addEventListener('error', function (event) { reportError(event.error || event.message || '资源加载失败'); });
  window.addEventListener('unhandledrejection', function (event) { reportError(event.reason); });
  window.addEventListener('blur', cancelInput);
  document.addEventListener('touchcancel', cancelInput, true);
  document.addEventListener('visibilitychange', function () { hidden = document.hidden; reconcile(); });
})(window, document);
