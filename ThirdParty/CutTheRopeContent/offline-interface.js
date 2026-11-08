/* Explicit adapter for the pinned game's GameInterface contract. No portal SDK. */
(function (window) {
  'use strict';
  const native = window.CutTheRopeNative;
  const listeners = Object.create(null);
  const features = new Set(['audio', 'copyright', 'credits', 'pause', 'progress', 'score', 'tutorial', 'visibilitychange', 'home', 'reset', 'force_english']);
  const activePointers = new Map();
  const saveFailures = new Map();
  let hostPaused = false, hidden = !!window.document.hidden, hostMuted = false, gameMuted = false;
  let paused = hidden, muted = false, saveFailure = null, flushGame = null, cancelGameInput = null, readScene = null, ready = false;
  function report(error) {
    const message = String(error && error.message || error);
    if (native && native.reportError) native.reportError(message);
    else window.console.error(message);
  }
  function emit(name, value) {
    for (const listener of (listeners[name] || []).slice()) listener(value);
  }
  function on(name, listener) {
    if (typeof listener !== 'function') throw new TypeError('Listener must be a function');
    (listeners[name] || (listeners[name] = [])).push(listener);
    return () => { listeners[name] = listeners[name].filter(item => item !== listener); };
  }
  function cancelPointers() {
    for (const [pointerId, event] of activePointers) {
      event.target.dispatchEvent(new window.PointerEvent('pointercancel', {
        pointerId, pointerType: event.pointerType, button: event.button,
        clientX: event.clientX, clientY: event.clientY, bubbles: true
      }));
    }
    activePointers.clear();
    if (cancelGameInput) cancelGameInput();
  }
  function updatePause() {
    const next = hostPaused || hidden;
    if (next !== paused) {
      if (next) cancelPointers();
      paused = next;
      emit('pause', paused);
    }
    updateMute();
  }
  function updateMute() {
    const next = hostMuted || gameMuted || paused;
    if (next !== muted) { muted = next; emit('mute', muted); }
  }
  function write(operation, key, value) {
    try {
      if (native) {
        const success = operation === 'set' ? native.writeSave(key, value) : native.removeSave(key);
        if (success !== true) throw new Error('Local save failed: ' + key);
      } else if (operation === 'set') window.localStorage.setItem('cut-the-rope:' + key, value);
      else window.localStorage.removeItem('cut-the-rope:' + key);
      saveFailures.delete(key);
    } catch (error) { saveFailures.set(key, error); report(error); throw error; }
  }
  const storage = {
    getItem(key) {
      if (typeof key !== 'string') throw new TypeError('Save key must be a string');
      try { return native ? native.readSave(key) : window.localStorage.getItem('cut-the-rope:' + key); }
      catch (error) { saveFailure = error; report(error); throw error; }
    },
    setItem(key, value) {
      if (typeof key !== 'string' || typeof value !== 'string') throw new TypeError('Save values must already be serialized strings');
      write('set', key, value);
    },
    removeItem(key) { write('remove', key); }
  };
  function eventPromise(type, detail) {
    window.dispatchEvent(new window.CustomEvent('cut-the-rope:' + type, { detail }));
    return Promise.resolve();
  }
  window.GameInterface = Object.freeze({
    storage,
    hasFeature: feature => features.has(feature),
    isMuted: () => muted,
    isPaused: () => paused,
    getCurrentLanguage: () => 'en',
    getOffsets: () => ({ left: 0, right: 0, top: 0, bottom: 0 }),
    sendPreloadProgress: value => emit('progress', value),
    sendScore: (score, detail) => emit('score', { score, detail }),
    gameReady() {
      if (ready) return;
      ready = true;
      if (native) native.ready();
      eventPromise('ready');
    },
    gameStart: level => eventPromise('start', level),
    gameComplete: () => eventPromise('complete'),
    gameOver: () => eventPromise('game-over'),
    gamePause: () => eventPromise('game-pause'),
    gameResume: () => eventPromise('game-resume'),
    gameQuit: () => eventPromise('game-quit'),
    gameMuted(value) { gameMuted = !!value; updateMute(); },
    onPauseStateChange(listener) { const unsubscribe = on('pause', listener); if (paused) listener(true); return unsubscribe; },
    onMuteStateChange(listener) { const unsubscribe = on('mute', listener); if (muted) listener(true); return unsubscribe; },
    onGoToHome: listener => on('home', listener),
    onGoToNextLevel: listener => on('next-level', listener),
    onGoToLevel: listener => on('level', listener),
    onRestartGame: listener => on('restart', listener),
    onQuitGame: listener => on('quit', listener),
    onGameOver: listener => on('game-over', listener),
    onOffsetChange: listener => on('offset', listener),
    // Original gameplay awaits these continuations even when ads are disabled.
    showInterstitialAd: () => Promise.resolve(),
    showRewardedAd: () => Promise.resolve({ isRewardGranted: false }),
    isRewardedAdAvailable: () => false
  });
  window.CutTheRopeOffline = Object.freeze({
    setHostPaused(value) { hostPaused = !!value; updatePause(); },
    setSoundEnabled(value) { hostMuted = !value; updateMute(); },
    attachGame(flush, cancelInput, sceneStatus) {
      if (flushGame || typeof flush !== 'function') throw new Error('Invalid game persistence registration');
      if (cancelInput != null && typeof cancelInput !== 'function') throw new Error('Invalid input cancellation registration');
      if (sceneStatus != null && typeof sceneStatus !== 'function') throw new Error('Invalid scene status registration');
      flushGame = flush;
      cancelGameInput = cancelInput || null;
      readScene = sceneStatus || null;
    },
    status() {
      const scene = ready && readScene ? readScene() : null;
      return { ready, scene, canReturnToHall: ready && scene === 'FamobiMenuScene' };
    },
    flush() {
      try { if (flushGame) flushGame(); }
      catch (error) { if (![...saveFailures.values()].includes(error)) { report(error); saveFailure = error; } }
      return saveFailure === null && saveFailures.size === 0;
    }
  });
  window.addEventListener('pointerdown', event => activePointers.set(event.pointerId, event), true);
  window.addEventListener('pointermove', event => { if (activePointers.has(event.pointerId)) activePointers.set(event.pointerId, event); }, true);
  for (const name of ['pointerup', 'pointercancel']) window.addEventListener(name, event => activePointers.delete(event.pointerId), true);
  window.document.addEventListener('visibilitychange', () => { hidden = !!window.document.hidden; updatePause(); if (hidden) window.CutTheRopeOffline.flush(); });
  window.addEventListener('pagehide', () => window.CutTheRopeOffline.flush());
  window.addEventListener('error', event => report(event.error || event.message));
  window.addEventListener('unhandledrejection', event => report(event.reason));
})(window);
