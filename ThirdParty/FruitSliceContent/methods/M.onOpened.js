this.result = t.result;
this.model1Box.visible = N.model === 1;
this.model2Box.visible = N.model === 2;
this.model3Box.visible = N.model === 3;
if (N.model === 1) {
  this.successBox.visible = !!this.result;
  this.failBox.visible = !this.result;
  if (this.result) {
    N.PlaySound('chenggong');
    this.guanka = e.GetUserData('guanka');
    this.guankaIndex = e.GetUserData('guankaIndex');
    this.fenshu.value = N.qieshuiguoNum;
    this.xing1.visible = N.xxxNum !== 2;
    this.xing2.visible = N.xxxNum !== 1;
    this.xing3.visible = N.xxxNum !== 2;
    const stars = 3 - N.xxxNum;
    if (stars > this.guanka[N.selectMapIndex]) {
      this.guanka[N.selectMapIndex] = stars;
      e.SetUserData('guanka', this.guanka);
    }
    if (this.guankaIndex === N.selectMapIndex && this.guankaIndex < this.guanka.length - 1) {
      this.guankaIndex++;
      e.SetUserData('guankaIndex', this.guankaIndex);
    }
    this.mode1ani.play(0, false);
  } else {
    N.PlaySound('shibai');
    this.shibaiAin.play(0, true);
  }
} else if (N.model === 2) {
  N.PlaySound('chenggong');
  if (N.qieshuiguoNum > this.wujinModelHeightNum) {
    this.wujinModelHeightNum = N.qieshuiguoNum;
    e.SetUserData('wujinModelHeightNum', this.wujinModelHeightNum);
  }
  this.height2score.value = this.wujinModelHeightNum;
  this.fenshu2.value = N.qieshuiguoNum;
} else if (N.model === 3) {
  N.PlaySound('chenggong');
  if (N.qieshuiguoNum > this.timeModelHeightNum) {
    this.timeModelHeightNum = N.qieshuiguoNum;
    e.SetUserData('timeModelHeightNum', this.timeModelHeightNum);
  }
  this.height3score.value = this.timeModelHeightNum;
  this.fenshu3.value = N.qieshuiguoNum;
}
