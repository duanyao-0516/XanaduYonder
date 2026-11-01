import { P as Phaser } from "./vendor-04f93d05.js";
import { ORE_TYPES, SHOP_TYPES, MAPS } from "./game-data.mjs";
import { BASE_WIDTH, GAME_HEIGHT, playWidth, heroX, projectAngle, reflowPlay, minePlayer } from "./layout.mjs";
import {
  validateSnapshot,
  ShopSelection,
  PointerActions,
  reserveBomb,
} from "./state-core.mjs";

const freshRun = () => ({
  level: 1,
  totalLevel: 30,
  money: 0,
  averageMoney: 150,
  baseMoney: 500,
  targetMoney: 0,
  levelState: "target",
  time: 40,
  showShop: [1, 2, 5],
  getShop: [],
});
const clone = (value) => JSON.parse(JSON.stringify(value));
const host = window.ClassicHost;
function viewportSize() {
  const box = document.getElementById("app");
  return { width: box?.clientWidth || 550, height: box?.clientHeight || 400 };
}
function safeInsets() {
  const style = typeof window.getComputedStyle === "function" ? window.getComputedStyle(document.documentElement) : null;
  return Object.fromEntries(["left", "top", "right", "bottom"].map(edge => [edge,
    Math.max(8, parseFloat(style?.getPropertyValue(`--classic-safe-${edge}`)) || 8),
  ]));
}
function configureViewport(scene, playing) {
  const size = viewportSize(), width = playing ? playWidth(size.width, size.height) : BASE_WIDTH;
  // A paused restart may run synchronously before Phaser's resize listener.
  // FIT reads cached parentSize before refresh measures it, so measure first.
  scene.game.scale?.getParentBounds();
  scene.game.scale?.setGameSize(width, GAME_HEIGHT);
  scene.cameras?.main.setSize(width, GAME_HEIGHT);
  document.body?.classList.toggle("playing", playing);
  return width;
}
const animationFields = [
  "accumulator",
  "nextTick",
  "delayCounter",
  "forward",
  "inReverse",
  "isPlaying",
  "hasStarted",
  "pendingRepeat",
];

function captureAnimation(sprite) {
  const state = sprite.anims;
  if (!state.currentAnim || !state.currentFrame) return null;
  const result = {
    key: state.currentAnim.key,
    frame: state.currentFrame.index,
    repeatCounter:
      state.repeatCounter === Number.MAX_VALUE ? -1 : state.repeatCounter,
  };
  for (const field of animationFields) result[field] = state[field];
  return result;
}

function restoreAnimation(sprite, state) {
  if (!state) return;
  sprite.play(state.key);
  const frame = sprite.anims.currentAnim.frames[state.frame - 1];
  if (!frame) throw new Error("双人存档动画帧无效，原存档已保留。");
  sprite.anims.setCurrentFrame(frame);
  for (const field of animationFields) sprite.anims[field] = state[field];
  sprite.anims.repeatCounter =
    state.repeatCounter === -1 ? Number.MAX_VALUE : state.repeatCounter;
}

export const runtime = {
  run: freshRun(),
  pending: null,
  current: null,
  game: null,
  active: false,
  paused: false,
  blocked: false,
  existing: false,
  inputs: null,
  skipDelta: false,
  transitionState: null,
  reflowing: false,
  enter(scene) {
    this.current = scene;
    this.game = scene.game;
    this.reflowing = false;
    this.game.input.enabled = !this.paused;
    this.inputs?.cancelAll();
    this.pending = null;
    this.transitionState = null;
    updateControls(scene.sys.settings.key === "Play" && !this.paused);
    if (this.paused) {
      // Phaser writes RUNNING after create() returns. Applying pause inside
      // create alone is overwritten; CREATE is emitted after that assignment.
      scene.events.once("create", () => {
        if (this.paused) this.pause();
      });
    }
    host.checkpoint();
  },
  capture() {
    if (!this.active || this.blocked || !this.current) return null;
    if (this.transitionState) return validateSnapshot(this.transitionState);
    const state = {
      schema: 1,
      mode: "duo",
      scene: this.current.sys.settings.key,
      run: clone(this.run),
    };
    if (state.scene === "Play") state.play = this.current.capture();
    if (state.scene === "Shop") state.shop = this.current.capture();
    if (state.scene === "Target")
      state.target = { remaining: this.current.remaining };
    return validateSnapshot(state);
  },
  transition(scene) {
    // Phaser queues scene switches until the next frame. Publish the logical
    // destination now so an immediate Android Back cannot save a mixed scene.
    if (["Target", "Start", "Fail"].includes(scene)) {
      if (scene === "Target")
        this.run.targetMoney =
          this.run.level * this.run.averageMoney + this.run.baseMoney;
      this.transitionState = {
        schema: 1,
        mode: "duo",
        scene,
        run: clone(this.run),
      };
      if (scene === "Target") this.transitionState.target = { remaining: 2000 };
    }
    this.current.scene.start(scene);
    host.checkpoint();
  },
  reflow() {
    if (!this.current || this.blocked || this.transitionState || this.reflowing) return;
    if (this.current.sys.settings.key !== "Play") {
      configureViewport(this.current, false);
      return;
    }
    const size = viewportSize(), width = playWidth(size.width, size.height);
    // Even an unchanged logical width can have a new CSS scale and safe HUD
    // offsets. Rebuild from the same snapshot so a paused canvas is repainted.
    // Capture before mutating any object. Until Phaser performs the queued
    // restart, both save and input observe this single consistent generation.
    const snapshot = this.capture();
    snapshot.play = reflowPlay(snapshot.play, width);
    this.pending = validateSnapshot(snapshot);
    this.transitionState = this.pending;
    this.reflowing = true;
    this.inputs?.cancelAll();
    this.game.input.enabled = false;
    this.skipDelta = true;
    this.current.scene.restart();
    // A background-paused engine must process restart once; enter() latches
    // pause again from CREATE before the first gameplay update.
    this.game.loop.wake();
  },
  pause() {
    this.paused = true;
    this.inputs?.cancelAll();
    updateControls(false);
    if (!this.game) return;
    this.game.input.enabled = false;
    this.current?.input.keyboard?.resetKeys();
    this.current?.scene.pause();
    this.game.sound.pauseAll();
    this.game.loop.sleep();
  },
  resume() {
    if (this.blocked) return;
    this.paused = false;
    this.skipDelta = true;
    this.inputs?.cancelAll();
    if (!this.game) return;
    this.game.input.enabled = true;
    this.current?.scene.resume();
    this.game.sound.mute = host.soundEnabled === false;
    if (host.soundEnabled !== false) this.game.sound.resumeAll();
    this.game.loop.wake();
    updateControls(this.current?.sys.settings.key === "Play");
  },
  delta(value) {
    if (this.paused) return 0;
    if (this.skipDelta) {
      this.skipDelta = false;
      return 0;
    }
    return Number.isFinite(value) ? Math.max(0, value) : 0;
  },
  begin() {
    if (this.blocked || this.transitionState) return;
    if (this.existing && !this.active) {
      host.message("已有双人存档，请在模式选择中继续，或确认重新开始。");
      window.location.href = "../index.html";
      return;
    }
    this.active = true;
    this.run.levelState = "target";
    this.transition("Target");
  },
};

