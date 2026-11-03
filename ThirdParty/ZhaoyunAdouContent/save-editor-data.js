/* Local player progression for the pinned game. No platform APIs or separate save key. */
(function () {
  'use strict';
  const I = window.__XanaduZhaoyunInternals;
  const keepKey = '_xanaduKeepSkills';
  const recruitsKey = '_xanaduManualRecruitIds';
  const playerPrototype = I.PlayerData.prototype;
  const specialPrototype = I.SpecialGeneralMgr.prototype;

  function requireValue(condition, message) {
    if (!condition) throw new Error(message);
  }

  function integer(value, min, max) {
    return Number.isSafeInteger(value) && value >= min && value <= max;
  }

  function manualId(id) {
    return integer(id, 12, 14);
  }

  // initData enumerates the constructor's keys, then expires props and writes immediately.
  // Register the two local keys before that pass, including for first-time saves.
  const init = playerPrototype.init;
  playerPrototype.init = function (...args) {
    this._data[keepKey] = true;
    this._data[recruitsKey] = [];
    return init.apply(this, args);
  };
  const load = playerPrototype.initData;
  playerPrototype.initData = function (saved) {
    const input = Object.assign({}, saved);
    input[keepKey] = typeof saved[keepKey] === 'boolean' ? saved[keepKey] : true;
    input[recruitsKey] = Array.isArray(saved[recruitsKey])
      ? [...new Set(saved[recruitsKey].filter(manualId))] : [];
    return load.call(this, input);
  };

  const expireProps = playerPrototype.deleteYesterdayProps;
  playerPrototype.deleteYesterdayProps = function () {
    if (!this._data[keepKey]) return expireProps.call(this);
    // Keep original acquisition times, while upgrading legacy id / [id,level] records.
    const rows = this._data._props;
    for (let i = 0; i < rows.length; i++) {
      const row = rows[i];
      if (Number.isInteger(row) || (Array.isArray(row) && row.length < 3 &&
          Number.isInteger(row[0]) && (row.length === 1 || Number.isInteger(row[1])))) {
        rows[i] = this.normalizePropsItem(row, this.resolveAcquireTime(row));
      }
    }
  };

  const recruitable = specialPrototype.nL;
  const recruitLabel = specialPrototype.lL;
  function hasManualRecruit(manager, id) {
    return manualId(id) && manager.player?._data[recruitsKey]?.includes(id);
  }
  specialPrototype.nL = function (id) {
    return hasManualRecruit(this, id) || recruitable.call(this, id);
  };
  specialPrototype.lL = function (id) {
    return hasManualRecruit(this, id) ? '可招募' : recruitLabel.call(this, id);
  };

  function currentGame() {
    const status = window.XanaduZhaoyun?.status();
    requireValue(status?.ready && status.active && !status.paused && !status.battle &&
      status.scene === 'MainScene', '请回到游戏主界面后再管理存档。');
    const scenes = I.SceneMgr.instance();
    for (const dialog of scenes.ho.values()) {
      requireValue(!dialog || !dialog.parent || dialog.destroyed || dialog.visible === false,
        '请先关闭游戏中的弹窗。');
    }
    requireValue(!document.getElementById('xanadu-personal-rank'), '请先关闭个人段位。');
    return I.DataMgr.instance();
  }

  // OD() excludes owned items; the editor must also show their current levels.
  function propCatalog(game) {
    const manager = I.PropsMgr.instance();
    const result = [];
    game.props.Fn.forEach((item, id) => {
      if (id === 0 || id === 1 || id === 23) return;
      result.push({id, name: item.txt, kind: manager.MD(id) ? 'active' : 'passive',
        level: game.player.getPropsLevel(id), maxLevel: item.Yn ? item.Yn.length : 1});
    });
    return result;
  }

  function catalog() {
    const game = currentGame();
    const player = game.player;
    const special = I.SpecialGeneralMgr.instance();
    return {
      gold: player.gold,
      keepSkills: player._data[keepKey],
      props: propCatalog(game),
      weapons: [...game.wy.my].map(([id, weapon]) => ({
        id, name: weapon.txt, count: Math.floor(player.getWeaponFragmentCount(id) / weapon.fragmentNum)
      })),
      generals: game.km.generalType.map((name, id) => ({
        id, name, recruitable: !special.aL(id) || special.nL(id)
      }))
    };
  }

  function propId(row) {
    return Array.isArray(row) ? row[0] : row;
  }

  function newProp(game, next, change) {
    const {id, level, replaceId} = change;
    const props = propCatalog(game);
    const requested = props.find(item => item.id === id);
    requireValue(requested && integer(level, 1, requested.maxLevel), '请选择有效的道具和等级。');
    const rows = next._props;
    requireValue(Array.isArray(rows) && rows.every(row => {
      const itemId = propId(row);
      return Number.isInteger(itemId) && game.props.Fn[itemId] &&
        (!Array.isArray(row) || (integer(row[1], 1, game.props.Fn[itemId].Yn?.length ?? 1) &&
          (row.length < 3 || Number.isFinite(row[2]))));
    }), '已有道具记录异常，未修改存档。');
    const ids = rows.map(propId);
    requireValue(new Set(ids).size === ids.length, '已有道具重复，未修改存档。');
    const index = ids.indexOf(id);
    const limit = requested.kind === 'active' ? game.props.Kn : game.props.Qn;
    const manager = I.PropsMgr.instance();
    const sameKind = other => manager.MD(other) === (requested.kind === 'active');
    const atCapacity = ids.filter(sameKind).length >= limit;
    const replacing = replaceId !== undefined && replaceId !== null;

    if (replacing) {
      const oldIndex = ids.indexOf(replaceId);
      requireValue(index === -1 && atCapacity && oldIndex >= 0 && sameKind(replaceId),
        '只能在同类栏已满时替换一件已持有的道具。');
      rows.splice(oldIndex, 1, [id, level, Date.now()]);
    } else if (index >= 0) {
      const acquired = game.player.resolveAcquireTime(rows[index]);
      rows[index] = [id, level, acquired];
    } else {
      requireValue(!atCapacity, '同类道具栏已满，请选择要替换的道具。');
      rows.push([id, level, Date.now()]);
    }
    const activeCount = rows.filter(row => manager.MD(propId(row))).length;
    requireValue(activeCount <= game.props.Kn && rows.length - activeCount <= game.props.Qn,
      '已有道具数量超出栏位，未修改存档。');
    next._openProps = true;
  }

  function addWeapon(game, next, id, quantity) {
    const weapon = game.wy.my.get(id);
    requireValue(weapon && integer(weapon.fragmentNum, 1, Number.MAX_SAFE_INTEGER) &&
      integer(quantity, 1, 99), '请选择有效武器，每次可增加 1 至 99 把。');
    const rows = next._weaponFragments;
    requireValue(Array.isArray(rows) && rows.every(pair => Array.isArray(pair) &&
      integer(pair[0], 0, Number.MAX_SAFE_INTEGER) && integer(pair[1], 0, Number.MAX_SAFE_INTEGER)) &&
      Array.isArray(next._newWeaponIds) && next._newWeaponIds.every(Number.isSafeInteger),
      '已有武器记录异常，未修改存档。');
    const matching = rows.filter(pair => pair[0] === id);
    requireValue(matching.length <= 1, '已有武器记录重复，未修改存档。');
    const count = (matching[0]?.[1] || 0) + weapon.fragmentNum * quantity;
    requireValue(Number.isSafeInteger(count), '武器碎片数量超出范围。');
    if (matching.length) matching[0][1] = count;
    else rows.push([id, count]);
    for (let i = 0; i < quantity; i++) next._newWeaponIds.push(id);
  }

  function refreshProps(player) {
    const props = I.PropsMgr.instance();
    props.rD.length = 0;
    props.oD.length = 0;
    for (const row of player.getPropsData()) {
      const id = propId(row);
      (props.MD(id) ? props.rD : props.oD).push(id);
    }
  }

  function apply(change) {
    let saved = false;
    try {
      const game = currentGame();
      const player = game.player;
      requireValue(change && typeof change === 'object', '请选择有效的存档操作。');
      const next = JSON.parse(JSON.stringify(player._data));
      switch (change.kind) {
        case 'settings':
          requireValue(integer(change.gold, 0, 99999999) && typeof change.keepSkills === 'boolean',
            '金币须为 0 至 99999999 的整数，并指定跨日道具设置。');
          next._gold = change.gold;
          next[keepKey] = change.keepSkills;
          break;
        case 'weapon':
          addWeapon(game, next, change.id, change.quantity);
          break;
        case 'prop':
          newProp(game, next, change);
          break;
        case 'general':
          requireValue(integer(change.id, 0, game.km.generalType.length - 1), '请选择有效武将。');
          if (!manualId(change.id) || next[recruitsKey].includes(change.id)) return {ok: true};
          next[recruitsKey].push(change.id);
          break;
        default:
          throw new Error('未知存档操作。');
      }

      const result = window.__XanaduZhaoyunPersistPlayerData(player, next);
      if (!result.ok) return result;
      saved = true;
      if (change.kind === 'prop') refreshProps(player);
      if (change.kind === 'weapon') I.WeaponMgr.instance().refresh();
      if (change.kind === 'settings') {
        const main = I.SceneMgr.instance().co('MainScene');
        if (main.goldTxt) main.goldTxt.text = String(player.gold);
      }
      return {ok: true};
    } catch (error) {
      if (saved) return {ok: true, warning: '修改已保存，界面更新失败。请重新进入游戏查看。'};
      return {ok: false, error: String(error.message || error)};
    }
  }

  window.XanaduSaveEditorData = Object.freeze({catalog, apply});
})();
