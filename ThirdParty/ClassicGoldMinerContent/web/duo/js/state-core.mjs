import { MAPS } from "./game-data.mjs";

// Pure input/persistence boundaries, independent of Phaser and the native host.
export class ShopSelection {
  constructor(selected = null) {
    this.selected = selected;
  }
  tap(item, purchase) {
    if (item.bought) return "unavailable";
    if (this.selected !== item.id) {
      this.selected = item.id;
      return "selected";
    }
    if (!purchase(item)) return "insufficient";
    this.selected = null;
    return "bought";
  }
}

export class PointerActions {
  constructor(dispatch) {
    this.dispatch = dispatch;
    this.owners = new Map();
  }
  down(pointerId, player, action) {
    if (this.owners.has(pointerId)) return false;
    if (![0, 1].includes(player) || !["drop", "bomb"].includes(action))
      return false;
    this.owners.set(pointerId, { player, action });
    this.dispatch(player, action);
    return true;
  }
  up(pointerId) {
    this.owners.delete(pointerId);
  }
  cancelAll() {
    this.owners.clear();
  }
}

// Reserve before starting the delayed visual effect. Two simultaneous pointers
// cannot spend the same shared bomb, and one hero cannot stack an in-flight bomb.
export function reserveBomb(shared, hero) {
  if (shared.bombCount < 1 || hero.bombRemaining !== null) return false;
  shared.bombCount -= 1;
  hero.bombRemaining = 500;
  return true;
}

function invalid(field) {
  throw new Error(`双人存档损坏或版本不兼容（${field}），原存档已保留。`);
}
function object(value, field) {
  if (!value || typeof value !== "object" || Array.isArray(value))
    invalid(field);
}
function number(value, field, min, max, integer = false) {
  if (
    typeof value !== "number" ||
    !Number.isFinite(value) ||
    value < min ||
    value > max ||
    (integer && !Number.isInteger(value))
  )
    invalid(field);
}
function bool(value, field) {
  if (typeof value !== "boolean") invalid(field);
}
function choice(value, field, values) {
  if (!values.includes(value)) invalid(field);
}
function list(value, field, max) {
  if (!Array.isArray(value) || value.length > max) invalid(field);
}
function text(value, field, max = 100) {
  if (typeof value !== "string" || value.length > max) invalid(field);
}
function duration(value, field, max) {
  if (value !== null) number(value, field, 0, max);
}

const animations = ["stand", "grab", "speedup", "fail", "boom", "reset"];
function animation(value, field, hook = false) {
  if (value === null) return;
  object(value, field);
  const frames = hook
    ? { grab: 2, reset: 1 }
    : { stand: 1, grab: 3, speedup: 3, fail: 2, boom: 5 };
  choice(value.key, `${field}.key`, Object.keys(frames));
  number(value.frame, `${field}.frame`, 1, frames[value.key], true);
  number(value.accumulator, `${field}.accumulator`, 0, 60000);
  number(value.nextTick, `${field}.nextTick`, 0, 60000);
  number(
    value.repeatCounter,
    `${field}.repeatCounter`,
    -1,
    Number.MAX_SAFE_INTEGER,
    true,
  );
  number(value.delayCounter, `${field}.delayCounter`, 0, 60000);
  for (const k of [
    "forward",
    "inReverse",
    "isPlaying",
    "hasStarted",
    "pendingRepeat",
  ])
    bool(value[k], `${field}.${k}`);
}