const sounds = {
  items: {},
  scene: null,
  init(scene) {
    this.scene = scene;
    for (const key of [
      "boom",
      "catch",
      "catched",
      "di",
      "rope",
      "score",
      "scene",
    ])
      this.items[key] = scene.sound.add(key, { loop: false });
  },
  play(key) {
    if (!runtime.paused && host.soundEnabled !== false) this.items[key]?.play();
  },
  stop(key) {
    this.items[key]?.stop();
  },
  stopAll() {
    this.scene?.sound.stopAll();
  },
};

class Hook extends Phaser.GameObjects.Sprite {
  constructor(scene, x, y) {
    super(scene, x, y, "hook");
    this.setOrigin(0.5, -1);
    this.setScale(0.8, 0.8);
    this.ore = null;
    this.grabxy = { x, y: y + this.height };
    this.anims.create({
      key: "grab",
      frames: this.anims.generateFrameNames("hook", {
        prefix: "hook",
        end: 1,
        zeroPad: 2,
      }),
      frameRate: 60,
      repeat: 0,
    });
    this.anims.create({
      key: "reset",
      frames: this.anims.generateFrameNames("hook", {
        prefix: "hook",
        end: 0,
        zeroPad: 2,
      }),
      frameRate: 10,
      repeat: 0,
    });
    scene.add.existing(this);
  }
  playGrabAnims() {
    this.play("grab");
  }
  playResetAnims() {
    this.play("reset");
  }
  getGrabXY() {
    return this.grabxy;
  }
  setGrabXY(x, y) {
    this.grabxy = { x, y };
  }
  move(x, y) {
    this.x += x;
    this.y += y;
  }
  setHookOre(ore) {
    if (ore === null && this.ore) this.ore.grabing = false;
    this.ore = ore;
  }
}

