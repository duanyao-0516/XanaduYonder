this.mouseDown = false;
this.curTouchId = null;
this.contiCutFruitNum = 0;
Laya.stage.off(Laya.Event.MOUSE_MOVE, this, this.onMove);
if (this.dao && !this.dao.destroyed) {
  this.dao.active = false;
  this.dao.transform.position = new Laya.Vector3(100,100,0);
  this.dao.active = true;
}
