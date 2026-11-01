this.list.array = Array.from({ length: 27 }, function (_, index) {
  return {
    icon: { skin: 'ui2/shop/icon' + (index + 1) + '.png', clickHandler: new Laya.Handler(this, this.ClickButton, [index]) },
    kuang: { skin: 'ui2/shop/kuang' + (index % 6 + 1) + '.png', alpha: this.useDaoId === index ? 1 : 0.7 },
    huodeiBtn: { skin: 'ui2/ButtonYellow.png', clickHandler: new Laya.Handler(this, this.ClickButton, [index]) },
    wenzi: { text: this.useDaoId === index ? '使用中' : '使用' }
  };
}, this);