export class Hero extends Phaser.GameObjects.Sprite {
  constructor(scene, x, y, index) {
    super(scene, x, y, "role_grab", 0);
    scene.add.existing(this);
    this.setScale(0.5, 0.5);
    this.rotationSpeed = index > 0 ? -1 : 1;
    this.running = false;
    this.grabSpeed = 4;
    this.grabSin = 0;
    this.grabCos = 0;
    this.gameWidth = scene.gameWidth || 550;
    this.gameHeight = 400;
    this.aimAngle = 0;
    this.friction = 1;
    this.prices = 0;
    this.pause = false;
    this.resetRemaining = null;
    this.bombRemaining = null;
    this.bombReturnAnimation = "stand";
    this.priceRemaining = null;
    this.graphics = scene.add.graphics({
      lineStyle: { width: 2, color: 5592405 },
    });
    this.hook = new Hook(scene, x - 15, y + 20);
    this.hookOriginX = this.hook.x;
    this.hookOriginY = this.hook.y;
    this.line = new Phaser.Geom.Line(
      this.hookOriginX,
      this.hookOriginY - 10,
      this.hookOriginX,
      this.hookOriginY,
    );
    this.hookDistance = Phaser.Math.Distance.Between(
      this.hook.x,
      this.hook.y,
      x,
      y,
    );
    this.updateLine();
    for (const [key, texture, end, frameRate, repeat] of [
      ["speedup", "role_grab", 2, 20, -1],
      ["fail", "role_fail", 1, 3, -1],
      ["grab", "role_grab", 2, 10, -1],
      ["boom", "role_boom", 4, 10, 0],
      ["stand", "role_grab", 0, 1, 0],
    ])
      this.anims.create({
        key,
        frames: this.anims.generateFrameNumbers(texture, { start: 0, end }),
        frameRate,
        repeat,
      });
    this.playStandAnims();
    this.showPrice = scene.add.text(x + 10, y + 50, "", {
      fontFamily: "Arial",
      fontSize: "16px",
      color: "#00ff00",
    });
    this.showPrice.alpha = 0;
  }
  updatePriceTip(value) {
    this.showPrice.setText(typeof value === "string" ? value : `$:${value}`);
    this.priceRemaining = 1500;
    this.renderPriceTip();
  }
  renderPriceTip() {
    const ratio = this.priceRemaining === null ? 0 : this.priceRemaining / 1500;
    this.showPrice.y = this.y - 10 * (1 - ratio);
    this.showPrice.alpha = ratio;
  }
  playSpeedUpAnims() {
    this.play("speedup");
  }
  playGrabAnims() {
    this.play("grab");
  }
  playStandAnims() {
    this.play("stand");
  }
  handleBoom() {
    this.bombReturnAnimation = this.anims.getName() || "stand";
    this.play("boom");
    sounds.play("boom");
  }
  handleBombEffect() {
    return this.scene.handleBombEffect(this);
  }
  setOffset(weight) {
    this.friction = Math.min(1 - weight / 100, 1);
  }
  setPrices(value) {
    this.prices += value;
    if (value > 0) this.updatePriceTip(value);
  }
  handleRun() {
    if (runtime.paused || this.running) return;
    this.running = true;
    this.grabSin = Math.sin(Math.PI * (-this.hook.angle / 180));
    this.grabCos = Math.cos(Math.PI * (-this.hook.angle / 180));
    this.playGrabAnims();
    sounds.play("rope");
  }
  handleGrab() {
    this.grabSin *= -1;
    this.grabCos *= -1;
  }
  handleReset() {
    const empty = this.hook.ore === null;
    this.hook.x = this.hookOriginX;
    this.hook.y = this.hookOriginY;
    this.scene.settleOre(this, this.hook.ore);
    this.hook.playResetAnims();
    this.playStandAnims();
    this.setOffset(0);
    this.hook.setHookOre(null);
    if (empty) {
      this.running = false;
      this.pause = false;
    } else {
      this.pause = true;
      this.resetRemaining = 100;
    }
  }
  tickEffects(delta) {
    if (this.resetRemaining !== null) {
      this.resetRemaining = Math.max(0, this.resetRemaining - delta);
      if (this.resetRemaining === 0) {
        this.running = false;
        this.pause = false;
        this.resetRemaining = null;
      }
    }
    if (this.bombRemaining !== null) {
      this.bombRemaining = Math.max(0, this.bombRemaining - delta);
      if (this.bombRemaining === 0) {
        if (this.hook.ore) {
          this.hook.ore.grabing = false;
          this.hook.ore.setXY(-999, -999);
        }
        this.hook.setHookOre(null);
        this.setOffset(0);
        this.bombRemaining = null;
        this.play(this.bombReturnAnimation);
        this.scene.renderBombs();
      }
    }
    if (this.priceRemaining !== null) {
      this.priceRemaining = Math.max(0, this.priceRemaining - delta);
      if (this.priceRemaining === 0) this.priceRemaining = null;
      this.renderPriceTip();
    }
  }
  updateLine() {
    const sin = Math.sin(Math.PI * (-this.hook.angle / 180));
    const cos = Math.cos(Math.PI * (-this.hook.angle / 180));
    const x = this.hook.x + sin * this.hookDistance,
      y = this.hook.y + cos * this.hookDistance;
    this.hook.setGrabXY(x, y + 5);
    this.line.x2 = x;
    this.line.y2 = y;
    this.graphics.clear();
    this.graphics.strokeLineShape(this.line);
  }
  update() {
    if (this.pause) return;
    const bottomOrRight =
      this.hook.y >= this.gameHeight - 2 * this.hook.height ||
      this.hook.x >= this.gameWidth - this.hook.width;
    const leftOrTop =
      this.hook.x < this.hook.width || this.hook.y < this.hookOriginY;
    if (bottomOrRight || leftOrTop) {
      if (bottomOrRight) {
        sounds.stop("rope");
        this.hook.playGrabAnims();
      }
      if (this.hook.y < this.hookOriginY) {
        sounds.stop("rope");
        this.handleReset();
        return;
      }
      this.grabSin *= -1;
      this.grabCos *= -1;
    }
    if (this.running)
      this.hook.move(
        this.grabSin * this.grabSpeed * this.friction,
        this.grabCos * this.grabSpeed * this.friction,
      );
    else {
      const angle = Math.floor(this.aimAngle);
      if ((angle >= 60 && this.rotationSpeed > 0) || (angle <= -60 && this.rotationSpeed < 0)) this.rotationSpeed *= -1;
      this.aimAngle += this.rotationSpeed;
      this.hook.angle = projectAngle(this.aimAngle, this.gameWidth / BASE_WIDTH);
    }
    this.updateLine();
  }
  capture() {
    return {
      aimAngle: this.aimAngle,
      rotationSpeed: this.rotationSpeed,
      running: this.running,
      grabSpeed: this.grabSpeed,
      grabSin: this.grabSin,
      grabCos: this.grabCos,
      friction: this.friction,
      prices: this.prices,
      pause: this.pause,
      resetRemaining: this.resetRemaining,
      bombRemaining: this.bombRemaining,
      bombReturnAnimation: this.bombReturnAnimation,
      animation: captureAnimation(this),
      hook: {
        x: this.hook.x,
        y: this.hook.y,
        angle: this.hook.angle,
        grabPoint: { ...this.hook.getGrabXY() },
        rope: { x: this.line.x2, y: this.line.y2 },
        oreId: this.hook.ore?.id ?? null,
        frame: String(this.hook.frame.name),
        animation: captureAnimation(this.hook),
      },
      priceTip: { text: this.showPrice.text, remaining: this.priceRemaining },
    };
  }
  restore(state, ores) {
    this.aimAngle = state.aimAngle ?? projectAngle(state.hook.angle, BASE_WIDTH / this.gameWidth);
    for (const field of [
      "rotationSpeed",
      "running",
      "grabSpeed",
      "grabSin",
      "grabCos",
      "friction",
      "prices",
      "pause",
      "resetRemaining",
      "bombRemaining",
      "bombReturnAnimation",
    ])
      this[field] = state[field];
    this.hook.x = state.hook.x;
    this.hook.y = state.hook.y;
    this.hook.angle = state.hook.angle;
    this.hook.ore =
      state.hook.oreId === null
        ? null
        : ores.find((ore) => ore.id === state.hook.oreId);
    this.hook.setFrame(state.hook.frame);
    restoreAnimation(this, state.animation);
    restoreAnimation(this.hook, state.hook.animation);
    this.showPrice.setText(state.priceTip.text);
    this.priceRemaining = state.priceTip.remaining;
    this.renderPriceTip();
    // handleReset moves the sprite immediately while the original grab-point
    // and rope wait for the next movement update (after the 100 ms pause).
    this.hook.setGrabXY(state.hook.grabPoint.x, state.hook.grabPoint.y);
    this.line.x2 = state.hook.rope.x;
    this.line.y2 = state.hook.rope.y;
    this.graphics.clear();
    this.graphics.strokeLineShape(this.line);
  }
}