export function validateSnapshot(state) {
  object(state, "root");
  if (state.schema !== 1 || state.mode !== "duo") invalid("schema/mode");
  choice(state.scene, "scene", ["Start", "Target", "Play", "Shop", "Fail"]);
  const run = state.run;
  object(run, "run");
  number(run.level, "level", 1, 30, true);
  if (
    run.totalLevel !== 30 ||
    run.averageMoney !== 150 ||
    run.baseMoney !== 500 ||
    run.time !== 40
  )
    invalid("rules");
  number(run.money, "money", 0, 1e9, true);
  number(run.targetMoney, "targetMoney", 0, 5000, true);
  choice(run.levelState, "levelState", ["target", "win", "pass", "fail"]);
  for (const key of ["showShop", "getShop"]) {
    list(run[key], key, 5);
    for (const id of run[key]) number(id, key, 1, 5, true);
    if (new Set(run[key]).size !== run[key].length) invalid(key);
  }
  if (
    run.showShop.length !== 3 ||
    run.getShop.some((id) => !run.showShop.includes(id))
  )
    invalid("shop rules");
  if (
    state.scene !== "Start" &&
    run.targetMoney !== run.level * run.averageMoney + run.baseMoney
  )
    invalid("targetMoney rules");
  if (state.scene === "Play") {
    const play = state.play;
    object(play, "play");
    const viewportWidth = play.viewportWidth === undefined ? 550 : play.viewportWidth;
    number(viewportWidth, "viewportWidth", 550, 1600, true);
    number(play.timeNum, "timeNum", 0, 40, true);
    number(play.clockElapsed, "clockElapsed", 0, 1000);
    number(play.background, "background", 1, 4, true);
    number(play.strengthEffect, "strengthEffect", 0, 100);
    number(play.luckyEffect, "luckyEffect", 0, 1);
    number(play.stoneBookEffect, "stoneBookEffect", 0, 3, true);
    number(play.bombCount, "bombCount", 0, 5, true);
    list(play.ores, "ores", 64);
    if (play.ores.length !== MAPS[run.level].length) invalid("map length");
    const ids = new Set();
    const caught = new Set();
    for (const ore of play.ores) {
      object(ore, "ore");
      number(ore.id, "ore.id", 0, 63, true);
      if (ids.has(ore.id)) invalid("duplicate ore");
      ids.add(ore.id);
      if (MAPS[run.level][ore.id]?.key !== ore.key) invalid("map identity");
      choice(ore.key, "ore.key", [
        "bomb",
        "bone",
        "diamond",
        "gold1",
        "gold2",
        "gold3",
        "lunckybag",
        "pig",
        "stone1",
        "stone2",
      ]);
      number(ore.x, "ore.x", -2000, 2000);
      number(ore.y, "ore.y", -2000, 2000);
      for (const key of ["show", "grabbing", "visible"])
        bool(ore[key], `ore.${key}`);
      number(ore.rotation, "ore.rotation", -100, 100);
      number(ore.scaleX, "ore.scaleX", -10, 10);
      number(ore.scaleY, "ore.scaleY", -10, 10);
    }
    list(play.heroes, "heroes", 2);
    if (play.heroes.length !== 2) invalid("heroes");
    for (const hero of play.heroes) {
      object(hero, "hero");
      for (const key of ["running", "pause"]) bool(hero[key], `hero.${key}`);
      choice(hero.rotationSpeed, "rotationSpeed", [-1, 1]);
      if (hero.aimAngle !== undefined) number(hero.aimAngle, "aimAngle", -180, 180);
      number(hero.grabSpeed, "grabSpeed", 0, 20);
      number(hero.grabSin, "grabSin", -1, 1);
      number(hero.grabCos, "grabCos", -1, 1);
      number(hero.friction, "friction", 0, 1);
      number(hero.prices, "prices", 0, 1e9, true);
      duration(hero.resetRemaining, "resetRemaining", 100);
      duration(hero.bombRemaining, "bombRemaining", 500);
      choice(
        hero.bombReturnAnimation,
        "bombReturnAnimation",
        animations.filter((key) => key !== "reset"),
      );
      animation(hero.animation, "hero.animation");
      object(hero.hook, "hook");
      number(hero.hook.x, "hook.x", -300, viewportWidth + 150);
      number(hero.hook.y, "hook.y", -100, 500);
      number(hero.hook.angle, "hook.angle", -180, 180);
      for (const field of ["grabPoint", "rope"]) {
        object(hero.hook[field], `hook.${field}`);
        number(hero.hook[field].x, `hook.${field}.x`, -400, viewportWidth + 250);
        number(hero.hook[field].y, `hook.${field}.y`, -200, 600);
      }
      animation(hero.hook.animation, "hook.animation", true);
      choice(hero.hook.frame, "hook.frame", ["hook00", "hook01", "__BASE"]);
      if (hero.hook.oreId !== null) {
        number(hero.hook.oreId, "hook.oreId", 0, 63, true);
        if (!ids.has(hero.hook.oreId) || caught.has(hero.hook.oreId))
          invalid("caught ore");
        const ore = play.ores.find((value) => value.id === hero.hook.oreId);
        if (!ore.grabbing || !ore.show) invalid("caught ore state");
        caught.add(hero.hook.oreId);
      }
      object(hero.priceTip, "priceTip");
      text(hero.priceTip.text, "priceTip.text");
      duration(hero.priceTip.remaining, "priceTip.remaining", 1500);
    }
    for (const ore of play.ores)
      if (ore.grabbing && !caught.has(ore.id)) invalid("orphan caught ore");
  } else if (state.scene === "Target") {
    object(state.target, "target");
    number(state.target.remaining, "target.remaining", 0, 2000);
  } else if (state.scene === "Shop") {
    object(state.shop, "shop");
    list(state.shop.items, "shop.items", 5);
    const ids = new Set();
    for (const item of state.shop.items) {
      object(item, "shop.item");
      number(item.id, "shop.id", 1, 5, true);
      number(item.price, "shop.price", 1, 500, true);
      bool(item.bought, "shop.bought");
      if (ids.has(item.id) || !run.showShop.includes(item.id))
        invalid("shop inventory");
      ids.add(item.id);
      if (item.bought !== run.getShop.includes(item.id))
        invalid("shop purchase");
    }
    if (ids.size !== run.showShop.length) invalid("shop inventory length");
    if (
      state.shop.selected !== null &&
      (!ids.has(state.shop.selected) ||
        state.shop.items.find((item) => item.id === state.shop.selected).bought)
    )
      invalid("shop selection");
  }
  // Never return a caller-owned mutable object, and never repair malformed data
  // with defaults that would silently replace an unrecoverable saved game.
  return JSON.parse(JSON.stringify(state));
}
