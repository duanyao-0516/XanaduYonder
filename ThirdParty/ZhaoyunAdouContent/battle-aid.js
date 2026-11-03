/* One-shot battle supplies use the original player rack and never write PlayerData. */
(function () {
  'use strict';
  const I = window.__XanaduZhaoyunInternals;
  const PLAYER = true;
  const RECRUIT_RACK = 3;
  let session = null;

  function currentBattle() {
    const status = window.XanaduZhaoyun.status();
    if (!status.ready || !status.active || !status.battle || status.scene !== 'BattleScene' ||
        status.awaitingContinue || status.error || document.hidden || (status.paused && !session)) {
      throw new Error('请在进行中的对局使用援将。');
    }
    const manager = I.SceneMgr.instance();
    for (const dialog of manager.ho.values()) {
      if (dialog?.parent && dialog.visible && !dialog.destroyed) throw new Error('请先关闭游戏内的弹窗。');
    }
    return manager.co('BattleScene');
  }

  function freeSlots(rack) {
    const slots = I.SlotsMgr.instance();
    let count = 0;
    for (let i = 0; i < rack.size; i++) if (slots.vB(PLAYER, i)) count++;
    return count;
  }

  function open() {
    if (session) throw new Error('援将面板已经打开。');
    const scene = currentBattle();
    const rack = I.ContainersMgr.instance().bP(RECRUIT_RACK, PLAYER);
    const generals = I.DataMgr.instance().km.generalType.map((name, id) => ({
      id, name, words: Array.from(name)
    }));
    const snapshot = {generals, freeSlots: freeSlots(rack)};
    // DOM controls stay live while original game time (including in-flight recruits) is paused.
    I.TimeMgr.instance().pause(true);
    session = {scene, rack, granted: false};
    return snapshot;
  }

  function close() {
    if (!session) return;
    session = null;
    // host-bridge owns background/continue separately and keeps those pauses in force.
    I.TimeMgr.instance().resume();
  }

  function grant(id) {
    const reservations = [];
    const slots = I.SlotsMgr.instance();
    try {
      if (!session || session.granted || currentBattle() !== session.scene) throw new Error('请重新打开援将面板。');
      const rack = session.rack;
      const generals = I.DataMgr.instance().km;
      const name = Number.isInteger(id) && generals.generalType[id];
      if (!name) throw new Error('请选择有效武将。');
      const words = Array.from(name);
      if (!words.every(word => generals.gE.includes(word))) throw new Error('该武将的姓名字无法用于布阵。');

      // An empty-looking slot may already be reserved by a normal recruit animation.
      const available = freeSlots(rack);
      if (available < words.length) {
        return {ok:false, freeSlots:available, error:'请先腾出 ' + (words.length - available) + ' 格。'};
      }
      for (const word of words) {
        const reservation = slots.kB(PLAYER, -1);
        if (!reservation) throw new Error('征召栏已有其他操作，请重试。');
        reservations.push({...reservation, word});
      }

      const units = I.SoldierMgr.instance();
      const prior = new Set(units.$D.keys());
      try {
        for (const {index, word} of reservations) {
          // Same factory as the normal refresh. It registers, displays and enables dragging the real unit.
          const unit = units.BB(RECRUIT_RACK, word, PLAYER, index);
          if (!unit || rack.getItem(index) !== unit) throw new Error('姓名字发放失败。');
        }
      } catch (error) {
        // BB can throw after registering a letter but before returning it to the caller.
        for (const [unitId, part] of units.$D) {
          if (!prior.has(unitId) && part.Jm && part.vS === RECRUIT_RACK &&
              reservations.some(slot => slot.index === part.kS.x)) units.iD(unitId);
        }
        throw error;
      }
      session.granted = true;
      return {ok:true, name, words, freeSlots:available - words.length};
    } catch (error) {
      return {ok:false, error:error.message || String(error)};
    } finally {
      for (const reservation of reservations) slots.release(reservation.token);
    }
  }

  window.XanaduBattleAid = Object.freeze({open, grant, close});
})();