class Ore extends Phaser.GameObjects.Container {
  constructor(scene, key, x, y, id) {
    super(scene, x || -999, y || -999);
    scene.add.existing(this);
    this.key = key;
    this.id = id;
    this.grabing = false;
    const sprite = new Phaser.GameObjects.Sprite(scene, 0, 0, key);
    sprite.setOrigin(0.5, 0.5);
    this.add(sprite);
    this.radius = Math.max(15, Math.max(sprite.width, sprite.height) / 2) + 5;
    this.setXY(x, y);
  }
  setXY(x, y) {
    this.show = x > 0 && y > 0;
    this.setPosition(x, y);
  }
  capture() {
    return {
      id: this.id,
      key: this.key,
      x: this.x,
      y: this.y,
      show: this.show,
      grabbing: this.grabing,
      visible: this.visible,
      rotation: this.rotation,
      scaleX: this.scaleX,
      scaleY: this.scaleY,
    };
  }
}

export class PlayScene extends Phaser.Scene {
  constructor() {
    super({ key: "Play" });
  }
  create() {
    this.gameWidth = configureViewport(this, true);
    this.gameHeight = GAME_HEIGHT;
    const saved = runtime.pending?.scene === "Play"
      ? reflowPlay(runtime.pending.play, this.gameWidth) : null;
    this.clockElapsed = saved?.clockElapsed ?? 0;
    this.timeNum = saved?.timeNum ?? runtime.run.time;
    this.strengthEffect =
      saved?.strengthEffect ?? (runtime.run.getShop.includes(2) ? 20 : 0);
    this.stoneBookEffect = saved?.stoneBookEffect ?? 0;
    this.luckyEffect =
      saved?.luckyEffect ?? (runtime.run.getShop.includes(3) ? 0.6 : 0.3);
    this.bombCount =
      saved?.bombCount ?? runtime.run.getShop.filter((id) => id === 1).length;
    this.background = saved?.background ?? Math.floor(4 * Math.random()) + 1;
    this.layoutHUD();
    this.addBackground();
    this.createUI();
    this.heroes = [new Hero(this, heroX(0, this.gameWidth), 35, 0), new Hero(this, heroX(1, this.gameWidth), 35, 1)];
    this.ores = (saved?.ores ?? MAPS[runtime.run.level]).map((state, index) => {
      const ore = new Ore(
        this,
        state.key,
        saved ? state.x : state.x * this.gameWidth / BASE_WIDTH,
        state.y,
        saved ? state.id : index,
      );
      if (saved) {
        ore.show = state.show;
        ore.grabing = state.grabbing;
        ore.visible = state.visible;
        ore.rotation = state.rotation;
        ore.scaleX = state.scaleX;
        ore.scaleY = state.scaleY;
      }
      return ore;
    });
    if (saved)
      this.heroes.forEach((hero, index) =>
        hero.restore(saved.heroes[index], this.ores),
      );
    this.bombEffects = [];
    this.renderBombs();
    this.refreshMoney();
    this.addKeyboard();
    runtime.enter(this);
  }
  layoutHUD() {
    const inset = safeInsets(), q = viewportSize().height / GAME_HEIGHT;
    const left = inset.left / q + 8, top = inset.top / q + 5;
    const right = this.gameWidth - inset.right / q - 110;
    this.hudLayout = { left, top, right };
    // Both HUD rows and the players' y=70 earnings must stay above the 4px
    // divider. Use this same boundary for painting and mine touch exclusion.
    this.hudBottom = Math.max(94, top + 46);
  }
  addBackground() {
    const image = this.add.image(-1, this.hudBottom, `bg_${this.background}`);
    image.setOrigin(0, 0);
    image.setScale(this.gameWidth / image.width, (GAME_HEIGHT - this.hudBottom) / image.height);
    this.add
      .graphics({ fillStyle: { color: 16750848 } })
      .fillRectShape(new Phaser.Geom.Rectangle(0, 0, this.gameWidth, this.hudBottom - 4));
    this.add
      .graphics({ fillStyle: { color: 13274724 } })
      .fillRectShape(new Phaser.Geom.Rectangle(0, this.hudBottom - 4, this.gameWidth, 4));
    this.add
      .graphics({ lineStyle: { width: 1, color: 0 } })
      .strokeLineShape(new Phaser.Geom.Line(0, this.hudBottom, this.gameWidth, this.hudBottom));
  }
  label(x, y, text, color = "#000000") {
    return this.add.text(x, y, text, {
      fontFamily: "Arial",
      fontSize: "16px",
      color,
    });
  }
  createUI() {
    const { left, top, right } = this.hudLayout;
    this.label(left, top, "金钱：");
    this.label(left, top + 22, "目标：");
    this.label(right, top, "时间：");
    this.label(right, top + 22, "关卡：");
    this.timeTxt = this.label(right + 52, top, String(this.timeNum), "#b00000");
    this.label(right + 52, top + 22, String(runtime.run.level), "#b00000");
    this.moneyTxt = this.label(left + 52, top, `$:${runtime.run.money}`, "#006600");
    this.label(left + 52, top + 22, `$:${runtime.run.targetMoney}`, "#664000").setFontStyle("bold");
    this.heroMoneyTexts = [
      this.label(heroX(0, this.gameWidth) - 20, 70, "一 $:0", "#006600"),
      this.label(heroX(1, this.gameWidth) - 20, 70, "二 $:0", "#006600"),
    ];
  }
  renderBombs() {
    const count =
      this.bombCount +
      this.heroes.filter((hero) => hero.bombRemaining !== null).length;
    while (this.bombEffects.length < count)
      this.bombEffects.push(
        this.add
          .image(this.gameWidth / 2 + 38 + 8 * this.bombEffects.length, 45, "shop_1")
          .setScale(0.5, 0.5),
      );
    this.bombEffects.forEach((image, index) => image.setVisible(index < count));
    updateControls(!runtime.paused && !runtime.reflowing);
  }
  handleBombEffect(hero) {
    if (runtime.paused || !reserveBomb(this, hero)) return false;
    hero.handleBoom();
    this.renderBombs();
    return true;
  }
  handleLuckyEffect() {
    if (Math.random() < this.luckyEffect) {
      this.strengthEffect = 60;
      return 0;
    }
    return Math.floor(600 * Math.random() + 1);
  }
  addKeyboard() {
    this.input.keyboard?.on("keydown", (event) => {
      const mapping = {
        KeyS: [0, "drop"],
        KeyW: [0, "bomb"],
        ArrowDown: [1, "drop"],
        ArrowUp: [1, "bomb"],
      }[event.code];
      if (!mapping) return;
      event.preventDefault();
      event.stopPropagation();
      if (!event.repeat) dispatchAction(...mapping);
    });
  }
  refreshMoney() {
    this.moneyTxt.setText(`$:${runtime.run.money}`);
    this.heroes.forEach((hero, index) =>
      this.heroMoneyTexts[index].setText(`${index ? "二" : "一"} $:${hero.prices}`),
    );
  }
  settleOre(hero, ore) {
    if (!ore) return;
    let price = ORE_TYPES[ore.key].prices,
      tip = "";
    if (ore.key === "lunckybag") {
      price = this.handleLuckyEffect();
      if (price === 0) tip = "力量";
    }
    if (ore.grabing) {
      if (tip) hero.updatePriceTip(tip);
      hero.setPrices(price);
      runtime.run.money += price;
      this.refreshMoney();
      sounds.play("di");
    }
    ore.setXY(-999, -999);
    ore.grabing = false;
  }
  gameOver() {
    this.timeNum = 0;
    const run = runtime.run;
    run.levelState =
      run.money >= run.targetMoney
        ? run.level < run.totalLevel
          ? "win"
          : "pass"
        : "fail";
    if (run.levelState === "win")
      run.showShop = [1, 2, 3, 4, 5]
        .sort(() => (Math.random() > 0.5 ? 1 : -1))
        .slice(0, 3);
    run.getShop = [];
    runtime.transition("Target");
  }
  update(time, delta) {
    if (runtime.paused || runtime.reflowing) return;
    delta = runtime.delta(delta);
    this.clockElapsed += delta;
    if (this.clockElapsed >= 1000) {
      this.clockElapsed = 0;
      this.timeNum--;
      this.timeTxt.setText(String(this.timeNum));
      if (this.timeNum <= 0) {
        this.gameOver();
        return;
      }
    }
    for (const hero of this.heroes) {
      hero.tickEffects(delta);
      hero.update();
      for (const ore of this.ores) {
        if (hero.hook.ore !== null) {
          hero.setOffset(
            Math.max(
              ORE_TYPES[hero.hook.ore.key].weight - this.strengthEffect,
              20,
            ),
          );
          hero.hook.ore.x = hero.hook.getGrabXY().x;
          hero.hook.ore.y = hero.hook.getGrabXY().y;
          continue;
        }
        const point = hero.hook.getGrabXY();
        const distance = Phaser.Math.Distance.Between(
          point.x,
          point.y,
          ore.x,
          ore.y,
        );
        if (!ore.grabing && distance < ore.radius) {
          ore.grabing = true;
          hero.hook.setHookOre(ore);
          hero.handleGrab();
        }
      }
    }
  }
  capture() {
    return {
      viewportWidth: this.gameWidth,
      timeNum: this.timeNum,
      clockElapsed: this.clockElapsed,
      background: this.background,
      strengthEffect: this.strengthEffect,
      luckyEffect: this.luckyEffect,
      stoneBookEffect: this.stoneBookEffect,
      bombCount: this.bombCount,
      ores: this.ores.map((ore) => ore.capture()),
      heroes: this.heroes.map((hero) => hero.capture()),
    };
  }
}

