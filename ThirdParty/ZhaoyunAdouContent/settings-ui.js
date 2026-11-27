/* Project the original settings onto touch controls. Preferences and avatar gestures stay game-owned. */
(function () {
  'use strict';
  const I = window.__XanaduZhaoyunInternals;
  let settings = null;
  function element(tag, text, className) {
    const node = document.createElement(tag);
    if (text) node.textContent = text;
    if (className) node.className = className;
    return node;
  }
  function removeSettings() {
    if (!settings) return;
    XanaduDialogs.reset(settings);
    settings.remove();
    settings = null;
  }
  function showSettings(scene) {
    removeSettings();
    const layer = element('div', '', 'xy-dialog xy-sheet xanadu-local-panel xanadu-settings');
    const card = element('section');
    card.setAttribute('role', 'dialog');
    card.setAttribute('aria-modal', 'true');
    card.setAttribute('aria-label', '游戏设置');
    card.append(element('h2', '游戏设置'));
    const options = element('div', '', 'xanadu-setting-options');
    card.append(options);
    options.append(element('h3', '显示'));
    function toggle(text, key, original) {
      const row = element('label', '', 'xy-switch');
      const control = element('input', '', 'xy-switch-input');
      control.type = 'checkbox';
      control.setAttribute('role', 'switch');
      control.checked = scene.settingData[key];
      control.onchange = () => {
        original.event(Laya.Event.CLICK, new Laya.Event());
        control.checked = scene.settingData[key];
      };
      row.append(element('span', text, 'xy-switch-label'), control);
      options.append(row);
    }
    toggle('显示伤害数字', 'showDamageNum', scene.checkBox);
    toggle('流畅模式', 'iosBetterMode', scene.iosBetterBox);

    options.append(element('h3', '声音'));
    const audio = I.SoundMgr.instance();
    function volume(text, key, apply) {
      const row = element('label', '', 'xanadu-volume');
      const slider = element('input');
      slider.type = 'range';
      slider.min = '0';
      slider.max = '100';
      slider.value = String(Math.round(scene.settingData[key] * 100));
      slider.oninput = () => {
        scene.settingData[key] = slider.valueAsNumber / 100;
        apply.call(audio, scene.settingData[key]);
      };
      slider.onchange = () => I.DataMgr.instance().player.setSettingData(scene.settingData);
      row.append(element('span', text), slider);
      options.append(row);
    }
    volume('音乐', 'musicVolume', audio.setMusicVolume);
    volume('音效', 'soundVolume', audio.setSoundVolume);
    const done = element('button', '完成', 'xy-secondary');
    const dismiss = () => XanaduDialogs.finishSheet(layer, () => {
      scene.closeBtn.event(Laya.Event.CLICK, new Laya.Event());
      removeSettings();
    });
    done.onclick = dismiss;
    card.append(done);
    layer.append(card);
    XanaduDialogs.bindBackdrop(layer, dismiss);
    scene.bg.visible = false;
    scene.getChildByName('box').visible = false;
    settings = layer;
    document.body.append(layer);
    done.focus({preventScroll:true});
  }

  // Laya font sizes are stage units. Limit menu copy in display pixels and restore
  // original values when a cached sprite returns to its owner.
  function formatCanvasDialog(root) {
    const original = new Map();
    function layout() {
      const canvas = document.querySelector('canvas');
      const screenScale = canvas.getBoundingClientRect().height / Laya.stage.height;
      if (!screenScale) return;
      const rem = parseFloat(getComputedStyle(document.documentElement).fontSize) || 16;
      function visit(node, scale, action) {
        if (node !== root) scale *= Math.abs(node.scaleY);
        if (!scale) return;
        action = action || /btn|button/i.test(node.name || '');
        if (node instanceof Laya.Text || node instanceof Laya.Label) {
          let saved = original.get(node);
          if (!saved || node.fontSize !== saved.applied) {
            saved = {size:node.fontSize, stroke:node.stroke || 0};
            original.set(node,saved);
          }
          const caption = String(node.text || '');
          const title = /title|weaponName/i.test(node.name || '') || (caption.length <= 12 && saved.size >= 40);
          const secondary = /count|price|rarity|unlock|note/i.test(node.name || '');
          const limit = action ? 14 : secondary ? 12 : title ? 16 : 14;
          saved.applied = Math.min(saved.size, Math.max(1, Math.round(rem * limit / (16 * scale))));
          node.fontSize = saved.applied;
          if (saved.stroke) node.stroke = Math.min(saved.stroke, Math.max(1, Math.round(1 / scale)));
          return;
        }
        for (let i=0; i<node.numChildren; i++) visit(node.getChildAt(i),scale,action);
      }
      visit(root,screenScale,false);
    }
    layout();
    Laya.stage.on(Laya.Event.RESIZE,root,layout);
    root.once(Laya.Event.UNDISPLAY,root,() => {
      Laya.stage.off(Laya.Event.RESIZE,root,layout);
      for (const [node,saved] of original) {
        if (!node.destroyed && node.fontSize === saved.applied) {
          node.fontSize = saved.size;
          node.stroke = saved.stroke;
        }
      }
    });
  }

  // The avatar carousel contains a timed gesture and an unlock sequence. Retain its original canvas controls.
  let avatar = null;
  function removeAvatar() {
    if (!avatar) return;
    Laya.stage.off(Laya.Event.RESIZE, avatar, avatar.layout);
    cancelAnimationFrame(avatar.frame);
    avatar.backdrop.remove();
    while (avatar.box.numChildren) avatar.original.addChild(avatar.box.getChildAt(0));
    avatar.original.visible = avatar.originalVisible;
    avatar.scene.closeBtn.hitArea = avatar.closeHitArea;
    avatar.paper.destroy();
    Laya.stage.screenMode = avatar.screenMode;
    avatar = null;
    Laya.stage.updateCanvasSize();
  }
  function showAvatar(scene) {
    removeAvatar();
    const screenMode = Laya.stage.screenMode;
    // Keep the settings sheet upright in a resized web view. Restore the game orientation on close.
    Laya.stage.screenMode = Laya.Stage.SCREEN_NONE;
    Laya.stage.updateCanvasSize();
    const original = scene.getChildByName('box');
    const box = new Laya.Box();
    box.size(original.width, original.height);
    const frame = original.getChildByName('settingWnd');
    frame.skin = '';
    const title = frame.getChildByName('Text');
    title.color = '#173d35';
    title.stroke = 0;
    const paper = new Laya.Sprite();
    paper.name = 'settingsSurface';
    paper.mouseEnabled = true;
    scene.addChildAt(paper, scene.getChildIndex(original));
    const originalVisible = original.visible, closeHitArea = scene.closeBtn.hitArea;
    while (original.numChildren) box.addChild(original.getChildAt(0));
    original.visible = false;
    const viewport = new Laya.Panel();
    viewport.name = 'avatarOptions';
    paper.addChild(viewport);
    viewport.addChild(box);
    const backdrop = element('div', '', 'xanadu-avatar-backdrop');
    document.body.append(backdrop);
    XanaduDialogs.bindBackdrop(backdrop, () => scene.closeBtn.event(Laya.Event.CLICK, new Laya.Event()));
    const state = {scene, original, originalVisible, closeHitArea, box, paper, backdrop, screenMode,
      progress:0, closing:false, frame:0};
    state.paint = progress => {
      state.progress = progress;
      paper.y = scene.height - progress * paper.height;
      scene.bg.alpha = .4 * progress;
      const canvas = document.querySelector('canvas').getBoundingClientRect();
      const top = paper.localToGlobal(new Laya.Point()).y;
      backdrop.style.height = state.closing ? '100%' : Math.max(0, canvas.top + top * canvas.height / Laya.stage.height) + 'px';
    };
    state.layout = () => {
      const cssScale = Laya.stage.width / window.innerWidth;
      const insets = getComputedStyle(document.body,'::before');
      const gutter = parseFloat(insets.marginLeft) || 16;
      const left = (gutter + parseFloat(insets.paddingLeft)) * cssScale;
      const right = (gutter + parseFloat(insets.paddingRight)) * cssScale;
      const scale = Math.min(scene.width-left-right, 440 * cssScale) / box.width;
      const safe = Math.max(16, parseFloat(insets.paddingBottom)) * cssScale;
      const height = Math.min(scene.height - 36 * cssScale, box.height * scale + safe);
      Laya.Tween.clearAll(box);
      box.centerX = box.centerY = NaN;
      box.anchorX = box.anchorY = 0;
      box.scale(scale, scale);
      box.pos(left+(scene.width-left-right-box.width*scale)/2, 0);
      paper.x = 0;
      paper.size(scene.width, height);
      viewport.size(scene.width, height - safe);
      const scrollType = box.height * scale > viewport.height + 1 ? Laya.ScrollType.Vertical : Laya.ScrollType.None;
      if (viewport.scrollType !== scrollType) viewport.scrollType = scrollType;
      viewport.refresh();
      const canvas = document.querySelector('canvas').getBoundingClientRect();
      state.paint(state.progress);
      const target = 44 * Laya.stage.height / canvas.height / scale;
      const close = scene.closeBtn;
      close.hitArea = new Laya.Rectangle((close.width-target)/2,(close.height-target)/2,target,target);
      paper.graphics.clear();
      paper.graphics.drawRoundRect(0, 0, scene.width, height, 20*cssScale, 20*cssScale, 0, 0, '#f8f7f1');
    };
    avatar = state;
    state.layout();
    Laya.stage.on(Laya.Event.RESIZE, state, state.layout);
    formatCanvasDialog(paper);
    animateAvatar(state, 1);
  }
  function animateAvatar(state, target, done) {
    cancelAnimationFrame(state.frame);
    const start = performance.now(), from = state.progress;
    const duration = XanaduDialogs.durationMs();
    function step(now) {
      if (avatar !== state) return;
      const t = duration ? Math.min(1, (now - start) / duration) : 1;
      const eased = state.closing ? t * t : 1 - (1 - t) ** 3;
      state.paint(from + (target - from) * eased);
      if (t < 1) state.frame = requestAnimationFrame(step);
      else if (done) done();
    }
    step(start);
  }
  function dismissAvatar(done) {
    const state = avatar;
    if (!state) return done();
    if (state.closing) return;
    state.closing = true;
    state.box.mouseEnabled = false;
    animateAvatar(state, 0, () => {
      state.box.mouseEnabled = true;
      done();
    });
  }
  function back() {
    if (window.__XanaduDismissCanvasCard()) return true;
    if (!avatar || !avatar.scene.parent) return false;
    avatar.scene.closeBtn.event(Laya.Event.CLICK, new Laya.Event());
    return true;
  }
  window.XanaduGameSettings = Object.freeze({showSettings,removeSettings,showAvatar,removeAvatar,dismissAvatar,back,formatCanvasDialog});
})();
