if (this.mouseDown || this.gameOverBool || this.pauseScrite || window.XanaduFruitContent.isPaused()) return;
this.mouseDown = true;
this.curTouchId = e.touchId;
this.contiCutFruitNum = 0;
Laya.stage.on(Laya.Event.MOUSE_MOVE, this, this.onMove);
this.inputX = Laya.stage.mouseX * Laya.stage.clientScaleX;
this.inputY = Laya.stage.mouseY * Laya.stage.clientScaleY;
this.prePos = new Laya.Point(this.inputX, this.inputY);
this.effectPos(this.inputX, this.inputY);