class PreloaderScene extends Phaser.Scene {
  constructor() {
    super({ key: "Preloader" });
  }
  preload() {
    const base = "./resource/assets/";
    for (const key of [
      "startbg",
      "targetbg",
      "shopbg",
      "title",
      "failbg",
      "shop_tip",
      "exitbtn",
      ...Object.keys(SHOP_TYPES),
      ...Object.keys(ORE_TYPES),
      "bg_1",
      "bg_2",
      "bg_3",
      "bg_4",
    ])
      this.load.image(key, `${base}ui/${key}.png`);
    this.load.spritesheet("role_boom", `${base}role/role_boom.png`, {
      frameWidth: 138,
      frameHeight: 143,
    });
    this.load.spritesheet("role_grab", `${base}role/role_grab.png`, {
      frameWidth: 138,
      frameHeight: 143,
    });
    this.load.spritesheet("role_fail", `${base}role/role_fail.png`, {
      frameWidth: 165,
      frameHeight: 149,
    });
    this.load.atlas("hook", `${base}role/hook.png`, `${base}role/hook.json`);
    for (const key of [
      "boom",
      "catch",
      "catched",
      "di",
      "rope",
      "scene",
      "score",
    ])
      this.load.audio(key, `${base}sounds/${key}.mp3`);
    this.load.on("loaderror", (file) => {
      runtime.blocked = true;
      host.message(`双人游戏资源加载失败：${file.key}。请返回模式选择重试。`);
    });
  }
  create() {
    if (runtime.blocked) return;
    sounds.init(this);
    this.game.sound.mute = host.soundEnabled === false;
    this.scene.start(runtime.pending?.scene ?? "Start");
  }
}

