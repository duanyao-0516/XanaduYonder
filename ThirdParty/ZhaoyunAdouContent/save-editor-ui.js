/* Local save editor. The main scene owns the entry button and closes this panel on exit. */
(function () {
  'use strict';
  let overlay = null;

  function element(tag, text, className) {
    const node = document.createElement(tag);
    if (text !== undefined) node.textContent = text;
    if (className) node.className = className;
    return node;
  }

  function button(text) {
    const node = element('button', text);
    node.type = 'button';
    return node;
  }

  function field(parent, title, control) {
    const label = element('label', undefined, 'save-editor-field');
    label.append(element('span', title), control);
    parent.append(label);
    return label;
  }

  function numberInput(title, parent, value) {
    const input = element('input');
    input.type = 'text';
    input.inputMode = 'numeric';
    input.autocomplete = 'off';
    input.value = String(value);
    field(parent, title, input);
    return input;
  }

  function integer(input, min, max, title) {
    const text = input.value.trim();
    const value = Number(text);
    if (!/^\d+$/.test(text) || !Number.isSafeInteger(value) || value < min || value > max) {
      throw new Error(title + '请输入 ' + min + ' 至 ' + max + ' 的整数');
    }
    return value;
  }

  function currentMain() {
    const status = window.XanaduZhaoyun?.status();
    if (!status?.ready || status.scene !== 'MainScene' || status.paused) return null;
    const manager = window.__XanaduZhaoyunInternals.SceneMgr.instance();
    const main = manager.co('MainScene');
    if (!main?.parent || main.destroyed || !main.visible) return null;
    // Main is cached and can remain underneath a shop, avatar or weapons scene.
    const root = main.parent;
    for (let i = root.numChildren - 1; i > root.getChildIndex(main); i--) {
      const scene = root.getChildAt(i);
      if (scene.visible && scene instanceof Laya.Scene) return null;
    }
    for (const dialog of manager.ho.values()) {
      if (dialog?.parent && dialog.visible && !dialog.destroyed) return null;
    }
    return document.querySelector('.xanadu-local-panel') ? null : main;
  }

  function close() {
    overlay?.remove();
    overlay = null;
  }

  function open() {
    if (overlay?.isConnected) return true;
    if (!currentMain()) return false;
    const data = window.XanaduSaveEditorData;
    let catalog;
    try {
      catalog = data.catalog();
    } catch (error) {
      console.warn('无法打开存档管理：', error.message || error);
      return false;
    }
    const screen = element('div', undefined, 'xanadu-save-editor');
    const panel = element('div', undefined, 'save-editor-card');
    panel.setAttribute('role', 'dialog');
    panel.setAttribute('aria-modal', 'true');
    panel.setAttribute('aria-labelledby', 'xanadu-save-editor-title');
    const header = element('header');
    const title = element('h2', '存档管理');
    title.id = 'xanadu-save-editor-title';
    const back = button('返回游戏');
    back.addEventListener('click', close);
    header.append(title, back);
    const nav = element('nav');
    nav.setAttribute('aria-label', '存档分类');
    const body = element('div', undefined, 'save-editor-body');
    const message = element('p', undefined, 'save-editor-message');
    message.setAttribute('role', 'status');
    message.setAttribute('aria-live', 'polite');
    panel.append(header, nav, body, message);
    screen.append(panel);
    overlay = screen;
    document.body.append(screen);
    back.focus({preventScroll:true});

    function feedback(text, error = false) {
      message.textContent = text;
      message.classList.toggle('save-editor-error', error);
    }

    const tabs = [];
    function section(title) {
      const choice = button(title);
      const page = element('section');
      page.setAttribute('aria-label', title);
      tabs.push({choice, page});
      nav.append(choice);
      body.append(page);
      choice.setAttribute('aria-pressed', String(tabs.length === 1));
      page.hidden = tabs.length !== 1;
      choice.addEventListener('click', () => {
        if (!page.hidden) return;
        for (const tab of tabs) {
          tab.page.hidden = tab.page !== page;
          tab.choice.setAttribute('aria-pressed', String(tab.page === page));
        }
        body.scrollTop = 0;
        feedback('');
      });
      return page;
    }

    let lastSubmit = -Infinity;
    function action(page, text, payload, success) {
      const submit = button(text);
      submit.className = 'save-editor-submit';
      page.append(submit);
      submit.addEventListener('click', () => {
        const now = performance.now();
        if (now - lastSubmit < 450) return;
        lastSubmit = now;
        let command;
        try {
          command = payload();
          const result = data.apply(command);
          if (!result?.ok) throw new Error(result?.error || '操作未完成');
          if (result.warning) {
            submit.disabled = true;
            feedback(result.warning, true);
            return;
          }
        } catch (error) {
          feedback('未能' + text + '：' + (error.message || error), true);
          return;
        }
        try {
          catalog = data.catalog();
          refresh(command.kind);
          feedback(success(command));
        } catch (error) {
          submit.disabled = true;
          feedback('已保存，读取最新数值失败：' + (error.message || error), true);
        }
      });
      return submit;
    }

    const settings = section('设置');
    const gold = numberInput('金币', settings, catalog.gold);
    const keepSkills = element('input');
    keepSkills.type = 'checkbox';
    keepSkills.checked = catalog.keepSkills;
    field(settings, '跨天保留技能道具', keepSkills).classList.add('save-editor-check');
    settings.append(element('p', '开启后，次日继续保留已装备的主动、被动道具和等级。'));
    action(settings, '保存设置', () => ({
      kind:'settings', gold:integer(gold, 0, 99999999, '金币'), keepSkills:keepSkills.checked
    }), () => '已保存：金币 ' + catalog.gold + '，跨天保留已' + (catalog.keepSkills ? '开启' : '关闭') + '。');

    const props = section('道具');
    const propChoice = window.XanaduPanelSelect.create(props, '选择道具');
    const propInfo = element('p');
    props.append(propInfo);
    const level = window.XanaduPanelSelect.create(props, '道具等级');
    const replacement = window.XanaduPanelSelect.create(props, '替换道具');
    const replaceField = replacement.element;
    const selectedProp = () => catalog.props.find(item => String(item.id) === propChoice.value);
    function propFields(preferredLevel) {
      const item = selectedProp();
      const active = item.kind === 'active';
      const limit = active ? 2 : 6;
      const held = catalog.props.filter(other => other.kind === item.kind && other.level > 0);
      const full = item.level === 0 && held.length >= limit;
      propInfo.textContent = (active ? '主动' : '被动') + '道具已装备 ' + held.length + '/' + limit +
        '。' + (item.level ? '当前为 ' + item.level + ' 级。' : '该道具尚未装备。') +
        (full ? '请选择要替换的同类道具。' : '');
      const levels = Array.from({length:item.maxLevel}, (_, i) => [i + 1, (i + 1) + ' 级']);
      level.setOptions(levels, preferredLevel ?? (item.level || 1));
      replaceField.hidden = !full;
      if (full) replacement.setOptions([['', '请选择要替换的道具'], ...held.map(other => [other.id, other.name + ' · ' + other.level + ' 级'])], '');
      else replacement.setOptions([]);
    }
    propChoice.element.addEventListener('change', () => { propFields(); feedback(''); });
    action(props, '保存道具', () => {
      const item = selectedProp();
      const command = {kind:'prop', id:item.id, level:Number(level.value)};
      if (!replaceField.hidden) {
        if (!replacement.value) throw new Error('同类已满，请选择要替换的道具');
        command.replaceId = Number(replacement.value);
      }
      return command;
    }, () => '已装备' + selectedProp().name + '，当前 ' + selectedProp().level + ' 级。');

    const weapons = section('武器');
    const weaponChoice = window.XanaduPanelSelect.create(weapons, '选择武器');
    const weaponInfo = element('p');
    weapons.append(weaponInfo);
    const quantity = numberInput('整把数量', weapons, 1);
    const selectedWeapon = () => catalog.weapons.find(item => String(item.id) === weaponChoice.value);
    function weaponFields() {
      weaponInfo.textContent = '当前拥有 ' + selectedWeapon().count + ' 把，每次可以添加 1 至 99 把。';
    }
    weaponChoice.element.addEventListener('change', () => { weaponFields(); feedback(''); });
    action(weapons, '添加武器', () => ({
      kind:'weapon', id:selectedWeapon().id, quantity:integer(quantity, 1, 99, '整把数量')
    }), command => '已添加' + selectedWeapon().name + ' ' + command.quantity + ' 把，现有 ' + selectedWeapon().count + ' 把。');

    const generals = section('武将');
    const generalChoice = window.XanaduPanelSelect.create(generals, '选择武将');
    const generalInfo = element('p');
    generals.append(generalInfo);
    const selectedGeneral = () => catalog.generals.find(item => String(item.id) === generalChoice.value);
    const grant = action(generals, '授予招募资格', () => ({kind:'general', id:selectedGeneral().id}),
      () => '已授予' + selectedGeneral().name + '招募资格。姓名字仍按正常规则抽取。');
    function generalFields() {
      const item = selectedGeneral();
      const ordinary = item.id < 12;
      grant.disabled = ordinary || item.recruitable;
      generalInfo.textContent = ordinary ? '这位普通武将原本就可随机招募。凑齐姓名字后，在战场上摆放合成。' :
        (item.recruitable ? '已有招募资格。' : '可以授予招募资格。') +
        '授予后仍需随机抽到姓名字，并手动摆放合成。';
    }
    generalChoice.element.addEventListener('change', () => { generalFields(); feedback(''); });

    function refresh(kind) {
      if (kind === 'settings') {
        gold.value = String(catalog.gold);
        keepSkills.checked = catalog.keepSkills;
      }
      if (!kind || kind === 'prop') {
        propChoice.setOptions(catalog.props.map(item => [item.id, item.name]), propChoice.value);
        propFields();
      }
      if (!kind || kind === 'weapon') {
        weaponChoice.setOptions(catalog.weapons.map(item => [item.id, item.name]), weaponChoice.value);
        weaponFields();
      }
      if (!kind || kind === 'general') {
        generalChoice.setOptions(catalog.generals.map(item => [item.id, item.name]), generalChoice.value);
        generalFields();
      }
    }
    refresh();
    return true;
  }

  window.XanaduSaveEditorUI = Object.freeze({open, close});
})();
