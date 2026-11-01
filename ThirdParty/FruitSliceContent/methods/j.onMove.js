if (!this.mouseDown || e.touchId !== this.curTouchId || this.gameOverBool || this.pauseScrite || window.XanaduFruitContent.isPaused()) return;
this.inputX = Laya.stage.mouseX * Laya.stage.clientScaleX;
this.inputY = Laya.stage.mouseY * Laya.stage.clientScaleY;
this.mouseMove();
