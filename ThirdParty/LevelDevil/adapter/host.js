/* Local integration for the pinned Level Devil release. No SDK or remote API. */
(function () {
  'use strict';
  const SAVE = 'UnicycleLegend:mySave', BACKUP = SAVE + ':last-good';
  let active = true, soundEnabled = false, visible = !document.hidden;
  let runtime = null, ready = false, exit = null, failed = false;
  let viewportInsets = [0, 0, 0, 0];
  let hallButtonFirstScreenOnly = false, menuPaused = false;
  function send(type, extra) {
    const value = JSON.stringify(Object.assign({type}, extra || {}));
    if (window.LevelDevilNative && typeof window.LevelDevilNative.postMessage === 'function') window.LevelDevilNative.postMessage(value);
    else if (window === window.top) window.prompt('xanadu-level-devil:' + value, '');
  }
  function saveNotice(message) {
    let notice = document.getElementById('save-status');
    if (!notice && document.body) { notice = document.createElement('div'); notice.id = 'save-status'; notice.setAttribute('role','status'); document.body.appendChild(notice); }
    if (notice) { notice.textContent = message || ''; notice.hidden = !message; }
  }
  function error(message, recoverable) { saveNotice(message); send('error', {message, recoverable:recoverable === true}); }
  window.LevelDevilHost = Object.freeze({
    configureFeedback(value) { window.__levelDevilMenu.configure(value); },
    feedbackSettingsResult(value) { window.__levelDevilMenu.result(value); },
    setHostState(value, sound) {
      active = value === true; soundEnabled = sound === true;
      if (runtime) runtime.stateChanged();
    },
    setViewportInsets(left, top, right, bottom) {
      const values = [left, top, right, bottom];
      if (values.some(value => typeof value !== 'number' || !Number.isFinite(value) || value < 0)) return;
      viewportInsets = values;
      ["left","top","right","bottom"].forEach((side,index)=>document.documentElement.style.setProperty("--ld-safe-"+side,values[index]+"px"));
      if (runtime) runtime.resize();
    },
    status() {
      return runtime ? runtime.status() : {ready:false, scene:null, canReturnToHall:false};
    },
    setHallButtonFirstScreenOnly(value) {
      if (typeof value !== 'boolean') return false;
      hallButtonFirstScreenOnly = value;
      if (runtime) runtime.refreshHallButton();
      return true;
    },
    releaseInput() { if (runtime) runtime.releaseInput(); },
    requestExit(requestId) {
      if (typeof requestId !== 'string' || !requestId || requestId.length > 128) return;
      if (exit && exit.id === requestId) return;
      window.__levelDevilMenu.close();
      exit = {id:requestId, committing:false};
      if (!runtime || !ready || failed) { send('exit-error', {requestId, message:'游戏尚未就绪，不能确认保存。'}); return; }
      runtime.releaseInput(); runtime.tryExit();
    },
    cancelExit() { exit = null; if (runtime) { runtime.cancelHallExit(); runtime.stateChanged(); } }
  });
  window.addEventListener('resize', () => { if (runtime) runtime.resize(); });
  document.addEventListener('visibilitychange', () => { visible = !document.hidden; if (runtime) runtime.stateChanged(); });
  window.addEventListener('blur', () => { if (runtime) runtime.releaseInput(); });
  // Capture at the DOM boundary too: timers may finish a checkpoint while exit
  // waits, but they must not receive new player input during that transaction.
  for (const type of ['keydown','keyup','mousedown','mouseup','mousemove','touchstart','touchmove','touchend','touchcancel','pointerdown','pointerup']) {
    window.addEventListener(type, event => {
      if (event.target instanceof Element && event.target.closest("#level-menu-ui") && active && visible && !exit && !failed) return;
      if (!active || !visible || exit || failed || menuPaused) {
        // Retire Lime's existing touch identities even while gameplay input is
        // blocked. releaseInput already canceled their game/mouse state; its
        // end guard prevents these cleanup events from acting as menu clicks.
        if (type === 'touchend' || type === 'touchcancel') return;
        event.preventDefault(); event.stopImmediatePropagation();
      }
    }, {capture:true, passive:false});
  }
  window.__installLevelDevil = function (R) {
    const {Engine:E, Script:C, Input:Q, SharedObject:SO, Serializer:S, Unserializer:U, Utils, Timer, classes} = R;
    const completions = new Set(); let transitionCallbacks = 0, skipPending = null;
    let pausedChannels = [], controls = null, lastScene = null;
    let layout = null, background = null, backgroundLayer = null, layoutKey = '', backgroundKey = '';
    const hostRunning = () => active && visible && !failed && !menuPaused;
    function clearMouseLatch() {
      Q.mouseDown = Q.mousePressed = Q.mouseReleased = false;
      if (E.stage) E.stage.__mouseDownLeft = null;
      classes['openfl.events.MouseEvent'].__buttonDown = false;
    }
    function drawBackground() {
      if (!layout || !background) return;
      if (!backgroundLayer) {
        backgroundLayer = new classes['openfl.display.Shape']();
        backgroundLayer.set_name('Offline viewport background');
        E.stage.addChildAt(backgroundLayer, 0);
      }
      // Single-player's light outline controls originally sit on the dark
      // wall color, not its light corridor background. Reuse that exact theme
      // color in the wings so the untouched original sprites remain visible.
      const theme = E.engine.behaviors && C.sceneHasBehavior('Scene_Manager')
        ? ['Color_R','Color_G','Color_B'].map(key=>E.engine.getGameAttribute(key)) : null;
      const key = theme ? theme.join(':') : 'scene-background';
      backgroundKey = key;
      const graphics = backgroundLayer.get_graphics(); graphics.clear();
      if (theme && theme.every(Number.isFinite)) {
        graphics.beginFill((theme[0]<<16)|(theme[1]<<8)|theme[2]);
        graphics.drawRect(0,0,layout.width,layout.height); graphics.endFill();
      } else background.draw(graphics, 0, 0, layout.width, layout.height);
    }
    function resize(force) {
      const engine = E.engine, canvas = document.querySelector('#openfl-content canvas');
      if (!engine || !engine.master || !canvas || !E.stage) return;
      const rect = canvas.getBoundingClientRect(), width = E.stage.stageWidth, height = E.stage.stageHeight;
      if (!(rect.width > 0 && rect.height > 0 && width > 0 && height > 0)) return;
      const key = [width,height,rect.width,rect.height,E.SCALE,...viewportInsets].join(':');
      if (!force && key === layoutKey) return;
      const left = Math.min(viewportInsets[0] * width / rect.width, width / 3);
      const top = Math.min(viewportInsets[1] * height / rect.height, height / 3);
      const right = width - Math.min(viewportInsets[2] * width / rect.width, width / 3);
      const bottom = height - Math.min(viewportInsets[3] * height / rect.height, height / 3);
      const unit = Math.min((right-left)/427, (bottom-top)/240);
      const x = left + (right-left-427*unit)/2, y = top + (bottom-top-240*unit)/2;
      releaseInput(); layoutKey = key;
      layout = {width,height,left,top,right,bottom,unit,cssUnit:unit*rect.width/width,x,y};
      E.screenScaleX = E.screenScaleY = E.unzoomedScaleX = E.unzoomedScaleY = unit / E.SCALE;
      E.screenOffsetX = x; E.screenOffsetY = y;
      engine.root.set_scaleX(E.screenScaleX); engine.root.set_scaleY(E.screenScaleY);
      engine.root.set_x(x); engine.root.set_y(y);
      engine.master.set_scrollRect(new classes['openfl.geom.Rectangle'](0,0,427*E.SCALE,240*E.SCALE));
      drawBackground();
      if (controls) controls.layout();
    }
    const setColorBackground = E.prototype.setColorBackground;
    E.prototype.setColorBackground = function (value) {
      setColorBackground.call(this,value); background = value; drawBackground();
    };
    const begin = E.prototype.begin;
    E.prototype.begin = function () { const value = begin.apply(this,arguments); resize(true); return value; };
    const reloadScreen = E.prototype.reloadScreen;
    E.prototype.reloadScreen = function () { const value = reloadScreen.apply(this,arguments); resize(true); return value; };
    const draw = E.prototype.draw;
    E.prototype.draw = function () {
      resize();
      const theme = this.behaviors && C.sceneHasBehavior('Scene_Manager')
        ? ['Color_R','Color_G','Color_B'].map(key=>this.getGameAttribute(key)).join(':') : 'scene-background';
      if (theme !== backgroundKey) drawBackground();
      if (controls) controls.layout(); return draw.apply(this,arguments);
    };
    function releaseInput() {
      if (controls) controls.releaseInput();
      if (Q._controlMap) for (const control of Object.values(Q._controlMap.h)) {
        // A cancellation is not an input release action (e.g. a menu click).
        control.down = control.pressed = control.released = false; control.pressure = 0;
      }
      if (Q._key) Q._key.fill(false);
      if (Q._controlsToReset) Q._controlsToReset.length = 0;
      for (const name of ['mouseDown','mousePressed','mouseReleased','rightMouseDown','rightMousePressed','rightMouseReleased','middleMouseDown','middleMousePressed','middleMouseReleased','mouseWheel']) Q[name] = false;
      clearMouseLatch();
      if (Q.multiTouchPoints) Q.multiTouchPoints.h = {};
      Q.numTouches = 0;
    }
    function applyAudio() {
      if (window.Howler) window.Howler.mute(!hostRunning() || !soundEnabled || !window.__levelDevilMenu.soundEnabled() || !!(exit && exit.committing));
      const engine = E.engine;
      if (!engine || !engine.channels) return;
      if (!hostRunning() || (exit && exit.committing)) {
        for (const channel of engine.channels) if (channel && channel.currentSound && !channel.paused) {
          pausedChannels.push({channel, clip:channel.currentClip, source:channel.currentSource}); channel.setPause(true);
        }
      } else {
        const suspended = pausedChannels; pausedChannels = [];
        for (const {channel,clip,source} of suspended) if (channel.paused && channel.currentClip === clip && channel.currentSource === source) channel.setPause(false);
      }
    }
    function stateChanged() {
      releaseInput(); if (E.engine) E.engine.lastTime = Timer.getTimer(); applyAudio(); tryExit(); window.__levelDevilMenu.refresh();
    }
    function validate(raw) {
      if (typeof raw !== 'string' || !raw || raw.length > 1000000) throw Error('无效的存档内容');
      const parser = new U(raw), data = parser.unserialize();
      if (parser.pos !== raw.length) throw Error('存档包含无效尾部数据');
      if (!data || typeof data !== 'object' || Array.isArray(data)) throw Error('无效的存档结构');
      const keys = Object.keys(data), schema = window.__levelDevilSaveSchema;
      if (!keys.length) throw Error('空存档');
      for (const key of keys) {
        const type = schema[key], value = data[key];
        // Original costume purchase writes 1 into these otherwise boolean flags.
        if (/^Costume_[ABC]$/.test(key) && (value === 0 || value === 1)) continue;
        if (!type || (type === 'array' ? !Array.isArray(value) || value.length > 10000 || value.some(x => !Number.isFinite(x)) : typeof value !== type || (type === 'number' && !Number.isFinite(value)))) throw Error('存档字段不兼容: ' + key);
      }
      return data;
    }
    function readSave() {
      const raw = localStorage.getItem(SAVE), backup = localStorage.getItem(BACKUP);
      if (raw === null && backup === null) return {};
      try { return validate(raw); } catch (originalError) {
        let recovered;
        try { recovered = validate(backup); } catch (_) { throw Error('存档损坏，已保留原数据。请返回大厅后重试。'); }
        // Never erase the damaged data silently; retain it for explicit recovery.
        if (raw !== null) localStorage.setItem(SAVE + ':damaged', raw);
        localStorage.setItem(SAVE, backup);
        error('已从最近一次有效备份恢复存档。', true);
        return recovered;
      }
    }
    const getLocal = SO.getLocal;
    let shared = null;
    SO.getLocal = function (name, localPath, secure) {
      if (name !== 'mySave' || localPath !== 'UnicycleLegend') return getLocal(name, localPath, secure);
      if (shared) return shared;
      try {
        const data = readSave(); shared = new SO(); shared.data = data; shared.__name = name; shared.__localPath = localPath;
        return shared;
      } catch (cause) { failed = true; error(cause.message); stateChanged(); throw cause; }
    };
    const originalFlush = SO.prototype.flush;
    SO.prototype.flush = function () {
      if (this.__name !== 'mySave' || this.__localPath !== 'UnicycleLegend') return originalFlush.apply(this, arguments);
      try {
        const raw = S.run(this.data); validate(raw);
        const previous = localStorage.getItem(SAVE);
        if (previous !== null) { validate(previous); localStorage.setItem(BACKUP, previous); }
        else localStorage.setItem(BACKUP, raw);
        // setItem atomically replaces the value; no remove-before-write window.
        localStorage.setItem(SAVE, raw);
        if (localStorage.getItem(SAVE) !== raw) throw Error('存档写入校验失败');
        saveNotice(null); return 0;
      } catch (_) { saveNotice('进度尚未保存，请释放空间后重试。'); return 1; }
    };
    Utils.flushSharedObject = function (object, callback) {
      let success = false;
      try { success = object.flush() === 0; } catch (_) {}
      if (callback) callback(success);
      if (!success && !exit) error('存档写入失败，已有存档已保留。请释放空间后重试。', true);
    };
    function tryExit() {
      if (!exit || exit.committing || exit.error || !ready || !hostRunning() || completions.size || transitionCallbacks || skipPending || !E.engine.sceneInitialized || E.engine.isTransitioning()) return;
      const request = exit; request.committing = true; releaseInput(); applyAudio();
      C.saveGame('mySave', success => {
        if (exit !== request) return;
        if (success) send('exit-ready', {requestId:request.id});
        else { request.committing = false; request.error = true; send('exit-error', {requestId:request.id, message:'存档写入失败，未退出。请重试或留在游戏。'}); }
      });
    }
    for (const pair of [['onMouseUp','mouseDown'],['onRightMouseUp','rightMouseDown'],['onMiddleMouseUp','middleMouseDown']]) {
      const original = Q[pair[0]];
      Q[pair[0]] = function(event) { if (Q[pair[1]]) original(event); };
    }
    const touchEnd = Q.onTouchEnd;
    Q.onTouchEnd = function(event) {
      if (Q.multiTouchPoints && Object.prototype.hasOwnProperty.call(Q.multiTouchPoints.h, String(event.touchPointID))) touchEnd(event);
    };
    const html5Window = classes['lime._internal.backend.html5.HTML5Window'].prototype;
    const handleTouchEvent = html5Window.handleTouchEvent;
    html5Window.handleTouchEvent = function(event) {
      // Lime maps primary touch DOWN/UP to mouse DOWN/UP, but CANCEL omits
      // mouse UP. Remember the primary ID before Lime clears it; a secondary
      // cancellation must leave the primary mouse state and other keys alone.
      const primary = this.primaryTouch;
      const canceledPrimary = event.type === 'touchcancel' && primary &&
        Array.from(event.changedTouches).some(touch => touch.identifier === primary.id);
      const result = handleTouchEvent.call(this,event);
      if (canceledPrimary) clearMouseLatch();
      return result;
    };
    const cleanup = E.prototype.cleanup;
    E.prototype.cleanup = function () {
      // Engine.cleanup discards this scene's TimedTask list. A user-triggered
      // retry/menu transition can cancel the 2,010 ms save or 500 ms skip task;
      // neither canceled action is a completed level and neither may block all
      // future exits. Release before the engine destroys the button actors.
      releaseInput();
      for (const token of completions) if (token.scene === this.scene) completions.delete(token);
      if (skipPending === this.scene) skipPending = null;
      cleanup.apply(this, arguments);
    };
    const update = E.prototype.onUpdate;
    E.prototype.onUpdate = function (event) {
      if (!hostRunning() || (exit && exit.committing)) { this.lastTime = Timer.getTimer(); return; }
      if (lastScene !== this.scene) { releaseInput(); lastScene = this.scene; window.__levelDevilMenu.refresh(); }
      update.call(this, event);
      if (!ready && this.sceneInitialized && this.scene && this.scene.name === 'Z_Choose_1P_2P' && !this.isTransitioning()) {
        ready = true; window.__levelDevilMenu.engineReady(); applyAudio(); send('ready');
      }
      tryExit(); window.__levelDevilMenu.refresh();
    };
    // Each callback runs asynchronously once. Its existing pause/unpause and
    // scene/reward logic remain in the original game, behind our host tick gate.
    function continueTransition(callback) {
      transitionCallbacks++;
      Promise.resolve().then(() => { try { if (callback) callback(); } finally { transitionCallbacks--; applyAudio(); tryExit(); } });
    }
    function reward(intent, callback) {
      if (intent !== 'speedrun-unlock' && intent !== 'optional-skip') throw Error('Unknown local reward intent');
      if (intent === 'optional-skip') skipPending = E.engine.scene;
      continueTransition(() => callback(true));
    }
    function beginCompletion() { const token = {scene:E.engine.scene}; completions.add(token); skipPending = null; return token; }
    function finishCompletion(token, success) {
      completions.delete(token);
      if (!success && exit) { exit.error = true; send('exit-error', {requestId:exit.id,message:'通关存档写入失败，未退出。请重试。'}); }
      // The original completion has now applied its progress fields. A retry
      // can safely persist them even if that first flush failed.
      if (success) tryExit();
    }
    function status() {
      const engine = E.engine, scene = engine && engine.scene;
      const interactive = !!(ready && !failed && !exit && engine && engine.sceneInitialized && !engine.isTransitioning());
      const menuAvailable = interactive && scene && [125,49,174].includes(scene.ID);
      return {ready, scene:scene ? scene.name : null, active:interactive && hostRunning() && !E.paused,
        menuAvailable:!!menuAvailable, modalPaused:menuPaused,
        gameplayActive:!!(interactive && (C.sceneHasBehavior('Scene_Manager') || C.sceneHasBehavior('2P Scene_Manager'))),
        canReturnToHall:!!(menuAvailable && scene.ID === 125)};
    }
    function applyPreferenceSound(enabled,persist) {
      if (!ready || !E.engine) return;
      E.engine.gameAttributes.h.Volume=enabled?100:0; C.setVolumeForAllSounds(enabled?1:0);
      for(const actor of C.getActorsOfType(C.getActorType(753)))if(actor&&!actor.dead&&!actor.recycled)actor.setAnimation(enabled?'100':'0');
      applyAudio();
      if(persist)C.saveGame('mySave',()=>{});
    }
    // This actor is the original Volume toggle. The native ACK is the one
    // authority; it updates both this saved engine field and actual audio gates.
    classes['scripts.ActorEvents_753'].prototype._event_Updating=function(){
      if(this.wrapper.enabled && hostRunning() && !exit && this.actor.isMousePressed())window.__levelDevilMenu.originalSound();
    };
    window.__levelDevilMenu.attach({send,status,sound:applyPreferenceSound,modal(value){menuPaused=value===true;stateChanged();}});
    runtime = {releaseInput, stateChanged, tryExit, resize, status, refreshHallButton:() => { if (controls) controls.layout(); }, cancelHallExit:() => { if (controls) controls.cancelHallExit(); }};
    if (window.__installLevelDevilControls) controls = window.__installLevelDevilControls({Engine:E,Script:C,classes,blocked:() => !hostRunning() || !!exit,
      viewport:() => layout, haptic:()=>window.__levelDevilMenu.haptic(), returnToChoice:()=>{if(!status().menuAvailable || ![49,174].includes(E.engine.scene.ID))return false;releaseInput();C.switchScene(125,null,C.createCrossfadeTransition(.1));return true;}, hallEnabled:() => !hallButtonFirstScreenOnly || !!(E.engine && E.engine.scene && E.engine.scene.ID === 125), hallExit:() => { if (ready && hostRunning() && !exit) { releaseInput(); send('exit-intent'); return true; } return false; }});
    applyAudio();
    return {continueTransition, reward, beginCompletion, finishCompletion};
  };
})();