export class StartScene extends Phaser.Scene {
  constructor() {
    super({ key: "Start" });
  }
  create() {
    configureViewport(this, false);
    this.add.image(0, 0, "startbg").setOrigin(0, 0);
    this.add.graphics({ fillStyle: { color: 0xe8bd00 } }).fillRectShape(new Phaser.Geom.Rectangle(48, 300, 195, 85));
    this.add.text(59, 312, "点左右半区，各自放钩\n点各自角落使用炸药\n两人可以同时操作", { fontFamily: "Arial", fontSize: "15px", color: "#4d3600", lineSpacing: 4 });
    const circle = new Phaser.Geom.Circle(120, 110, 95);
    const button = this.add.graphics({
      fillStyle: { color: 16750848, alpha: 0 },
    });
    button.fillCircleShape(circle);
    button.setInteractive(circle, Phaser.Geom.Circle.Contains);
    this.add
      .text(70, 110, "开 始", {
        fontFamily: "Arial",
        fontSize: "30px",
        color: "#555555",
      })
      .setShadow(2, 2, "#333333", 2, true, true);
    button.on("pointerdown", () => runtime.begin());
    runtime.enter(this);
  }
}

export class TargetScene extends Phaser.Scene {
  constructor() {
    super({ key: "Target" });
  }
  create() {
    configureViewport(this, false);
    this.remaining =
      runtime.pending?.scene === "Target"
        ? runtime.pending.target.remaining
        : 2000;
    const run = runtime.run;
    run.targetMoney = run.level * run.averageMoney + run.baseMoney;
    this.visuals = [this.add.image(0, 0, "targetbg").setOrigin(0, 0)];
    if (["pass", "win", "fail"].includes(run.levelState)) {
      sounds.stopAll();
      sounds.play("scene");
      const message =
        run.levelState === "pass"
          ? "恭喜通关游戏所有关卡"
          : run.levelState === "win"
            ? "恭喜达成本关目标分数"
            : "遗憾没达成目标分数";
      this.visuals.push(
        this.add.text(150, 140, message, {
          fontFamily: "Arial",
          fontSize: "26px",
          color: "#00ff00",
        }),
      );
    } else {
      sounds.play("scene");
      this.visuals.push(
        this.add
          .text(140, 120, "本关目标分数：", {
            fontFamily: "Arial",
            fontSize: "20px",
            color: "#FEBF00",
          })
          .setShadow(1, 1, "#FEBF55", 2, true, true),
      );
      this.visuals.push(
        this.add.text(150, 160, `$：${run.targetMoney}`, {
          fontFamily: "Arial",
          fontSize: "40px",
          color: "#00ff00",
        }),
      );
    }
    this.renderFade();
    runtime.enter(this);
  }
  renderFade() {
    for (const visual of this.visuals)
      visual.alpha = 0.3 + 0.7 * (this.remaining / 2000);
  }
  update(time, delta) {
    if (runtime.paused) return;
    this.remaining = Math.max(0, this.remaining - runtime.delta(delta));
    this.renderFade();
    if (this.remaining === 0)
      runtime.transition(
        runtime.run.levelState === "win"
          ? "Shop"
          : ["fail", "pass"].includes(runtime.run.levelState)
            ? "Fail"
            : "Play",
      );
  }
}

export class FailScene extends Phaser.Scene {
  constructor() {
    super({ key: "Fail" });
  }
  create() {
    configureViewport(this, false);
    this.add.image(0, 0, "failbg").setOrigin(0, 0);
    this.add
      .text(70, 170, `你共获得${runtime.run.money}点金钱！`, {
        fontFamily: "Arial",
        fontSize: "30px",
        color: "#FF0000",
      })
      .setShadow(2, 2, "#333333", 2, true, true);
    const button = this.add.graphics({ fillStyle: { color: 65280, alpha: 0 } }),
      area = new Phaser.Geom.Rectangle(63, 330, 70, 30);
    button.fillRectShape(area);
    button.setInteractive(area, Phaser.Geom.Rectangle.Contains);
    button.on("pointerdown", () => {
      if (runtime.run.levelState === "pass") {
        runtime.run.level = 1;
        runtime.run.money = 0;
      }
      runtime.transition("Start");
    });
    runtime.enter(this);
  }
}

