/* BattleScene owns the launcher. The battle controller owns the pause and the card slots. */
(function () {
  'use strict';
  let panel = null;
  let owner = null;

  function close() {
    panel?.remove();
    panel = null;
    const battle = owner;
    owner = null;
    battle?.close();
  }

  function node(tag, text, className) {
    const element = document.createElement(tag);
    if (text !== undefined) element.textContent = text;
    if (className) element.className = className;
    return element;
  }

  function showResult(text) {
    window.__XanaduZhaoyunInternals?.TipMgr.instance().showTip(text);
  }

  function open() {
    if (panel?.isConnected) return true;
    try {
      const battle = window.XanaduBattleAid;
      const snapshot = battle.open();
      owner = battle;
      let freeSlots = snapshot.freeSlots;
      const generals = snapshot.generals;
      panel = node('div', undefined, 'xanadu-save-editor xanadu-battle-aid');
      const card = node('div', undefined, 'save-editor-card');
      card.setAttribute('role', 'dialog');
      card.setAttribute('aria-modal', 'true');
      card.setAttribute('aria-labelledby', 'xanadu-battle-aid-title');
      const header = node('header');
      const title = node('h2', '战斗援将');
      title.id = 'xanadu-battle-aid-title';
      const back = node('button', '返回战斗');
      back.type = 'button';
      back.addEventListener('click', close);
      header.append(title, back);
      const body = node('div', undefined, 'save-editor-body');
      const label = node('label', undefined, 'save-editor-field');
      const select = node('select');
      for (const general of generals) select.add(new Option(general.name, String(general.id)));
      label.append(node('span', '选择武将'), select);
      const slots = node('p');
      const example = node('p', '例如孙尚香会发入“孙、尚、香”三个字。把字拖上棋盘即可按原规则合成。');
      const send = node('button', '发放姓名字', 'save-editor-submit');
      send.type = 'button';
      const message = node('p', undefined, 'save-editor-message');
      message.setAttribute('role', 'status');
      message.setAttribute('aria-live', 'polite');
      const selected = () => generals.find(item => String(item.id) === select.value);
      function describe() {
        const general = selected();
        const needed = general.words.length;
        slots.textContent = '征召栏有 ' + freeSlots + ' 个空位。' + general.words.join('、') +
          ' 需要 ' + needed + ' 格。' + (needed > freeSlots ? '请先腾出 ' + (needed - freeSlots) + ' 格。' : '');
      }
      select.addEventListener('change', () => {
        describe();
        message.textContent = '';
      });
      send.addEventListener('click', () => {
        const general = selected();
        try {
          const result = battle.grant(general.id);
          if (Number.isInteger(result?.freeSlots)) freeSlots = result.freeSlots;
          if (!result?.ok) {
            describe();
            const missing = general.words.length - freeSlots;
            message.textContent = missing > 0 ? '请先腾出 ' + missing + ' 格，再发放姓名字。' :
              (result?.error || '发放未完成。');
            message.classList.add('save-editor-error');
            return;
          }
          close();
          showResult((result.words || general.words).join('、') + '已发入征召栏。');
        } catch (error) {
          message.textContent = error.message || String(error);
          message.classList.add('save-editor-error');
        }
      });
      describe();
      body.append(label, slots, example, send);
      card.append(header, body, message);
      panel.append(card);
      document.body.append(panel);
      back.focus({preventScroll:true});
      return true;
    } catch (error) {
      close();
      showResult(error.message || String(error));
      return false;
    }
  }

  window.XanaduBattleAidUI = Object.freeze({open, close});
})();
