/* Host lifecycle contract for the pinned, locally bundled game. No native bridge. */
(function (window) {
  "use strict";

  const SAVE_KEY = "xanadu.zhaoyun-adou.player.v1";
  const OVERLAY_ID = "xanadu-zhaoyun-resume";
  const document = window.document;
  const state = {
    ready: false,
    active: false,
    visible: !document.hidden,
    soundEnabled: false,
    hostPaused: false,
    gamePaused: false,
    gameTimerPaused: false,
    awaitingContinue: false,
    exitRequested: false,
    exitSettled: false,
    originalMuted: false,
    error: null,
    saveError: null,
    exitError: null
  };
  const blockedSaves = new WeakMap();
  let internals;
  let originalPause;
  let originalResume;
  let initializationError = null;
  let overlay = null;

  function message(error) {
    return error && error.message ? String(error.message) : String(error);
  }

  function persistPlayerData(player, nextData) {
    try {
      if (blockedSaves.has(player)) throw new Error(blockedSaves.get(player));
      if (!player || !nextData || typeof nextData !== "object" || Array.isArray(nextData)) {
        throw new Error("存档数据尚未就绪");
      }
      player.key = SAVE_KEY;
      const committedData = Object.assign({}, nextData, { _saveTime: Date.now() });
      const value = JSON.stringify(committedData);
      const storage = window.Laya.LocalStorage;
      storage.setItem(SAVE_KEY, value);
      if (storage.getItem(SAVE_KEY) !== value) {
        throw new Error("存档写入后校验失败");
      }
      player._data = committedData;
      state.saveError = null;
      return { ok: true };
    } catch (error) {
      state.saveError = "存档失败：" + message(error);
      return { ok: false, error: state.saveError };
    }
  }

  function savePlayer(player) {
    return persistPlayerData(player, player && player._data).ok;
  }

  function save() {
    return state.ready ? savePlayer(internals.DataMgr.instance().player) : false;
  }

  function installHooks() {
    internals = window.__XanaduZhaoyunInternals;
    if (!internals || !internals.PlayerData || !internals.TimeMgr ||
        !internals.DataMgr || !internals.SceneMgr || !internals.GameMgr) {
      throw new Error("离线宿主桥缺少游戏接口");
    }
    // This entry is exclusively host-driven. Mute before GameInit can play anything.
    state.originalMuted = Boolean(window.Laya.SoundManager.muted);
    window.Laya.SoundManager.muted = true;
    const playerPrototype = internals.PlayerData.prototype;
    const originalInit = playerPrototype.init;
    if (typeof originalInit !== "function" || typeof playerPrototype.setData !== "function") {
      throw new Error("离线存档接口与当前游戏版本不一致");
    }
    playerPrototype.init = function () {
      // The original constructor assigns a generic key. Override before its first read.
      this.key = SAVE_KEY;
      try {
        const value = window.Laya.LocalStorage.getItem(SAVE_KEY);
        if (value !== null && value !== undefined) {
          const data = JSON.parse(value);
          if (!data || typeof data !== "object" || Array.isArray(data)) {
            throw new Error("已有存档格式无效，原数据已保留");
          }
        }
      } catch (error) {
        const detail = "已有存档无法读取，原数据已保留：" + message(error);
        blockedSaves.set(this, detail);
        state.saveError = detail;
      }
      return originalInit.apply(this, arguments);
    };
    // The original method swallowed storage exceptions. Every write now verifies persistence.
    playerPrototype.setData = function () { return savePlayer(this); };

    const timePrototype = internals.TimeMgr.prototype;
    originalPause = timePrototype.pause;
    originalResume = timePrototype.resume;
    if (typeof originalPause !== "function" || typeof originalResume !== "function") {
      throw new Error("离线暂停接口与当前游戏版本不一致");
    }
    timePrototype.pause = function (pauseTimer) {
      state.gamePaused = true;
      state.gameTimerPaused = pauseTimer === undefined ? true : Boolean(pauseTimer);
      return originalPause.call(this, state.hostPaused || state.gameTimerPaused);
    };
    timePrototype.resume = function () {
      state.gamePaused = false;
      state.gameTimerPaused = false;
      return state.hostPaused ? originalPause.call(this, true) : originalResume.call(this);
    };
  }

  function activeScene() {
    if (!state.ready) return "Loading";
    const manager = internals.SceneMgr.instance();
    let current = "MainScene";
    // Scenes are cached after closing. Only attached scenes represent the current screen.
    manager.io.forEach(function (scene, name) {
      if (scene && !scene.destroyed && scene.parent && scene.visible !== false) current = name;
    });
    return String(current);
  }

  function inBattle() {
    if (!state.ready) return false;
    const scene = internals.SceneMgr.instance().co("BattleScene");
    const battle = internals.DataMgr.instance().Qf;
    return Boolean(scene && !scene.destroyed && scene.parent && battle && !battle.wi);
  }

  function effectiveActive() {
    return state.active && state.visible && !state.exitRequested;
  }

  function syncSound() {
    if (state.ready) {
      window.Laya.SoundManager.muted = state.originalMuted || !state.soundEnabled ||
        !effectiveActive() || state.hostPaused;
    }
  }

  function removeOverlay() {
    if (overlay) overlay.remove();
    overlay = null;
  }

  function restoreGamePause() {
    const time = internals.TimeMgr.instance();
    if (!state.gamePaused) {
      originalResume.call(time);
    } else if (state.gameTimerPaused) {
      originalPause.call(time, true);
    } else {
      // PauseDialog freezes gameplay but requires Laya timers for its controls/animation.
      window.Laya.timer.resume();
      originalPause.call(time, false);
    }
  }

  function continueBattle() {
    if (!state.ready || !effectiveActive() || !state.awaitingContinue) return;
    state.awaitingContinue = false;
    state.hostPaused = false;
    removeOverlay();
    restoreGamePause();
    syncSound();
  }

  function showOverlay() {
    if (overlay || !document.body) return;
    overlay = document.createElement("div");
    overlay.id = OVERLAY_ID;
    overlay.setAttribute("role", "dialog");
    overlay.setAttribute("aria-modal", "true");
    overlay.setAttribute("aria-label", "对局已暂停");
    overlay.style.cssText = "position:fixed;inset:0;z-index:2147483647;display:flex;align-items:center;justify-content:center;background:rgba(15,20,29,.82);color:#fff;font-family:system-ui,sans-serif;";
    const panel = document.createElement("div");
    panel.style.cssText = "padding:28px;text-align:center;max-width:320px;";
    const title = document.createElement("p");
    title.textContent = "对局已暂停";
    title.style.cssText = "font-size:24px;font-weight:700;margin:0 0 12px;";
    const detail = document.createElement("p");
    detail.textContent = "点击继续，回到当前对局。";
    detail.style.cssText = "font-size:16px;margin:0 0 24px;line-height:1.5;";
    const button = document.createElement("button");
    button.type = "button";
    button.textContent = "继续游戏";
    button.style.cssText = "font:inherit;font-weight:700;padding:12px 28px;border:0;border-radius:12px;background:#f0c46b;color:#282116;cursor:pointer;";
    button.addEventListener("click", continueBattle);
    panel.appendChild(title);
    panel.appendChild(detail);
    panel.appendChild(button);
    overlay.appendChild(panel);
    document.body.appendChild(overlay);
    button.focus();
  }

  function reconcile(saveOnSuspend) {
    if (!state.ready) return;
    try {
      if (!effectiveActive()) {
        if (!state.hostPaused && inBattle() && !state.exitRequested) state.awaitingContinue = true;
        state.hostPaused = true;
        originalPause.call(internals.TimeMgr.instance(), true);
        removeOverlay();
        if (saveOnSuspend) save();
      } else if (state.hostPaused) {
        if (state.awaitingContinue && inBattle()) {
          showOverlay();
        } else {
          state.awaitingContinue = false;
          state.hostPaused = false;
          removeOverlay();
          restoreGamePause();
        }
      }
    } finally {
      // Storage failure must never leave a hidden game playing sound or advancing.
      syncSound();
    }
  }

  function snapshot() {
    let scene = "Loading";
    let battle = false;
    let canReturnToHall = false;
    let paused = !effectiveActive() || state.hostPaused;
    try {
      scene = activeScene();
      battle = inBattle();
      const menu = state.ready ? internals.SceneMgr.instance().co("MainScene") : null;
      canReturnToHall = state.ready && scene === "MainScene" && !battle && !state.exitRequested &&
        Boolean(menu && menu.parent && !menu.destroyed && menu.visible !== false);
      if (state.ready) paused = paused || Boolean(internals.TimeMgr.instance().ur);
    } catch (error) {
      state.error = message(error);
    }
    return {
      ready: state.ready,
      scene: scene,
      battle: battle,
      canReturnToHall: canReturnToHall,
      paused: paused,
      error: initializationError || state.exitError || state.error || state.saveError,
      active: state.active,
      soundEnabled: state.soundEnabled,
      awaitingContinue: state.awaitingContinue,
      saveKey: SAVE_KEY
    };
  }

  function run(action, reportPauseOnFailure) {
    state.error = null;
    try { action(); } catch (error) { state.error = message(error); }
    const error = initializationError || state.exitError || state.error || state.saveError;
    if (!error) return { ok: true };
    return reportPauseOnFailure ? { ok: false, error: error, paused: snapshot().paused } :
      { ok: false, error: error };
  }

  function settleExit() {
    if (!state.ready || state.exitSettled) return;
    state.exitSettled = true;
    if (inBattle()) {
      const data = internals.DataMgr.instance();
      // Exactly the original PauseDialog confirmation path; no mid-battle snapshot is invented.
      try {
        internals.GameMgr.instance().gameOver(false, true, data.Qf.Rs < 5, true);
      } catch (error) {
        // An interrupted settlement may already have changed progression; never replay it.
        state.exitError = "退出结算失败：" + message(error);
      }
    }
    save();
  }

  window.XanaduZhaoyun = Object.freeze({
    configure: function (options) {
      return run(function () {
        if (!options || typeof options.soundEnabled !== "boolean") {
          throw new Error("soundEnabled 必须是布尔值");
        }
        state.soundEnabled = options.soundEnabled;
        syncSound();
      });
    },
    setActive: function (active, persistOnSuspend) {
      return run(function () {
        if (typeof active !== "boolean") throw new Error("active 必须是布尔值");
        if (persistOnSuspend !== undefined && typeof persistOnSuspend !== "boolean") throw new Error("persistOnSuspend 必须是布尔值");
        state.active = active;
        // A native failure alert pauses without recursively retrying persistence.
        reconcile(!active && persistOnSuspend !== false);
      }, active === false);
    },
    exit: function () {
      return run(function () {
        state.exitRequested = true;
        state.active = false;
        state.awaitingContinue = false;
        removeOverlay();
        try { settleExit(); } finally { reconcile(true); }
      }, true);
    },
    status: snapshot
  });

  window.__XanaduZhaoyunBridgeReady = function () {
    return run(function () {
      if (state.ready) return;
      if (!internals || !originalPause || !originalResume) throw new Error("离线宿主桥未完成初始化");
      const player = internals.DataMgr.instance().player;
      if (!player || player.key !== SAVE_KEY) throw new Error("离线存档命名空间未在初始化前安装");
      state.ready = true;
      if (state.exitRequested) settleExit();
      reconcile(!effectiveActive());
    });
  };

  // Internal adapter transaction: callers construct one candidate for multi-field rewards.
  window.__XanaduZhaoyunPersistPlayerData = persistPlayerData;

  document.addEventListener("visibilitychange", function () {
    run(function () {
      state.visible = !document.hidden;
      reconcile(!state.visible);
    });
  });
  window.addEventListener("pagehide", function () {
    run(function () { state.visible = false; reconcile(true); });
  });
  window.addEventListener("pageshow", function () {
    run(function () { state.visible = !document.hidden; reconcile(!state.visible); });
  });

  try { installHooks(); } catch (error) { initializationError = message(error); }
})(window);
