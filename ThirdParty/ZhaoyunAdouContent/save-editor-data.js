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
  // These PropsConfig blurbs differ from their pinned battle handlers. Other items use nr(),
  // including the level-specific activation chances of 升职令.
  const propEffects = {
    毛笔:'将目标改成另一个字，保留等级和已携带的效果。新字不会是铲。',
    练兵符:'低于 3 级的目标必升 1 级；3、4 级的目标分别有 70%、60% 概率升级，否则降 1 级。每局最多使用两次。',
    御敌千里:'使弓兵或弓系武将的基础射程翻倍，本局持续。合成可以继承，同一目标不能叠加。',
    砚台:'使落点附近敌方单位的攻速加成减少 20 个百分点，持续 5 秒。攻速最低为单位的基础攻速。',
    地雷:'埋在道路上，敌人踩中后引爆。普通小怪被炸飞并击杀，首领受到生命值 10% 的伤害。',
    农民:'每 30 秒尝试生成一名农民。普通耕作时，1 至 5 级农民分别约每 20、10、5、3、2 秒产出 1 个局内馒头。',
    招贤榜:'原牌池中的每张武将姓名字各有 50% 概率多放一张。实际抽到的概率随本局牌池变化。',
    陨石:'来敌接近阿斗时，对路线末段的敌人造成伤害。冷却 300 秒。',
    垃圾桶:'回收文字单位，获得与其等级相等的局内馒头。不能回收农民。'
  };

  function propCatalog(game) {
    const manager = I.PropsMgr.instance();
    const result = [];
    game.props.Fn.forEach((item, id) => {
      if (id === 0 || id === 1 || id === 23) return;
      const active = manager.MD(id);
      const level = game.player.getPropsLevel(id);
      const maxLevel = item.Yn ? item.Yn.length : 1;
      const levelDetails = Array.from({length:maxLevel}, (_, index) => {
        const previewLevel = index + 1;
        const rarity = game.props.ar(id, previewLevel);
        return {level:previewLevel, rarityName:game.props.sr[rarity], rarityColor:game.props.ir[rarity],
          description:propEffects[item.txt] || game.props.nr(id, previewLevel)};
      });
      const preview = levelDetails[level - 1] || levelDetails[0];
      result.push({id, name:item.txt, kind:active ? 'active' : 'passive', typeLabel:active ? '主动道具' : '被动道具',
        level, maxLevel, levelDetails, rarityName:preview.rarityName, rarityColor:preview.rarityColor,
        description:preview.description});
    });
    return result;
  }

  // The pinned GeneralFactory configures attacks per named general. It has no general rarity.
  const weaponTypes = ['弓', '枪', '刀', '剑'];
  // Confirmed combat effects for items whose weapon.json intro promises a different
  // trigger, target, reward or finisher. See the pinned weapon guide for source anchors.
  const weaponEffects = {
    铁胎弓:'有 10% 概率射出火龙，沿路径留下火焰。',
    霸王弓:'主箭首次命中时，分别有 50% 概率向附近的其他敌人弹射。雷电还能继续连锁。',
    诸葛连弩:'先攻击 10 次，第 11 次攻击改为扇形连发 10 支火箭，然后重新计数。',
    铁枪:'对每个新目标首次命中时，有 20% 概率刺穿 0.5 秒，并追加 2 倍当前攻击的伤害。',
    点钢枪:'先前命中的当前目标死亡时，持枪人攻速提高 50%，持续 2 秒。其他单位完成击杀也能触发。',
    梨花枪:'先前命中的当前目标死亡时，飞出 8 朵梨花追击其他敌人。多朵梨花可以命中同一目标。',
    丈八蛇矛:'持枪武将上场时和每次升级时，都会尝试在射程内的道路上放出一条灵蛇。灵蛇约每 2 秒攻击一次。',
    龙胆亮银枪:'每次生成攻击投射物时，有 10% 概率召唤飞枪，分别打击本方战场的有效敌人。赵云每段刺击的触发率为 5%。',
    古锭刀:'首次命中每个新敌人时，获得 1 个局内馒头。',
    青龙偃月刀:'当前锁定的目标死亡后，下一次攻击释放旋转刀气。',
    方天画戟:'将普通敌人挑起再摔落。武将 1 至 5 级时触发率为 10% 至 30%。落地造成 5 倍当前攻击的伤害，目标剩余生命低于 20% 时直接击杀。周围敌人跌倒 1 秒。',
    铁剑:'除装备面板的攻击加成外，战斗中还增加 3 点攻击。',
    轩辕剑:'持剑人每击杀 5 名敌人，召出一柄圣剑。蓄势超过 3 秒后，下次攻击有 50% 概率斩杀剩余生命低于一半的目标。',
    落月弓:'目标距持弓人不超过 1 格时攻速翻倍，1 至 2 格时为 1.5 倍。命中约 5 秒后结算月牙伤害，剩余生命不超过 20% 时处决。'
  };
  function weaponDescription(weapon) {
    const effect = weapon.id >= 34 && weapon.id <= 42
      ? '每第 10 次攻击触发剑招。君子击晕范围内的敌人 3 秒，小人强化横扫后进入虚弱期。刘备必出君子，其他可选持剑人两招各有 50% 概率。'
      : weaponEffects[weapon.txt] || weapon.intro;
    return '装备攻击 +' + weapon.addAttPower + '。' + effect;
  }
  const generalMechanics = {
    赵云:'快速多段刺击，优先拦截靠近终点的敌人。“七进七出”召唤幻象，在敌群中往返突刺七次。',
    张飞:'横扫命中会减慢敌人移动。“大喝”震晕周围敌人 2 秒。',
    马超:'投枪有概率震晕目标，普通敌人的眩晕概率高于首领。',
    关羽:'持续攻击同一目标会逐步提高攻速。“跳斩”连续发动五次，每次对周围敌人造成溅射伤害。',
    黄忠:'弓箭贯穿敌人，升级可增加射击次数。“火箭烈”向道路上的敌群发射范围火箭。',
    关平:'横扫周围敌人。“大喝”震晕周围敌人 1 秒。',
    关兴:'优先拦截靠近终点的敌人，命中普通敌人有 10% 概率使其眩晕 0.3 秒。',
    张苞:'出枪攻击，命中普通敌人有 10% 概率使其眩晕 0.3 秒。',
    张翼:'优先攻击靠近终点的敌人。“跳斩”发动一次，对周围敌人造成 50% 溅射伤害。',
    黄盖:'挥剑攻击离自己最近的单个敌人。',
    刘备:'普攻拦截靠近终点的敌人。“圣剑”攻击近处目标，造成范围伤害并击倒敌人。',
    黄祖:'优先攻击靠近终点的敌人。“箭雨”向敌群连续射出多轮箭矢。',
    孙尚香:'三把剑分别选取目标并攻击。“剑神”短时提高攻速和伤害，扩大索敌范围。',
    甄宓:'放箭时有机会引来天雷、狂风或雨。“惊风雨”使气象攻击频繁触发。',
    貂蝉:'普攻可发射花瓣，并有机会抛出往返的大花。“桃花漫舞”控制敌人跳舞，期间花瓣翻倍。'
  };

  function generalCatalog() {
    const game = I.DataMgr.instance();
    const special = I.SpecialGeneralMgr.instance();
    return game.km.generalType.map((name, id) => ({
      id, name, words:game.km.merge[id].slice(), recruitable:!special.aL(id) || special.nL(id),
      typeLabel:weaponTypes[game.km.Mm[id].type] + '系武将', description:generalMechanics[name]
    }));
  }

  function catalog() {
    const game = currentGame();
    const player = game.player;
    return {
      gold: player.gold,
      keepSkills: player._data[keepKey],
      props: propCatalog(game),
      weapons: [...game.wy.my].map(([id, weapon]) => ({
        id, name:weapon.txt, count:Math.floor(player.getWeaponFragmentCount(id) / weapon.fragmentNum),
        typeLabel:weaponTypes[weapon.type], rarityName:game.wy.sr[weapon.rarity],
        rarityColor:game.wy.ir[weapon.rarity], description:weaponDescription(weapon)
      })),
      generals: generalCatalog()
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

  window.XanaduSaveEditorData = Object.freeze({catalog, apply, generals:generalCatalog});
})();