export class ShopScene extends Phaser.Scene {
  constructor() {
    super({ key: "Shop" });
  }
  create() {
    configureViewport(this, false);
    const saved =
      runtime.pending?.scene === "Shop" ? runtime.pending.shop : null;
    this.selection = new ShopSelection(saved?.selected ?? null);
    this.add.image(0, 0, "shopbg").setOrigin(0, 0);
    this.add.image(225, 150, "shop_tip");
    this.add.text(
      50,
      110,
      "点一次查看说明和价格，再点同一商品购买。\n买好物品后，点击下一关继续游戏。",
      {
        fontFamily: "Arial",
        fontSize: "18px",
        color: "#000000",
        align: "left",
      },
    );
    this.add
      .text(435, 70, "下一关", {
        fontFamily: "Arial",
        fontSize: "18px",
        color: "#00ff00",
      })
      .setFontStyle("bold");
    this.tipTxt = this.make.text({
      x: 50,
      y: 330,
      text: "",
      style: {
        font: "16px Arial",
        color: "#FFFF00",
        wordWrap: { width: 450, useAdvancedWrap: true },
      },
    });
    this.walletTxt = this.add.text(420, 25, `金钱 $${runtime.run.money}`, {
      fontFamily: "Arial",
      fontSize: "14px",
      color: "#006600",
    });
    const next = this.add.graphics({ fillStyle: { color: 65280, alpha: 0 } }),
      area = new Phaser.Geom.Rectangle(422, 60, 82, 35);
    next.fillRectShape(area);
    next.setInteractive(area, Phaser.Geom.Rectangle.Contains);
    next.on("pointerdown", () => this.nextLevel());
    this.nextButton = next;
    this.touchSize = null;
    this.items = (
      saved?.items ??
      runtime.run.showShop.map((id) => ({
        id,
        price: SHOP_TYPES[`shop_${id}`].price(),
        bought: false,
      }))
    ).map((state) => {
      const item = { ...state },
        index = item.id - 1;
      item.image = this.add
        .image(80 * index + 30, 250, `shop_${item.id}`)
        .setInteractive();
      item.label = this.add
        .text(80 * index + 10, 295, `$:${item.price}`, {
          fontFamily: "Arial",
          fontSize: "14px",
          color: "#006600",
        })
        .setFontStyle("bold");
      item.image.setVisible(!item.bought);
      item.label.setVisible(!item.bought);
      item.image.on("pointerdown", () => this.handleItem(item));
      return item;
    });
    if (this.selection.selected !== null)
      this.describe(
        this.items.find((item) => item.id === this.selection.selected),
      );
    this.updateTouchAreas();
    runtime.enter(this);
  }
  updateTouchAreas() {
    const canvas = this.game.canvas;
    if (!canvas?.getBoundingClientRect) return;
    const rect = canvas.getBoundingClientRect();
    if (!rect.width || !rect.height) return;
    const size = `${rect.width}:${rect.height}`;
    if (size === this.touchSize) return;
    this.touchSize = size;
    const qx = rect.width / 550, qy = rect.height / 400;
    for (const item of this.items) {
      const image = item.image, label = item.label;
      const left = Math.max(0, image.x - 38), right = Math.min(550, image.x + 38);
      const contentLeft = Math.min(image.x - image.width / 2, label.x);
      const contentRight = Math.max(image.x + image.width / 2, label.x + label.width);
      const width = Math.min(right - left, Math.max(contentRight - contentLeft, 48 / qx));
      const x = Math.max(left, Math.min(right - width, (contentLeft + contentRight - width) / 2));
      const y = Math.max(190, Math.min(image.y - image.height / 2, image.y - 24 / qy));
      const bottom = Math.min(320, Math.max(image.y + image.height / 2, label.y + label.height, image.y + 24 / qy));
      // Phaser 3 adds displayOrigin after inverse-transforming the pointer.
      // Replace the live area: calling setInteractive again retains the old one.
      image.input.hitArea = new Phaser.Geom.Rectangle(
        x - image.x + image.width / 2, y - image.y + image.height / 2,
        width, bottom - y,
      );
      image.input.hitAreaCallback = Phaser.Geom.Rectangle.Contains;
    }
    const width = Math.min(180, Math.max(82, 48 / qx));
    const height = Math.min(150, Math.max(35, 48 / qy));
    this.nextButton.input.hitArea = new Phaser.Geom.Rectangle(
      Math.min(550 - width, 463 - width / 2), Math.max(0, 77.5 - height / 2), width, height,
    );
  }
  update() {
    this.updateTouchAreas();
  }
  describe(item) {
    this.tipTxt.setText(
      `${SHOP_TYPES[`shop_${item.id}`].tip}\n价格 $${item.price} · 再点此商品购买`,
    );
  }
  nextLevel() {
    if (runtime.paused || runtime.transitionState) return;
    runtime.run.levelState = "target";
    runtime.run.level++;
    runtime.transition("Target");
  }
  handleItem(item) {
    if (runtime.paused || runtime.transitionState) return;
    const result = this.selection.tap(item, (selected) => {
      if (runtime.run.money < selected.price) return false;
      runtime.run.money -= selected.price;
      runtime.run.getShop.push(selected.id);
      selected.bought = true;
      selected.image.setVisible(false);
      selected.label.setVisible(false);
      this.walletTxt.setText(`金钱 $${runtime.run.money}`);
      return true;
    });
    if (result === "selected") this.describe(item);
    if (result === "insufficient")
      this.tipTxt.setText(`金钱不足，需要 $${item.price}。可选择其他商品。`);
    if (result === "bought")
      this.tipTxt.setText("购买成功！可选择其他商品，或进入下一关。");
    host.checkpoint();
  }
  capture() {
    return {
      selected: this.selection.selected,
      items: this.items.map(({ id, price, bought }) => ({ id, price, bought })),
    };
  }
}

function dispatchAction(player, action) {
  if (
    runtime.paused ||
    runtime.blocked ||
    runtime.transitionState ||
    runtime.reflowing ||
    runtime.current?.sys.settings.key !== "Play"
  )
    return;
  const hero = runtime.current.heroes[player];
  if (action === "drop") hero.handleRun();
  else hero.handleBombEffect();
  host.checkpoint();
}

function updateControls(enabled) {
  const play = runtime.current?.sys.settings.key === "Play" ? runtime.current : null;
  document.body?.classList.toggle("playing", !!play);
  for (const button of document.querySelectorAll("[data-action]")) {
    const player = Number(button.dataset.player), hero = play?.heroes?.[player];
    const count = play?.bombCount ?? 0;
    button.disabled = !enabled || !play || count <= 0 || hero?.bombRemaining !== null;
    const badge = button.querySelector?.(".bomb-count");
    if (badge) badge.textContent = String(count);
  }
}

function leaveForMenu() {
  if (host.pauseAndSave('menu')) window.location.href = '../index.html';
  else host.foreground();
}

function bindControls() {
  runtime.inputs = new PointerActions(dispatchAction);
  for (const button of document.querySelectorAll("[data-action]")) {
    button.addEventListener("pointerdown", (event) => {
      event.preventDefault();
      event.stopPropagation();
      if (button.disabled) return;
      button.setPointerCapture(event.pointerId);
      runtime.inputs.down(
        event.pointerId,
        Number(button.dataset.player),
        button.dataset.action,
      );
    });
    for (const eventName of [
      "pointerup",
      "pointercancel",
      "lostpointercapture",
    ])
      button.addEventListener(eventName, (event) => {
        event.preventDefault();
        runtime.inputs.up(event.pointerId);
        if (button.hasPointerCapture(event.pointerId))
          button.releasePointerCapture(event.pointerId);
      });
    button.addEventListener("contextmenu", (event) => event.preventDefault());
  }
  document.getElementById("mode-menu")?.addEventListener("click", leaveForMenu);
}

function bindMine(canvas) {
  canvas.addEventListener("pointerdown", event => {
    if (runtime.current?.sys.settings.key !== "Play") return;
    event.preventDefault();
    event.stopPropagation();
    if (runtime.paused || runtime.reflowing || runtime.transitionState || runtime.blocked) return;
    const player = minePlayer(event.clientX, event.clientY, canvas.getBoundingClientRect(),
      runtime.current.gameWidth, runtime.current.hudBottom, safeInsets());
    if (player === null) return;
    canvas.setPointerCapture(event.pointerId);
    runtime.inputs.down(event.pointerId, player, "drop");
  });
  for (const name of ["pointerup", "pointercancel", "lostpointercapture"])
    canvas.addEventListener(name, event => {
      runtime.inputs.up(event.pointerId);
      if (canvas.hasPointerCapture(event.pointerId)) canvas.releasePointerCapture(event.pointerId);
    });
  canvas.addEventListener("contextmenu", event => event.preventDefault());
}

function boot() {
  bindControls();
  window.addEventListener?.("resize", () => runtime.reflow());
  window.addEventListener?.("classic-insets", () => runtime.reflow());
  const query = new URLSearchParams(window.location.search);
  try {
    if (query.get("new") === "1") runtime.active = true;
    else {
      const saved = host.read("duo");
      runtime.existing = saved !== null;
      if (query.get("resume") === "1") {
        if (!saved)
          throw new Error("没有可继续的双人存档，请返回模式选择开始新游戏。");
        runtime.pending = validateSnapshot(saved);
        runtime.run = clone(runtime.pending.run);
        runtime.active = true;
      }
    }
  } catch (error) {
    runtime.blocked = true;
    const message = document.getElementById("load-error");
    if (message) {
      message.hidden = false;
      message.textContent = error.message;
    }
    host.message(error.message);
  }
  host.register({
    mode: "duo",
    capture: () => runtime.capture(),
    pause: () => runtime.pause(),
    resume: () => runtime.resume(),
  });
  if (runtime.blocked) return;
  runtime.game = new Phaser.Game({
    type: Phaser.AUTO,
    scene: [
      PreloaderScene,
      StartScene,
      TargetScene,
      FailScene,
      ShopScene,
      PlayScene,
    ],
    physics: {},
    scale: {
      mode: Phaser.Scale.FIT,
      autoCenter: Phaser.Scale.CENTER_BOTH,
      parent: "app",
      width: 550,
      height: 400,
    },
    input: { activePointers: 4 },
    autoFocus: false,
    render: { pixelArt: false, antialias: true },
    callbacks: {
      postBoot: (game) => {
        runtime.game = game;
        bindMine(game.canvas);
        game.sound.mute = host.soundEnabled === false;
        if (runtime.paused) runtime.pause();
      },
    },
  });
}

if (document.readyState === "loading")
  document.addEventListener("DOMContentLoaded", boot, { once: true });
else boot();
