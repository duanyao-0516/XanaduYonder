var __extends = function(a,b){function c(){this.constructor=a;}for(var d in b)if(b.hasOwnProperty(d))a[d]=b[d];c.prototype=b.prototype;a.prototype=new c;};
var     FadeInOut = function() {
        function a(a) {
            this.fadeColor = 0, this.game = a
        }
        return a.Init = function(b) {
            a.instance = new a(b)
        }, a.prototype.fadeTo = function(a) {
            this.createBkg(), this.spr_bg.alpha = 0, this.nextState = a, this.game.add.existing(this.spr_bg);
            var b = this.game.add.tween(this.spr_bg);
            b.to({
                alpha: 1
            }, 500, null), b.onComplete.add(this.changeState, this), b.start()
        }, a.prototype.createBkg = function() {
            this.spr_bg = this.game.make.graphics(0, 0), this.spr_bg.beginFill(this.fadeColor, 1), this.spr_bg.drawRect(ScaleManager.bkgX(), ScaleManager.bkgY(), ScaleManager.MaxWidth, ScaleManager.MaxHeight), this.spr_bg.endFill()
        }, a.prototype.changeState = function() {
            this.game.state.start(this.nextState)
        }, a.prototype.fadeOut = function() {
            this.createBkg(), this.spr_bg.alpha = 1, this.game.add.existing(this.spr_bg);
            var a = this.game.add.tween(this.spr_bg);
            a.to({
                alpha: 0
            }, 500, null), a.start()
        }, a
    }(),
    Helper;
! function(a) {
    var b = function() {
        function a() {}
        return a.create = function(b, c, d, e, f, g, h, i, j, k) {
            return void 0 === g && (g = 0), void 0 === h && (h = 0), void 0 === i && (i = 0), void 0 === j && (j = 0), void 0 === k && (k = !1), a._image = b.cache.getImage(e), "string" == typeof f ? a._frame = b.cache.getFrameByName(e, f) : a._frame = b.cache.getFrameByIndex(e, f), a.calculateNineImage(c, d, g, h, i, j, k), a._nineImage = new Phaser.BitmapData(b, "NineImage" + a._textureKey++, a._width, a._height), a.renderNineImage(), a._nineImage
        }, a.calculateNineImage = function(b, c, d, e, f, g, h) {
            var i = a._frame;
            if (a._centralWidth = i.width - e - g, a._centralHeight = i.height - d - f, h) a._horizontalRepeats = b, a._verticalRepeats = c, a._width = e + g + a._centralWidth * b, a._height = d + f + a._centralHeight * c, a._lastWidth = 0, a._lastHeight = 0;
            else {
                var j = b - e - g;
                a._horizontalRepeats = Math.floor(j / a._centralWidth), a._lastWidth = j % a._centralWidth;
                var k = c - d - f;
                a._verticalRepeats = Math.floor(k / a._centralHeight), a._lastHeight = k % a._centralHeight, a._width = b, a._height = c
            }
            a._leftWidth = e, a._rightWidth = g, a._topHeight = d, a._bottomHeight = f
        }, a.renderNineImage = function() {
            var b = a._frame.y,
                c = 0;
            a._topHeight > 0 && (a.renderNineImageRow(a._image, b, c, a._topHeight), b += a._topHeight, c += a._topHeight);
            for (var d = 0; d < a._verticalRepeats; d++) a.renderNineImageRow(a._image, b, c, a._centralHeight), c += a._centralHeight;
            a._lastHeight > 0 && (a.renderNineImageRow(a._image, b, c, a._lastHeight), c += a._lastHeight), b += a._centralHeight, a._bottomHeight > 0 && a.renderNineImageRow(a._image, b, c, a._bottomHeight)
        }, a.renderNineImageRow = function(b, c, d, e) {
            var f = a._frame.x,
                g = 0;
            a._leftWidth > 0 && (a._nineImage.copy(b, f, c, a._leftWidth, e, g, d), g += a._leftWidth, f += a._leftWidth);
            for (var h = 0; h < a._horizontalRepeats; h++) a._nineImage.copy(b, f, c, a._centralWidth, e, g, d), g += a._centralWidth;
            a._lastWidth > 0 && (a._nineImage.copy(b, f, c, a._lastWidth, e, g, d), g += a._lastWidth), f += a._centralWidth, a._rightWidth > 0 && a._nineImage.copy(b, f, c, a._rightWidth, e, g, d)
        }, a._textureKey = 0, a
    }();
    a.NineImage = b
}(Helper || (Helper = {}));
var SaveAPI = function() {
        function a(a) {
            this.game = a
        }
        return a.isCapable = function() {
            return !("undefined" == typeof window.localStorage)
        }, a.getAccess = function(a) {
            if (!this.isCapable()) return null;
            try {
                return JSON.parse(localStorage.getItem(a))
            } catch (b) {
                return window.localStorage.getItem(a)
            }
        }, a.setAccess = function(a, b) {
            if (!this.isCapable()) return null;
            try {
                var c = window.localStorage.getItem(a);
                (b > c || null == c) && window.localStorage.setItem(a, JSON.stringify(b))
            } catch (d) {
                d == DOMException.QUOTA_EXCEEDED_ERR
            }
        }, a.setForce = function(a, b) {
            if (!this.isCapable()) return null;
            try {
                window.localStorage.setItem(a, JSON.stringify(b))
            } catch (c) {
                c == DOMException.QUOTA_EXCEEDED_ERR
            }
        }, a.clear = function() {
            return this.isCapable() ? void window.localStorage.clear() : null
        }, a.SOUND = "GoldMinerSounds", a.UNIQ_ID = "GoldMinerUniq", a.SCORE = "GoldMinerMaxScore", a
    }(),
    ScaleManager = function() {
        function a() {}
        return a.SizeCalculation = function(b) {
            this.game = b;
            var c = a.GameWidth,
                d = a.GameHeight,
                e = Math.min(window.innerWidth / a.GameWidth, window.innerHeight / a.GameHeight);
            c = c * e < window.innerWidth ? window.innerWidth / e : c, d = d * e < window.innerHeight ? window.innerHeight / e : d, b.scale.setUserScale(e, e, 0, 0), b.scale.setGameSize(c, d), a.inSplash || b.world.setBounds(-(c - a.GameWidth) / 2, -(d - a.GameHeight) / 2, a.GameWidth, a.GameHeight), this.game.scale.refresh()
        }, a.bkgX = function() {
            return (a.GameWidth - a.MaxWidth) / 2
        }, a.bkgY = function() {
            return (a.GameHeight - a.MaxHeight) / 2
        }, a.checkOrientation = function(a, b) {
            void 0 === a && (a = !1), void 0 === b && (b = !1);
            var c = window.innerWidth,
                d = window.innerHeight;
            d > c && 400 > c || 400 > c && this.game.device.iPhone ? this.enterIncorrectOrientation() : this.leaveIncorrectOrientation()
        }, a.enterIncorrectOrientation = function() {
            document.getElementById("orientation").style.display = "block", document.getElementById("content").style.display = "none", this.game.paused = !0
        }, a.leaveIncorrectOrientation = function() {
            document.getElementById("orientation").style.display = "none", document.getElementById("content").style.display = "block", this.game.paused = !1
        }, a.GameWidth = 640, a.GameHeight = 480, a.MaxWidth = 1440, a.MaxHeight = 1457, a.inSplash = !0, a
    }(),
    LevelData = function() {
        function a() {}
        return a
    }(),
    SpriteData = function() {
        function a() {}
        return a
    }(),
    Levels = function() {
        function a() {}
        return a.levels = [{
            LevelName: "L1_1",
            bkg: "182.jpg",
            data: [{
                name: "sprite215.png",
                x: 406,
                y: 195,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite217.png",
                x: 94,
                y: 379,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite217.png",
                x: 325,
                y: 191,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 175,
                y: 240,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 128,
                y: 183,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite207.png",
                x: 322,
                y: 318,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 375,
                y: 178,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite220.png",
                x: 426,
                y: 137,
                scale: {
                    x: .7294158935546875,
                    y: .7294158935546875
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 261,
                y: 352,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite220.png",
                x: 107,
                y: 134,
                scale: {
                    x: .7294158935546875,
                    y: .7294158935546875
                },
                flip: !1
            }, {
                name: "sprite217.png",
                x: 522,
                y: 332,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite215.png",
                x: 65,
                y: 144,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 361,
                y: 265,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite207.png",
                x: 110,
                y: 264,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 420,
                y: 160,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }]
        }, {
            LevelName: "L1_2",
            bkg: "309.jpg",
            data: [{
                name: "sprite217.png",
                x: 128,
                y: 319,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite217.png",
                x: 325,
                y: 191,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 178,
                y: 181,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 107,
                y: 122,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite207.png",
                x: 537,
                y: 264,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 426,
                y: 213,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 425,
                y: 184,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite220.png",
                x: 460,
                y: 129,
                scale: {
                    x: .7294158935546875,
                    y: .7294158935546875
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 183,
                y: 353,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite220.png",
                x: 68,
                y: 170,
                scale: {
                    x: .7294158935546875,
                    y: .7294158935546875
                },
                flip: !1
            }, {
                name: "sprite217.png",
                x: 236,
                y: 288,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite215.png",
                x: 362,
                y: 356,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 398,
                y: 253,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite207.png",
                x: 71,
                y: 232,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 450,
                y: 150,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }]
        }, {
            LevelName: "L1_3",
            bkg: "310.jpg",
            data: [{
                name: "sprite217.png",
                x: 290,
                y: 285,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite217.png",
                x: 318,
                y: 236,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 178,
                y: 181,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 107,
                y: 122,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite207.png",
                x: 490,
                y: 243,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 256,
                y: 189,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 350,
                y: 172,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite220.png",
                x: 451,
                y: 154,
                scale: {
                    x: .7294158935546875,
                    y: .7294158935546875
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 229,
                y: 340,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite220.png",
                x: 107,
                y: 215,
                scale: {
                    x: .7294158935546875,
                    y: .7294158935546875
                },
                flip: !1
            }, {
                name: "sprite217.png",
                x: 181,
                y: 253,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite215.png",
                x: 184,
                y: 357,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 398,
                y: 253,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite207.png",
                x: 41,
                y: 227,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 425,
                y: 120,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }]
        }, {
            LevelName: "L2_1",
            bkg: "309.jpg",
            data: [{
                name: "sprite290.png",
                x: 403,
                y: 261,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite314.png",
                x: 493,
                y: 342,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite314.png",
                x: 44,
                y: 176,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite313.png",
                x: 473,
                y: 158,
                scale: {
                    x: .710845947265625,
                    y: .710845947265625
                },
                flip: !1
            }, {
                name: "sprite312.png",
                x: 112,
                y: 284,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite312.png",
                x: 480,
                y: 214,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite311.png",
                x: 373,
                y: 354,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite313.png",
                x: 411,
                y: 229,
                scale: {
                    x: .759033203125,
                    y: .759033203125
                },
                flip: !1
            }, {
                name: "sprite312.png",
                x: 373,
                y: 306,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite312.png",
                x: 436,
                y: 127,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite312.png",
                x: 268,
                y: 186,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite312.png",
                x: 338,
                y: 216,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite313.png",
                x: 232,
                y: 293,
                scale: {
                    x: .7592010498046875,
                    y: .7592010498046875
                },
                flip: !1
            }, {
                name: "sprite312.png",
                x: 191,
                y: 234,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 83,
                y: 190,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 310,
                y: 280,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 437,
                y: 360,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite215.png",
                x: 182,
                y: 337,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 165,
                y: 256,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite311.png",
                x: 36,
                y: 311,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }]
        }, {
            LevelName: "L2_2",
            bkg: "182.jpg",
            data: [{
                name: "sprite314.png",
                x: 343,
                y: 294,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite314.png",
                x: 491,
                y: 262,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite313.png",
                x: 116,
                y: 299,
                scale: {
                    x: .710845947265625,
                    y: .710845947265625
                },
                flip: !1
            }, {
                name: "sprite312.png",
                x: 429,
                y: 359,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite312.png",
                x: 480,
                y: 214,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite311.png",
                x: 50,
                y: 350,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite313.png",
                x: 179,
                y: 192,
                scale: {
                    x: .759033203125,
                    y: .759033203125
                },
                flip: !1
            }, {
                name: "sprite312.png",
                x: 373,
                y: 306,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite312.png",
                x: 436,
                y: 127,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite312.png",
                x: 354,
                y: 138,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite312.png",
                x: 374,
                y: 195,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite313.png",
                x: 103,
                y: 384,
                scale: {
                    x: .7592010498046875,
                    y: .7592010498046875
                },
                flip: !1
            }, {
                name: "sprite312.png",
                x: 321,
                y: 241,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 59,
                y: 129,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 23,
                y: 208,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 33,
                y: 276,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite217.png",
                x: 399,
                y: 256,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite215.png",
                x: 61,
                y: 170,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 165,
                y: 256,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite311.png",
                x: 497,
                y: 359,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }]
        }, {
            LevelName: "L2_3",
            bkg: "182.jpg",
            data: [{
                name: "sprite314.png",
                x: 493,
                y: 342,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite314.png",
                x: 33,
                y: 168,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite313.png",
                x: 116,
                y: 299,
                scale: {
                    x: .710845947265625,
                    y: .710845947265625
                },
                flip: !1
            }, {
                name: "sprite312.png",
                x: 66,
                y: 163,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite312.png",
                x: 480,
                y: 214,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite311.png",
                x: 175,
                y: 374,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite313.png",
                x: 397,
                y: 156,
                scale: {
                    x: .759033203125,
                    y: .759033203125
                },
                flip: !1
            }, {
                name: "sprite312.png",
                x: 373,
                y: 306,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite312.png",
                x: 436,
                y: 127,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite312.png",
                x: 354,
                y: 138,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite312.png",
                x: 374,
                y: 195,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite313.png",
                x: 232,
                y: 293,
                scale: {
                    x: .7592010498046875,
                    y: .7592010498046875
                },
                flip: !1
            }, {
                name: "sprite312.png",
                x: 191,
                y: 234,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 83,
                y: 190,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 319,
                y: 278,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 33,
                y: 276,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite217.png",
                x: 399,
                y: 256,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite215.png",
                x: 248,
                y: 324,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 165,
                y: 256,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite311.png",
                x: 64,
                y: 348,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }]
        }, {
            LevelName: "L3_1",
            bkg: "310.jpg",
            data: [{
                name: "sprite290.png",
                x: 429,
                y: 299,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite220.png",
                x: 258,
                y: 196,
                scale: {
                    x: .7294158935546875,
                    y: .7294158935546875
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 476,
                y: 189,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 345,
                y: 249,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite217.png",
                x: 73,
                y: 248,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite217.png",
                x: 132,
                y: 311,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 99,
                y: 121,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 379,
                y: 200,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite207.png",
                x: 301,
                y: 365,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 99,
                y: 153,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 134,
                y: 124,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite220.png",
                x: 410,
                y: 231,
                scale: {
                    x: .7294158935546875,
                    y: .7294158935546875
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 103,
                y: 277,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite220.png",
                x: 350,
                y: 313,
                scale: {
                    x: .7294158935546875,
                    y: .7294158935546875
                },
                flip: !1
            }, {
                name: "sprite217.png",
                x: 509,
                y: 207,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite215.png",
                x: 80,
                y: 182,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 479,
                y: 294,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }]
        }, {
            LevelName: "L3_2",
            bkg: "310.jpg",
            data: [{
                name: "sprite290.png",
                x: 429,
                y: 261,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite220.png",
                x: 280,
                y: 196,
                scale: {
                    x: .7294158935546875,
                    y: .7294158935546875
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 462,
                y: 229,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 387,
                y: 257,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite217.png",
                x: 372,
                y: 353,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite217.png",
                x: 76,
                y: 223,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 99,
                y: 121,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 379,
                y: 200,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite207.png",
                x: 168,
                y: 393,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 99,
                y: 153,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 134,
                y: 124,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite220.png",
                x: 410,
                y: 231,
                scale: {
                    x: .7294158935546875,
                    y: .7294158935546875
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 103,
                y: 277,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite220.png",
                x: 412,
                y: 301,
                scale: {
                    x: .7294158935546875,
                    y: .7294158935546875
                },
                flip: !1
            }, {
                name: "sprite217.png",
                x: 509,
                y: 207,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite215.png",
                x: 62,
                y: 141,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 469,
                y: 277,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }]
        }, {
            LevelName: "L3_3",
            bkg: "309.jpg",
            data: [{
                name: "sprite290.png",
                x: 439,
                y: 298,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite220.png",
                x: 250,
                y: 196,
                scale: {
                    x: .7294158935546875,
                    y: .7294158935546875
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 474,
                y: 228,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 372,
                y: 259,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite217.png",
                x: 423,
                y: 243,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite217.png",
                x: 76,
                y: 223,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 99,
                y: 121,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 379,
                y: 200,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite207.png",
                x: 7,
                y: 305,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 99,
                y: 153,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 134,
                y: 124,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite220.png",
                x: 296,
                y: 235,
                scale: {
                    x: .7294158935546875,
                    y: .7294158935546875
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 115,
                y: 283,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite220.png",
                x: 404,
                y: 309,
                scale: {
                    x: .7294158935546875,
                    y: .7294158935546875
                },
                flip: !1
            }, {
                name: "sprite217.png",
                x: 509,
                y: 207,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite215.png",
                x: 333,
                y: 253,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 476,
                y: 277,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }]
        }, {
            LevelName: "L4_1",
            bkg: "182.jpg",
            data: [{
                name: "sprite319.png",
                x: 565,
                y: 270,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite319.png",
                x: -16,
                y: 205,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !0
            }, {
                name: "sprite319.png",
                x: 183,
                y: 280,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !0
            }, {
                name: "sprite215.png",
                x: 151,
                y: 242,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite215.png",
                x: 383,
                y: 257,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite316.png",
                x: 170,
                y: 318,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite316.png",
                x: 327,
                y: 329,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite215.png",
                x: 429,
                y: 202,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite220.png",
                x: 337,
                y: 259,
                scale: {
                    x: .7294158935546875,
                    y: .7294158935546875
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 466,
                y: 182,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 143,
                y: 162,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite217.png",
                x: 326,
                y: 207,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 382,
                y: 179,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 390,
                y: 203,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 368,
                y: 165,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 113,
                y: 139,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 113,
                y: 345,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite215.png",
                x: 252,
                y: 322,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }]
        }, {
            LevelName: "L4_2",
            bkg: "309.jpg",
            data: [{
                name: "sprite319.png",
                x: 383,
                y: 141,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite319.png",
                x: 68,
                y: 204,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !0
            }, {
                name: "sprite319.png",
                x: 266,
                y: 273,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !0
            }, {
                name: "sprite316.png",
                x: 120,
                y: 332,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite316.png",
                x: 329,
                y: 311,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite215.png",
                x: 417,
                y: 170,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite220.png",
                x: 280,
                y: 196,
                scale: {
                    x: .7294158935546875,
                    y: .7294158935546875
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 466,
                y: 182,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 143,
                y: 160,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite217.png",
                x: 345,
                y: 216,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite217.png",
                x: 68,
                y: 238,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 174,
                y: 136,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 379,
                y: 200,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 97,
                y: 132,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 203,
                y: 185,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 172,
                y: 360,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite217.png",
                x: 518,
                y: 178,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite215.png",
                x: 220,
                y: 241,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }]
        }, {
            LevelName: "L4_3",
            bkg: "309.jpg",
            data: [{
                name: "sprite319.png",
                x: 438,
                y: 153,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite319.png",
                x: 108,
                y: 186,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !0
            }, {
                name: "sprite319.png",
                x: 150,
                y: 129,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !0
            }, {
                name: "sprite316.png",
                x: 120,
                y: 332,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite316.png",
                x: 329,
                y: 311,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite215.png",
                x: 430,
                y: 275,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite220.png",
                x: 278,
                y: 291,
                scale: {
                    x: .7294158935546875,
                    y: .7294158935546875
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 482,
                y: 276,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 77,
                y: 283,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite217.png",
                x: 353,
                y: 266,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite217.png",
                x: 36,
                y: 288,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 190,
                y: 279,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 380,
                y: 249,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 34,
                y: 181,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 163,
                y: 288,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 172,
                y: 360,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite217.png",
                x: 510,
                y: 246,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite215.png",
                x: 238,
                y: 312,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }]
        }, {
            LevelName: "L5_1",
            bkg: "310.jpg",
            data: [{
                name: "sprite319.png",
                x: -72,
                y: 286,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !0
            }, {
                name: "sprite319.png",
                x: 504,
                y: 281,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite319.png",
                x: 73,
                y: 198,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !0
            }, {
                name: "sprite290.png",
                x: 487,
                y: 355,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite207.png",
                x: 563,
                y: 296,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite207.png",
                x: 0,
                y: 334,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 70,
                y: 328,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 445,
                y: 349,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 498,
                y: 306,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite316.png",
                x: 185,
                y: 358,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite220.png",
                x: 512,
                y: 343,
                scale: {
                    x: .7294158935546875,
                    y: .7294158935546875
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 425,
                y: 200,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 310,
                y: 194,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite217.png",
                x: 206,
                y: 229,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite217.png",
                x: 68,
                y: 238,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 143,
                y: 137,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 347,
                y: 254,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 99,
                y: 126,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 118,
                y: 306,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 470,
                y: 391,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite217.png",
                x: 458,
                y: 175,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite215.png",
                x: 390,
                y: 338,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }]
        }, {
            LevelName: "L5_2",
            bkg: "310.jpg",
            data: [{
                name: "sprite319.png",
                x: -72,
                y: 286,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !0
            }, {
                name: "sprite319.png",
                x: 452,
                y: 282,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite319.png",
                x: 135,
                y: 200,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !0
            }, {
                name: "sprite207.png",
                x: 567,
                y: 310,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite207.png",
                x: 0,
                y: 334,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 70,
                y: 328,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 445,
                y: 349,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 498,
                y: 306,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite316.png",
                x: 185,
                y: 358,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite316.png",
                x: 327,
                y: 332,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite220.png",
                x: 309,
                y: 256,
                scale: {
                    x: .7294158935546875,
                    y: .7294158935546875
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 425,
                y: 200,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 115,
                y: 158,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite217.png",
                x: 206,
                y: 229,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite217.png",
                x: 68,
                y: 238,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 143,
                y: 137,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 347,
                y: 254,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 99,
                y: 126,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 118,
                y: 306,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 177,
                y: 269,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite217.png",
                x: 458,
                y: 175,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite215.png",
                x: 390,
                y: 338,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }]
        }, {
            LevelName: "L5_3",
            bkg: "182.jpg",
            data: [{
                name: "sprite319.png",
                x: -63,
                y: 289,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !0
            }, {
                name: "sprite319.png",
                x: 434,
                y: 276,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite319.png",
                x: 135,
                y: 200,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !0
            }, {
                name: "sprite207.png",
                x: 437,
                y: 389,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite207.png",
                x: 0,
                y: 334,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 256,
                y: 299,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 23,
                y: 273,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 511,
                y: 247,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite316.png",
                x: 220,
                y: 383,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite316.png",
                x: 327,
                y: 332,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite220.png",
                x: 309,
                y: 256,
                scale: {
                    x: .7294158935546875,
                    y: .7294158935546875
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 425,
                y: 200,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 115,
                y: 158,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite217.png",
                x: 206,
                y: 229,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite217.png",
                x: 68,
                y: 238,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 236,
                y: 277,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 347,
                y: 254,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 78,
                y: 124,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 118,
                y: 306,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 167,
                y: 268,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite217.png",
                x: 458,
                y: 175,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite215.png",
                x: 384,
                y: 183,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }]
        }, {
            LevelName: "L6_1",
            bkg: "182.jpg",
            data: [{
                name: "sprite321.png",
                x: 402,
                y: 377,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite321.png",
                x: 80,
                y: 197,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !0
            }, {
                name: "sprite321.png",
                x: -118,
                y: 157,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !0
            }, {
                name: "sprite321.png",
                x: 713,
                y: 183,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite207.png",
                x: 284,
                y: 310,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 424,
                y: 127,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 473,
                y: 230,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 415,
                y: 276,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite220.png",
                x: 518,
                y: 309,
                scale: {
                    x: .7294158935546875,
                    y: .7294158935546875
                },
                flip: !1
            }, {
                name: "sprite220.png",
                x: 123,
                y: 328,
                scale: {
                    x: .7294158935546875,
                    y: .7294158935546875
                },
                flip: !1
            }, {
                name: "sprite215.png",
                x: 286,
                y: 248,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite220.png",
                x: 442,
                y: 344,
                scale: {
                    x: .7294158935546875,
                    y: .7294158935546875
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 30,
                y: 289,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 230,
                y: 331,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite217.png",
                x: 220,
                y: 171,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite217.png",
                x: 21,
                y: 214,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 170,
                y: 284,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 357,
                y: 169,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 97,
                y: 228,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 70,
                y: 140,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 336,
                y: 333,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite217.png",
                x: 469,
                y: 318,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }]
        }, {
            LevelName: "L6_2",
            bkg: "182.jpg",
            data: [{
                name: "sprite321.png",
                x: 414,
                y: 323,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite321.png",
                x: 154,
                y: 298,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !0
            }, {
                name: "sprite321.png",
                x: 50,
                y: 186,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !0
            }, {
                name: "sprite321.png",
                x: 444,
                y: 200,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite207.png",
                x: 552,
                y: 347,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 465,
                y: 272,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 422,
                y: 253,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 415,
                y: 276,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite220.png",
                x: 441,
                y: 132,
                scale: {
                    x: .7294158935546875,
                    y: .7294158935546875
                },
                flip: !1
            }, {
                name: "sprite220.png",
                x: 112,
                y: 273,
                scale: {
                    x: .7294158935546875,
                    y: .7294158935546875
                },
                flip: !1
            }, {
                name: "sprite215.png",
                x: 478,
                y: 202,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite220.png",
                x: 357,
                y: 168,
                scale: {
                    x: .7294158935546875,
                    y: .7294158935546875
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 107,
                y: 149,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 225,
                y: 254,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite217.png",
                x: 388,
                y: 255,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite217.png",
                x: 82,
                y: 360,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 170,
                y: 284,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 437,
                y: 266,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 97,
                y: 228,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 195,
                y: 349,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 323,
                y: 370,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite217.png",
                x: 373,
                y: 378,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }]
        }, {
            LevelName: "L6_3",
            bkg: "322.jpg",
            data: [{
                name: "sprite321.png",
                x: 413,
                y: 314,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite321.png",
                x: 212,
                y: 234,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !0
            }, {
                name: "sprite321.png",
                x: 43,
                y: 200,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !0
            }, {
                name: "sprite321.png",
                x: 532,
                y: 199,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite207.png",
                x: 246,
                y: 371,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 491,
                y: 315,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 342,
                y: 283,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 426,
                y: 298,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite220.png",
                x: 411,
                y: 168,
                scale: {
                    x: .7294158935546875,
                    y: .7294158935546875
                },
                flip: !1
            }, {
                name: "sprite220.png",
                x: 95,
                y: 275,
                scale: {
                    x: .7294158935546875,
                    y: .7294158935546875
                },
                flip: !1
            }, {
                name: "sprite215.png",
                x: 451,
                y: 320,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite220.png",
                x: 210,
                y: 174,
                scale: {
                    x: .7294158935546875,
                    y: .7294158935546875
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 53,
                y: 165,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 305,
                y: 267,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite217.png",
                x: 23,
                y: 320,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite217.png",
                x: 82,
                y: 360,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 438,
                y: 353,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 446,
                y: 285,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 124,
                y: 290,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 195,
                y: 349,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 527,
                y: 270,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite217.png",
                x: 377,
                y: 378,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }]
        }, {
            LevelName: "L7_1",
            bkg: "322.jpg",
            data: [{
                name: "sprite332.png",
                x: 442,
                y: 170,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite332.png",
                x: 136,
                y: 278,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite319.png",
                x: 494,
                y: 221,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite319.png",
                x: 532,
                y: 142,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite319.png",
                x: 270,
                y: 261,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite319.png",
                x: 88,
                y: 325,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !0
            }, {
                name: "sprite319.png",
                x: 23,
                y: 239,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !0
            }, {
                name: "sprite319.png",
                x: 330,
                y: 122,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !0
            }, {
                name: "sprite316.png",
                x: 525,
                y: 175,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite316.png",
                x: 187,
                y: 311,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite207.png",
                x: 23,
                y: 356,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite207.png",
                x: 208,
                y: 391,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite207.png",
                x: 444,
                y: 333,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 294,
                y: 174,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite215.png",
                x: 417,
                y: 250,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 108,
                y: 175,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite324.png",
                x: 408,
                y: 189,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite326.png",
                x: 395,
                y: 201,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite326.png",
                x: 168,
                y: 229,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite324.png",
                x: 70,
                y: 254,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }]
        }, {
            LevelName: "L7_2",
            bkg: "310.jpg",
            data: [{
                name: "sprite332.png",
                x: 364,
                y: 220,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite332.png",
                x: 161,
                y: 222,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite319.png",
                x: 366,
                y: 160,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite319.png",
                x: 529,
                y: 137,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite319.png",
                x: 473,
                y: 267,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite319.png",
                x: 126,
                y: 341,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !0
            }, {
                name: "sprite319.png",
                x: 67,
                y: 272,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !0
            }, {
                name: "sprite319.png",
                x: 20,
                y: 145,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !0
            }, {
                name: "sprite316.png",
                x: 500,
                y: 193,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite316.png",
                x: 184,
                y: 304,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite207.png",
                x: 23,
                y: 356,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite207.png",
                x: 257,
                y: 386,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite207.png",
                x: 339,
                y: 310,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 295,
                y: 271,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite215.png",
                x: 121,
                y: 202,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 13,
                y: 237,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite324.png",
                x: 64,
                y: 176,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite326.png",
                x: 423,
                y: 184,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite326.png",
                x: 66,
                y: 319,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite324.png",
                x: 214,
                y: 235,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }]
        }, {
            LevelName: "L7_3",
            bkg: "310.jpg",
            data: [{
                name: "sprite332.png",
                x: 350,
                y: 303,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite332.png",
                x: 231,
                y: 304,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite319.png",
                x: 477,
                y: 226,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite319.png",
                x: 574,
                y: 136,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite319.png",
                x: 634,
                y: 283,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite319.png",
                x: 26,
                y: 273,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !0
            }, {
                name: "sprite319.png",
                x: 123,
                y: 207,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !0
            }, {
                name: "sprite319.png",
                x: 176,
                y: 143,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !0
            }, {
                name: "sprite316.png",
                x: 525,
                y: 175,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite316.png",
                x: 55,
                y: 145,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite207.png",
                x: 165,
                y: 337,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite207.png",
                x: 293,
                y: 369,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite207.png",
                x: 416,
                y: 342,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 294,
                y: 174,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite215.png",
                x: 439,
                y: 200,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 74,
                y: 187,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite324.png",
                x: 408,
                y: 189,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite326.png",
                x: 395,
                y: 201,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite326.png",
                x: 130,
                y: 238,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite324.png",
                x: 436,
                y: 278,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }]
        }, {
            LevelName: "L8_1",
            bkg: "309.jpg",
            data: [{
                name: "sprite332.png",
                x: 480,
                y: 358,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite332.png",
                x: 452,
                y: 171,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite332.png",
                x: 365,
                y: 274,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite332.png",
                x: 153,
                y: 263,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite332.png",
                x: 60,
                y: 211,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite321.png",
                x: 101,
                y: 261,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !0
            }, {
                name: "sprite321.png",
                x: 561,
                y: 160,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite321.png",
                x: 23,
                y: 203,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !0
            }, {
                name: "sprite290.png",
                x: 324,
                y: 207,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 209,
                y: 173,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 107,
                y: 145,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 71,
                y: 283,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 501,
                y: 123,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 331,
                y: 342,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 202,
                y: 298,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 407,
                y: 243,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite215.png",
                x: 49,
                y: 364,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite215.png",
                x: 522,
                y: 313,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }]
        }, {
            LevelName: "L8_2",
            bkg: "309.jpg",
            data: [{
                name: "sprite332.png",
                x: 405,
                y: 318,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite332.png",
                x: 136,
                y: 306,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite332.png",
                x: 199,
                y: 234,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite332.png",
                x: 346,
                y: 232,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite332.png",
                x: 275,
                y: 163,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite321.png",
                x: 106,
                y: 292,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !0
            }, {
                name: "sprite321.png",
                x: 391,
                y: 166,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite321.png",
                x: 163,
                y: 222,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !0
            }, {
                name: "sprite290.png",
                x: 329,
                y: 197,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 243,
                y: 145,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 318,
                y: 371,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 271,
                y: 372,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 221,
                y: 373,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 95,
                y: 265,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 294,
                y: 133,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 422,
                y: 284,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite215.png",
                x: 21,
                y: 308,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite215.png",
                x: 524,
                y: 300,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }]
        }, {
            LevelName: "L8_3",
            bkg: "182.jpg",
            data: [{
                name: "sprite332.png",
                x: 58,
                y: 200,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite332.png",
                x: 489,
                y: 198,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 55,
                y: 363,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 79,
                y: 351,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 532,
                y: 296,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite207.png",
                x: 527,
                y: 375,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite207.png",
                x: 410,
                y: 316,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite207.png",
                x: 270,
                y: 335,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite207.png",
                x: 139,
                y: 376,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite207.png",
                x: 35,
                y: 313,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 141,
                y: 177,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite220.png",
                x: 306,
                y: 164,
                scale: {
                    x: .7294158935546875,
                    y: .7294158935546875
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 330,
                y: 288,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite220.png",
                x: 241,
                y: 278,
                scale: {
                    x: .7294158935546875,
                    y: .7294158935546875
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 219,
                y: 216,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 542,
                y: 236,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite220.png",
                x: 471,
                y: 256,
                scale: {
                    x: .7294158935546875,
                    y: .7294158935546875
                },
                flip: !1
            }, {
                name: "sprite220.png",
                x: 429,
                y: 206,
                scale: {
                    x: .7294158935546875,
                    y: .7294158935546875
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 510,
                y: 130,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite220.png",
                x: 36,
                y: 140,
                scale: {
                    x: .7294158935546875,
                    y: .7294158935546875
                },
                flip: !1
            }, {
                name: "sprite220.png",
                x: 30,
                y: 248,
                scale: {
                    x: .7294158935546875,
                    y: .7294158935546875
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 142,
                y: 230,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 402,
                y: 238,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 106,
                y: 260,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 421,
                y: 157,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite220.png",
                x: 137,
                y: 203,
                scale: {
                    x: .7294158935546875,
                    y: .7294158935546875
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 118,
                y: 156,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }]
        }, {
            LevelName: "L9_1",
            bkg: "310.jpg",
            data: [{
                name: "sprite332.png",
                x: 8,
                y: 156,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite332.png",
                x: 548,
                y: 124,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite332.png",
                x: 550,
                y: 258,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite332.png",
                x: 465,
                y: 366,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite332.png",
                x: 316,
                y: 380,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite332.png",
                x: 162,
                y: 382,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite332.png",
                x: 37,
                y: 304,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite321.png",
                x: 142,
                y: 278,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !0
            }, {
                name: "sprite321.png",
                x: 71,
                y: 224,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !0
            }, {
                name: "sprite321.png",
                x: 378,
                y: 246,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite321.png",
                x: 480,
                y: 172,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 254,
                y: 153,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite326.png",
                x: 286,
                y: 309,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite324.png",
                x: 487,
                y: 331,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 544,
                y: 186,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 111,
                y: 372,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 527,
                y: 332,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 65,
                y: 255,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 69,
                y: 378,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 495,
                y: 203,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 432,
                y: 262,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite215.png",
                x: 518,
                y: 277,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite215.png",
                x: 207,
                y: 353,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite215.png",
                x: 21,
                y: 206,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite207.png",
                x: 401,
                y: 331,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite207.png",
                x: 117,
                y: 322,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }]
        }, {
            LevelName: "L9_2",
            bkg: "182.jpg",
            data: [{
                name: "sprite332.png",
                x: 123,
                y: 153,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite332.png",
                x: 413,
                y: 146,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite332.png",
                x: 208,
                y: 264,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite332.png",
                x: 517,
                y: 362,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite332.png",
                x: 328,
                y: 264,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite332.png",
                x: 266,
                y: 265,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite332.png",
                x: 26,
                y: 360,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite321.png",
                x: 144,
                y: 268,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !0
            }, {
                name: "sprite321.png",
                x: 57,
                y: 199,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !0
            }, {
                name: "sprite321.png",
                x: 389,
                y: 254,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite321.png",
                x: 469,
                y: 191,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 109,
                y: 117,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite326.png",
                x: 295,
                y: 328,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite324.png",
                x: 487,
                y: 331,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 297,
                y: 229,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 221,
                y: 384,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 511,
                y: 317,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 65,
                y: 255,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 313,
                y: 385,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 384,
                y: 167,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 236,
                y: 229,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite215.png",
                x: 518,
                y: 277,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite215.png",
                x: 269,
                y: 384,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite215.png",
                x: 17,
                y: 206,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite207.png",
                x: 401,
                y: 331,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite207.png",
                x: 117,
                y: 322,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }]
        }, {
            LevelName: "L9_3",
            bkg: "309.jpg",
            data: [{
                name: "sprite332.png",
                x: 268,
                y: 243,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite319.png",
                x: 201,
                y: 129,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !0
            }, {
                name: "sprite319.png",
                x: 205,
                y: 235,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite321.png",
                x: 45,
                y: 345,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !0
            }, {
                name: "sprite319.png",
                x: 102,
                y: 275,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !0
            }, {
                name: "sprite319.png",
                x: 372,
                y: 197,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite319.png",
                x: 565,
                y: 142,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite319.png",
                x: -18,
                y: 152,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !0
            }, {
                name: "sprite321.png",
                x: 504,
                y: 338,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite321.png",
                x: 339,
                y: 252,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !0
            }, {
                name: "sprite321.png",
                x: -62,
                y: 317,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !0
            }, {
                name: "sprite321.png",
                x: 54,
                y: 196,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !0
            }, {
                name: "sprite321.png",
                x: 382,
                y: 303,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite321.png",
                x: 595,
                y: 183,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 118,
                y: 178,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite215.png",
                x: 268,
                y: 374,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }]
        }, {
            LevelName: "L10_1",
            bkg: "182.jpg",
            data: [{
                name: "sprite210.png",
                x: 325,
                y: 316,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 124,
                y: 305,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 395,
                y: 153,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 267,
                y: 204,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite210.png",
                x: 126,
                y: 161,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 117,
                y: 225,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 47,
                y: 183,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite316.png",
                x: 425,
                y: 372,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite217.png",
                x: 512,
                y: 307,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 434,
                y: 169,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 383,
                y: 226,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 323,
                y: 230,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite217.png",
                x: 362,
                y: 341,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite217.png",
                x: 228,
                y: 289,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite316.png",
                x: 472,
                y: 203,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite316.png",
                x: 307,
                y: 276,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite217.png",
                x: 168,
                y: 339,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite207.png",
                x: 526,
                y: 379,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite207.png",
                x: 543,
                y: 271,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite207.png",
                x: 412,
                y: 282,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite207.png",
                x: 247,
                y: 358,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite316.png",
                x: 142,
                y: 385,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 126,
                y: 279,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite217.png",
                x: 95,
                y: 268,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite316.png",
                x: 38,
                y: 246,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite207.png",
                x: 67,
                y: 322,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }]
        }, {
            LevelName: "L10_2",
            bkg: "322.jpg",
            data: [{
                name: "sprite332.png",
                x: 46,
                y: 155,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite332.png",
                x: 377,
                y: 166,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite332.png",
                x: 172,
                y: 156,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite332.png",
                x: 332,
                y: 354,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite332.png",
                x: 465,
                y: 240,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite332.png",
                x: 36,
                y: 319,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite332.png",
                x: 222,
                y: 264,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite321.png",
                x: 142,
                y: 273,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !0
            }, {
                name: "sprite321.png",
                x: 71,
                y: 226,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !0
            }, {
                name: "sprite321.png",
                x: 378,
                y: 246,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite321.png",
                x: 480,
                y: 169,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 254,
                y: 153,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite326.png",
                x: 377,
                y: 273,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite324.png",
                x: 464,
                y: 316,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 298,
                y: 346,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 118,
                y: 371,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 56,
                y: 275,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 190,
                y: 201,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 69,
                y: 378,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 495,
                y: 203,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 432,
                y: 262,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite215.png",
                x: 451,
                y: 368,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite215.png",
                x: 226,
                y: 328,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite215.png",
                x: 80,
                y: 174,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite207.png",
                x: 401,
                y: 331,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite207.png",
                x: 117,
                y: 322,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }]
        }, {
            LevelName: "L10_3",
            bkg: "309.jpg",
            data: [{
                name: "sprite332.png",
                x: 181,
                y: 181,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite332.png",
                x: 548,
                y: 284,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite332.png",
                x: 353,
                y: 310,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite332.png",
                x: 465,
                y: 366,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite332.png",
                x: 265,
                y: 345,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite332.png",
                x: 162,
                y: 382,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite332.png",
                x: 56,
                y: 291,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite321.png",
                x: 142,
                y: 278,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !0
            }, {
                name: "sprite321.png",
                x: 71,
                y: 224,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !0
            }, {
                name: "sprite321.png",
                x: 378,
                y: 246,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite321.png",
                x: 480,
                y: 172,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite205.png",
                x: 254,
                y: 153,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite326.png",
                x: 286,
                y: 309,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite324.png",
                x: 487,
                y: 331,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 533,
                y: 205,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 158,
                y: 346,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 523,
                y: 344,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 93,
                y: 264,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 225,
                y: 214,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 495,
                y: 203,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite290.png",
                x: 432,
                y: 262,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite215.png",
                x: 497,
                y: 285,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite215.png",
                x: 207,
                y: 353,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite215.png",
                x: 25,
                y: 174,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite207.png",
                x: 416,
                y: 401,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }, {
                name: "sprite207.png",
                x: 117,
                y: 322,
                scale: {
                    x: 1,
                    y: 1
                },
                flip: !1
            }]
        }], a
    }(),
    Music = function() {
        function a(a) {
            this.game = a, this.boom = this.game.make.audio("boom", 1), this.down = this.game.make.audio("down", 1), this.goal = this.game.make.audio("goal", 1), this.hvBad = this.game.make.audio("hvBad", 1), this.hvCool = this.game.make.audio("hvCool", 1), this.hvGood = this.game.make.audio("hvGood", 1), this.scoreAdd = this.game.make.audio("scoreAdd", 1), this.up = this.game.make.audio("up", 1), this.upfinish = this.game.make.audio("upfinish", 1), this.win = this.game.make.audio("win", 1);
            var b = SaveAPI.getAccess(SaveAPI.SOUND);
            null == b && (b = !0), this.SoundActive = b
        }
        return a.prototype.getNameObject = function(a) {
            if ("sound" == a) {
                if (this.SoundActive) return "soundOn.png";
                if (!this.SoundActive) return "soundOff.png"
            }
        }, a.prototype.statusSound = function(a) {
            this.SoundActive = !this.SoundActive, SaveAPI.setForce(SaveAPI.SOUND, this.SoundActive), a.frameName = this.getNameObject("sound")
        }, a.prototype.audioEffects = function(a) {
            this.SoundActive && (a.isPlaying || a.play())
        }, a
    }(),
    __extends = this && this.__extends || function(a, b) {
        function c() {
            this.constructor = a
        }
        for (var d in b) b.hasOwnProperty(d) && (a[d] = b[d]);
        a.prototype = null === b ? Object.create(b) : (c.prototype = b.prototype, new c)
    },
    Shop = function(a) {
        function b() {
            a.apply(this, arguments), this.descriptions = ["After you have grabbed onto something with your\nclaw, press the up arrow to throw a piece\nof dynamite at it and blow it up.", "Strength drink. The Miner will reel up objects a little\nfaster on the next level.\n The drink only lasts for one level.", "Lucky Clover. This will increase the chances of getting\nsomething good out of the grab bags on the next level.\nThis is only good for one level.", "Rock Collectors book. Rocks will be worth three times\nas much money on the next level. This is only good\n for one level.", "Diamond Polish. During the next level diamonds will\nbe worth more money. Only good for one level."]
        }
        return __extends(b, a), b.prototype.create = function() {
            var a = this;
            Game.bonuses.cloverBonus = !1, Game.bonuses.diamondBonus = !1, Game.bonuses.rockBonus = !1, Game.bonuses.strenght = 0, this.btns = [], this.yes = !1, this.game.add.tileSprite(ScaleManager.bkgX(), ScaleManager.bkgY(), ScaleManager.MaxWidth, ScaleManager.MaxHeight, b.KEY, "shopBkg.png");
            var c = this.game.make.sprite(this.game.world.width / 2, this.game.world.height, b.KEY, "table.png");
            c.anchor.set(.5, 1), this.descript = this.game.make.bitmapText(0, -60, "font", "", 22, "center"), this.descript.tint = 16776960, c.addChild(this.descript), this.man = this.game.add.sprite(c.x + c.width / 2 - 90, c.y - c.height + 15, b.KEY, "man1.png"), this.man.anchor.set(.5, 1), this.game.add.existing(c);
            var d = this.man.animations.add("stay", ["man0.png", "man1.png", "man2.png", "man1.png", "man0.png"], 30);
            d.onComplete.add(function() {
                var b = a.game.time.create(!0);
                b.add(5e3 * Math.random() + 1e3, a.man.animations.play, a.man.animations, "stay"), b.autoDestroy = !0, b.start()
            }), this.man.animations.play("stay");
            var e = this.game.add.sprite(this.game.world.width / 2 + 200, this.game.world.height / 2, b.KEY, "dialog.png");
            e.anchor.set(1, 1);
            var f = this.game.make.bitmapText(-e.width + 14, -e.height + 14, "font", 'Click on the items you want to buy. Click on\n"Next Level" when you are ready to go on.', 20);
            f.tint = 0, f.cacheAsBitmap = !0, e.addChild(f);
            var g = this.game.add.sprite(this.game.world.width / 2 - 60, this.game.world.height / 2 - 160, b.KEY, "goldMiner.png");
            g.anchor.set(.5, .5), g.scale.set(.8, .8);
            var h = this.game.add.button(g.x + g.width / 2 + 100, g.y, b.KEY, this.nextLevel, this, "btnNextOver.png", "btnNextUp.png", "btnNextUp.png", "btnNextUp.png");
            h.anchor.set(.5, .5), h.scale.set(1.4, 1.4), this.nextLevelBtn = h;
            var i = this.game.make.bitmapText(0, 0, "font", "Next Level", 18);
            i.tint = 65280, i.x -= i.width / 2, i.y -= i.height / 2, h.addChild(i);
            for (var j = 1; 6 > j; j++) {
                var k;
                ShopButton.canCreate(j) && (k = new ShopButton(this.game, 95 * j - 50, 300, j), k.name = j.toString(), this.btns[j] = k, this.game.add.existing(k), k.onInputOver.add(this.showDescript, this), k.onInputDown.add(this.buyItem, this))
            }
            var l = Helper.NineImage.create(this.game, this.world.game.width, 8, Game.KEY, "border.png", 3, 3, 3, 3, !1),
                m = new Phaser.Sprite(this.game, 0, 0, l.texture);
            m.anchor.set(0, 1), this.game.add.existing(m), m = new Phaser.Sprite(this.game, 0, this.game.world.height, l.texture), this.game.add.existing(m), l = Helper.NineImage.create(this.game, 8, this.game.world.height + 16, Game.KEY, "border.png", 3, 3, 3, 3, !1);
            var m = new Phaser.Sprite(this.game, 0, -8, l.texture);
            m.anchor.set(1, 0), this.game.add.existing(m);
            var m = new Phaser.Sprite(this.game, this.game.world.width, -8, l.texture);
            this.game.add.existing(m)
        }, b.prototype.nextLevel = function() {
            this.nextLevelBtn.inputEnabled = !1, this.man.animations.stop(), this.yes ? (this.man.frameName = "man4.png", Music.music.audioEffects(Music.music.scoreAdd)) : (this.man.frameName = "man3.png", Music.music.audioEffects(Music.music.hvBad)), Game.currentLevelNum++, Goal.TYPE = ScreenType.Goal, FadeInOut.instance.fadeTo("Goal")
        }, b.prototype.showDescript = function(a) {
            this.descript.text = this.descriptions[parseInt(a.name) - 1], this.descript.updateText(), this.descript.y = 0 - this.descript.height, this.descript.anchor.set(.5, .5)
        }, b.prototype.buyItem = function(a) {
            Music.music.audioEffects(Music.music.scoreAdd), this.yes = !0, a.kill();
            var a = this.btns[parseInt(a.name)],
                b = parseInt(a.name);
            Game.currentMoney -= a.price, 1 == b ? Game.bonuses.stick++ : 2 == b ? Game.bonuses.strenght = 2 : 3 == b ? Game.bonuses.cloverBonus = !0 : 4 == b ? Game.bonuses.rockBonus = !0 : 5 == b && (Game.bonuses.diamondBonus = !0)
        }, b.KEY = "atlas", b
    }(Phaser.State),
    ShopButton = function(a) {
        function b(c, d, e, f) {
            var g = "bonus" + f + ".png";
            a.call(this, c, d, e, b.KEY, this.onClickToMe, this, g, g, g, g), f = f, this.anchor.set(.5, .5), this.scale.set(1.1, 1.1), this.price = this.getPrice(f);
            var h = this.game.make.bitmapText(0, 51, "font", "$" + this.price, 24);
            h.tint = 39168, h.cacheAsBitmap = !0, h.x -= h.width / 2, h.y -= h.height / 2, this.addChild(h)
        }
        return __extends(b, a), b.canCreate = function(a) {
            var b = !0;
            return 1 == a ? Game.bonuses.stick >= 5 && (b = !1) : 2 == a ? BoulderData.random(10) + 1 >= 5 && (b = !1) : 3 == a ? BoulderData.random(10) + 1 >= 5 && (b = !1) : 4 == a ? BoulderData.random(10) + 1 >= 7 && (b = !1) : 5 == a && BoulderData.random(10) + 1 >= 6 && (b = !1), b
        }, b.prototype.onClickToMe = function() {}, b.prototype.getPrice = function(a) {
            var b = 0;
            return 1 == a ? (b = BoulderData.random(300) + 1, b += 2 * Game.currentLevelNum) : 2 == a ? b = BoulderData.random(300) + 100 : 3 == a ? (b = BoulderData.random(50 * Game.currentLevelNum) + 1, b += 2 * Game.currentLevelNum) : 4 == a ? b = BoulderData.random(150) + 1 : 5 == a && (b = BoulderData.random(100 * Game.currentLevelNum) + 1, b += 200), b
        }, b.KEY = "atlas", b
    }(Phaser.Button),
    Game = function(a) {
        function b() {
            a.apply(this, arguments)
        }
        return __extends(b, a), b.getGoal = function(a) {
            return 5 * (27 * a * a + 28 * a + 75)
        }, b.prototype.create = function() {
            var a = b.currentLevelNum;
            a -= 1, a >= b.totalLevels && (a = (b.currentLevelNum - 6) % b.totalLevels);
            var c = window.ClassicSoloRestore && window.ClassicSoloRestore.scene === "Game" ? window.ClassicSoloRestore.game.mapIndex : 3 * a + BoulderData.random(3),
                d = Levels.levels[c];
            this.classicMapIndex = c;
            this.classicBackground = this.game.add.sprite(0, 70, d.bkg), this.classicBackground.width = this.game.world.width, BoulderData.init();
            var e = Helper.NineImage.create(this.game, ScaleManager.GameWidth, 71, b.KEY, "upPanel.png", 5, 5, 5, 5, !1),
                f = new Phaser.Sprite(this.game, 0, 0, e.texture);
            this.classicHudPanel = f, this.game.add.existing(f), f = this.game.add.sprite(this.game.world.width / 2, 0, b.KEY, "blue.png"), f.anchor.set(.5, 0),  this.boulders = [], this.placeBoulders(d), this.placePod(), this.boom = this.game.make.sprite(0, 0, b.KEY), this.boom.anchor.set(.5, .5), this.boom.animations.add("dboom", ["dboom1.png", "dboom2.png", "dboom3.png", "dboom4.png", "dboom5.png", "dboom6.png", "dboom7.png", "dboom8.png", "dboom9.png", "dboom10.png"]), this.dynamite = new DynamiteMenu(this.game, this.game.world.width / 2 + 60, 26), this.dynamite.setCount(b.bonuses.stick), this.game.add.existing(this.dynamite), this.game.input.onDown.add(this.onMouseClick, this);
            var g = this.game.input.keyboard.addKey(Phaser.Keyboard.UP);
            g.onDown.add(this.onMakeBoomKey, this);
            var h = this.game.input.keyboard.addKey(Phaser.Keyboard.DOWN);
            h.onDown.add(this.onMouseClick, this), this.createUI(), this.levelTxt.text = "" + b.currentLevelNum, this.setMoney(b.currentMoney), this.currentLevelGoal = b.getGoal(b.currentLevelNum), this.setGoal(this.currentLevelGoal), this.timeBeforeEndLevel = 61, this.timer = this.game.time.events.loop(Phaser.Timer.SECOND, this.changeTime, this), this.timer.timer.start();
            var e = Helper.NineImage.create(this.game, 100, this.game.world.height, b.KEY, "br.png", 2, 2, 2, 2, !1),
                f = new Phaser.Sprite(this.game, 0, 0, e.texture);
            f.anchor.x = 1, this.game.add.existing(f), f = new Phaser.Sprite(this.game, this.game.world.width, 0, e.texture), this.game.add.existing(f), e = Helper.NineImage.create(this.game, this.game.world.width, 100, b.KEY, "br.png", 2, 2, 2, 2, !1), f = new Phaser.Sprite(this.game, 0, this.game.world.height, e.texture), this.game.add.existing(f);
            var e = Helper.NineImage.create(this.game, this.world.game.width, 8, b.KEY, "border.png", 3, 3, 3, 3, !1),
                i = new Phaser.Sprite(this.game, 0, 0, e.texture);
            i.anchor.set(0, 1), this.game.add.existing(i), i = new Phaser.Sprite(this.game, 0, this.game.world.height, e.texture), this.game.add.existing(i), e = Helper.NineImage.create(this.game, 8, this.game.world.height + 16, b.KEY, "border.png", 3, 3, 3, 3, !1);
            var i = new Phaser.Sprite(this.game, 0, -8, e.texture);
            i.anchor.set(1, 0), this.game.add.existing(i);
            var i = new Phaser.Sprite(this.game, this.game.world.width, -8, e.texture);
            this.game.add.existing(i);
            var j = this.game.add.button(10, this.game.world.height - 40, b.KEY, Music.music.statusSound, Music.music);
            this.classicSoundButton = j, j.input.priorityID = 1, j.scale.set(.5, .5), j.frameName = Music.music.getNameObject("sound")
        }, b.prototype.changeTime = function() {
            return this.timeBeforeEndLevel--, -1 == this.timeBeforeEndLevel ? void this.endLevel() : void(this.timerTxt.text = this.timeBeforeEndLevel.toString())
        }, b.prototype.endLevel = function() {
            SaveAPI.setAccess(SaveAPI.SCORE, b.currentMoney);
            SaveAPI.getAccess(SaveAPI.SCORE);
            this.currentLevelGoal <= b.currentMoney ? Goal.TYPE = ScreenType.Win : Goal.TYPE = ScreenType.Loose, this.game.time.events.remove(this.timer), this.game.state.start("Goal")
        }, b.prototype.createUI = function() {
            var a = this.game.add.group(),
                c = this.game.make.bitmapText(66, 12, "font", "Money", 20);
            c.tint = 9400321, c.cacheAsBitmap = !0, c.x -= c.width, a.addChild(c), c = this.game.make.bitmapText(66, 40, "font", "Goal", 20), c.tint = 9400321, c.cacheAsBitmap = !0, c.x -= c.width, a.addChild(c), c = this.game.make.bitmapText(this.game.world.width - 130, 12, "font", "   Time:", 20), c.tint = 9400321, c.cacheAsBitmap = !0, a.addChild(c), c = this.game.make.bitmapText(this.game.world.width - 130, 40, "font", "   Level:", 20), c.tint = 9400321, c.cacheAsBitmap = !0, a.addChild(c), this.moneyTxt = this.game.add.text(74, 10, "$1000", {
                font: "bold 16pt Arial",
                fill: "#009900",
                align: "left"
            }), this.moneyTxt.setShadow(1, 1, "#000000", 2), this.goalTxt = this.game.add.text(74, 38, "$1000", {
                font: "bold 16pt Arial",
                fill: "#FF9900",
                align: "left"
            }), this.goalTxt.setShadow(1, 1, "#000000", 2), this.timerTxt = this.game.add.text(this.game.world.width - 60, 10, "60", {
                font: "bold 16pt Arial",
                fill: "#FF6600",
                align: "left"
            }), this.timerTxt.setShadow(1, 1, "#000000", 2), this.levelTxt = this.game.add.text(this.game.world.width - 60, 38, "1", {
                font: "bold 16pt Arial",
                fill: "#FF6600",
                align: "left"
            }), this.levelTxt.setShadow(1, 1, "#000000", 2), this.addScoreTxt = this.game.add.text(this.pod.x, this.pod.y + 10, "1000", {
                font: "bold 20pt Arial",
                fill: "#009900",
                align: "left"
            }), this.addScoreTxt.anchor.set(.5, 0), this.addScoreTxt.scale.set(0, 0);
            var d = this.game.add.button(490, 40, b.KEY, this.endLevel, this, "exitOver.png", "exitDown.png", "exitDown.png", "exitDown.png");
            d.anchor.set(.5, .5), this.classicExitButton = d, this.classicHudLabels = a
        }, b.prototype.scoreAnim = function(a) {
            var c = this,
                d = this.addScoreTxt;
            d.text = "$" + a, d.position.set(this.pod.x, this.pod.y + 10), d.scale.set(0, 0), Music.music.audioEffects(Music.music.upfinish), this.game.add.tween(d.scale).to({
                x: 1,
                y: 1
            }, 200, "Linear", !0), this.game.add.tween(d).to({
                x: this.game.world.width / 2 - 120,
                y: 30
            }, 200, "Linear", !0).onComplete.add(function() {
                c.game.add.tween(d.scale).to({
                    x: 0,
                    y: 0
                }, 200, "Linear", !0, 500), c.game.add.tween(d).to({
                    x: c.moneyTxt.x,
                    y: c.moneyTxt.y
                }, 200, "Linear", !0, 500).onComplete.add(function() {
                    c.setMoney(a + b.currentMoney), c.pod.changeStateTo("rotate"), Music.music.audioEffects(Music.music.scoreAdd)
                })
            }, this)
        }, b.prototype.strAnim = function() {
            var a = this;
            this.pod.miner.setAnimStr();
            var c = this.addScoreTxt;
            c.text = "STRENGTH", c.position.set(this.pod.x, this.pod.y + 10), c.scale.set(0, 0), this.game.add.tween(c.scale).to({
                x: 1,
                y: 1
            }, 200, "Linear", !0), this.game.add.tween(c).to({
                x: this.game.world.width / 2 - 120,
                y: 30
            }, 200, "Linear", !0).onComplete.add(function() {
                a.game.add.tween(c.scale).to({
                    x: 0,
                    y: 0
                }, 200, "Linear", !0, 500).onComplete.add(function() {
                    b.bonuses.strenght += 5, a.pod.changeStateTo("rotate")
                })
            }, this)
        }, b.prototype.onAddScore = function(a) {
            if ("dynamite" == a.price) this.rcvDynamite();
            else if ("power" == a.price) this.strAnim();
            else {
                var c = a.price;
                !b.bonuses.rockBonus || 8 != a.type && 14 != a.type ? !b.bonuses.diamondBonus || 11 != a.type && 10 != a.type || (c += a.bonusAdd) : c += a.bonusAdd, this.scoreAnim(c)
            }
        }, b.prototype.rcvDynamite = function() {
            var a = this,
                c = this.game.add.sprite(this.game.world.width / 2 - 90, 40, b.KEY, "dyn.png");
            c.anchor.set(.5, .5), c.scale.set(0, 0);
            var d = this.game.add.tween(c.scale).to({
                x: 1,
                y: 1
            }, 200, "Linear", !0);
            d.onComplete.add(function() {
                a.game.add.tween(c).to({
                    x: a.dynamite.x,
                    y: a.dynamite.y + 15
                }, 100, "Linear", !0, 200).onComplete.add(function() {
                    c.kill(), a.dynamite.addOne(), a.pod.changeStateTo("rotate")
                })
            }, this)
        }, b.prototype.setMoney = function(a) {
            b.currentMoney = a, this.moneyTxt.text = "$" + b.currentMoney
        }, b.prototype.setGoal = function(a) {
            this.goalTxt.text = "$" + a
        }, b.prototype.makeBoom = function() {
            var a = this;
            this.game.add.existing(this.boom), this.boom.position = this.pod.gethvPos(), this.pod.setToSimple(), this.pod.slowDown = 0, this.pod.empty = !0, Music.music.audioEffects(Music.music.boom), this.boom.animations.play("dboom", 30, !1).onComplete.addOnce(function() {
                a.boom.parent.removeChild(a.boom)
            }, this);
            for (var b = this.boulders.length; b >= 0; b--)
                if (null != this.boulders[b] && this.boulders[b].picked) return this.boulders[b].picked = !1, this.boulders[b].kill(), void this.boulders.splice(b, 1)
        }, b.prototype.boomBounler = function(a) {
            a.animations.play("boom", 30, !1, !0), a.scale.x = 3, a.scale.y = 3, Music.music.audioEffects(Music.music.boom);
            for (var b = this.boulders.length; b >= 0; b--) null != this.boulders[b] && this.boulders[b] != a && Math.abs(Math.sqrt(Math.pow(this.boulders[b].x - a.x, 2) + Math.pow(this.boulders[b].y - a.y, 2))) <= 100 && (this.boulders[b].kill(), this.boulders.splice(b, 1))
        }, b.prototype.placeBoulders = function(a) {
            for (var b = 0; b < a.data.length; b++) {
                var c = new Boulder(this.game, a.data[b]);
                this.game.add.existing(c), this.boulders.push(c), c.addScore.add(this.onAddScore, this), c.makeBoomBldr.add(this.boomBounler, this);
            }
        }, b.prototype.placePod = function() {
            this.pod = new Pod(this.game, 46), this.game.add.existing(this.pod), this.pod.createRod(), this.pod.changeStateTo("rotate")
        }, b.prototype.onMouseClick = function(a) {
            this.pod.shootPod()
        }, b.prototype.onMakeBoomKey = function() {
            "rewind" == this.pod.podStatus && this.pod.slowDown > 0 && b.bonuses.stick > 0 && (this.makeBoom(), this.pod.miner.setAnimDinamite(), this.dynamite.removeOne())
        }, b.prototype.update = function() {
            for (var a in this.boulders) this.boulders[a].updateStatus(this), this.boulders[a].remove() && (this.removeBoulder(this.boulders[a]), this.boulders.splice(a, 1));
            this.pod.updateStatus()
        }, b.prototype.removeBoulder = function(a) {
            a.kill(), a = null
        }, b.prototype.beginSwipe = function() {
            this.startX = this.game.input.worldX, this.startY = this.game.input.worldY, this.inSwipe = !0
        }, b.prototype.endSwipe = function() {
            this.inSwipe = !1
        }, b.prototype.swipeUpdate = function(a, b, c) {
            if (this.inSwipe) {
                this.endX = this.game.input.worldX, this.endY = this.game.input.worldY;
                var d = this.startX - this.endX,
                    e = this.startY - this.endY;
                Math.abs(e) > 2 * Math.abs(d) && Math.abs(e) > 2 && e > 0 && (this.onMakeBoomKey(), this.inSwipe = !1)
            }
        }, b.currentMoney = 0, b.currentLevelNum = 1, b.totalLevels = 10, b.KEY = "atlas", b
    }(Phaser.State),
    Boulder = function(a) {
        function b(c, d) {
            var e = this;
            a.call(this, c, d.x, d.y, b.KEY, d.name), this.classicDefinition = JSON.parse(JSON.stringify(d)), this.picked = !1, this.gotBoulder = !1, this.scale.x = d.scale.x, this.scale.y = d.scale.y, this.x += 35, this.y += 50, this.anchor.set(.5, .5);
            var f = 0;
            if (d.flip && (this.scale.x *= -1), "sprite215.png" == d.name ? this.boulderData = BoulderData.reciveSprite215() : (this.boulderData = BoulderData.data[d.name], f = BoulderData.data[d.name].type), 10 == f || 6 == f) {
                var g = -370;
                d.flip && (g *= -1), this.classicMoveTween = this.game.add.tween(this).to({
                    x: this.x + g
                }, 4200, "Linear", !0, 0, 9999, !0);
                this.classicMoveTween.onLoop.add(function() {
                    e.scale.x *= -1
                })
            }
            if (9 == f) this.animations.add("boom", ["dboom1.png", "dboom2.png", "dboom3.png", "dboom4.png", "dboom5.png", "dboom6.png", "dboom7.png", "dboom8.png", "dboom9.png", "dboom10.png"]);
            else if (5 == f) {
                this.animations.add("blick", ["sprite207_0001.png", "sprite207_0002.png", "sprite207_0003.png", "sprite207_0004.png", "sprite207_0005.png", "sprite207_0006.png", "sprite207_0007.png", "sprite207_0008.png", "sprite207_0009.png"]);
                var h = this.animations.play("blick", 30);
                h.onComplete.add(function() {
                    var a = e.game.time.create(!0);
                    a.add(3e3 * Math.random() + 1e3, h.play, h, "blick"), a.autoDestroy = !0, a.start()
                })
            }
            this.addScore = new Phaser.Signal, this.makeBoomBldr = new Phaser.Signal
        }
        return __extends(b, a), b.prototype.updateStatus = function(a) {
            var b = a.pod;
            if (this.picked) "wait" == b.podStatus && (b.miner.setStaticWait(), this.addScore.dispatch(this.boulderData), this.gotBoulder = !0);
            else if ("shoot" == b.podStatus && this.hitTestPoint(b.hotSpotX, b.hotSpotY)) {
                b.changeStateTo("rewind"), b.empty = !1, b.setCustomHv(this.boulderData.type), 9 == this.boulderData.type ? this.makeBoomBldr.dispatch(this) : this.kill(), this.playSoundBoulder(this.boulderData.type), this.picked = !0;
                var c = this.boulderData.weight - Game.bonuses.strenght;
                0 > c && (c = 1), b.miner.setUp(60 / c), b.slowDown = this.boulderData.weight
            }
        }, b.prototype.playSoundBoulder = function(a) {
            2 == a || 3 == a || 12 == a ? Music.music.audioEffects(Music.music.hvGood) : 6 == a || 7 == a || 8 == a || 13 == a || 14 == a ? Music.music.audioEffects(Music.music.hvBad) : 4 != a && 5 != a && 10 != a && 11 != a || Music.music.audioEffects(Music.music.hvCool)
        }, b.prototype.hitTestPoint = function(a, b) {
            return Math.abs(Math.sqrt(Math.pow(a - this.x, 2) + Math.pow(b - this.y, 2))) < Math.abs(this.width)
        }, b.prototype.remove = function() {
            return this.gotBoulder
        }, b.KEY = "atlas", b
    }(Phaser.Sprite),
    Pod = function(a) {
        function b(c, d) {
            a.call(this, c, c.world.width / 2, d, b.KEY, "hv1.png"), this._podStatus = "rotate", this.slowDown = 0, this.empty = !1, this.speed = 0, this.maxSpeed = 4, this.currentDir = 0, this.startRopeX = 0, this.yPoint = 0, this.anchor.set(.5, -.2), this.yPoint = d, this.miner = new Miner(this.game, this.game.world.width / 2, 0), this.miner.anchor.set(.3, 0), this.game.add.existing(this.miner), this.miner.setStaticWait(), this.waveAnim = this.game.make.sprite(0, 0, b.KEY, "getAnim0001.png"), this.waveAnim.anchor.set(.5, .15), this.waveAnim.animations.add("getAnim", ["getAnim0001.png", "getAnim0002.png", "getAnim0003.png", "getAnim0004.png", "getAnim0005.png", "getAnim0006.png", "getAnim0007.png", "getAnim0008.png", "getAnim0009.png"])
        }
        return __extends(b, a), b.prototype.gethvPos = function() {
            var a = new Phaser.Point(this.hotSpotX, this.hotSpotY);
            return a
        }, b.prototype.setToSimple = function() {
            this.anchor.set(.5, -.2), this.frameName = "hv1.png"
        }, b.prototype.createRod = function() {
            this.rod = new Rod(this.game, this.x, this.y), this.parent.addChild(this.rod), this.rotation = -Math.PI / 3, this.rotateTween = this.game.add.tween(this).to({
                rotation: Math.PI / 3
            }, 2e3, Phaser.Easing.Sinusoidal.InOut, !0, 0, 99999, !0), this.changeStateTo("rotate")
        }, b.prototype.changeStateTo = function(a) {
            this._podStatus = a, "rotate" != a ? this.rotateTween.pause() : this.rotateTween.resume()
        }, Object.defineProperty(b.prototype, "podStatus", {
            get: function() {
                return this._podStatus
            },
            enumerable: !0,
            configurable: !0
        }), b.prototype.move = function() {
            this.x += this.speed
        }, b.prototype.setSpeed = function(a) {
            this.speed = a, this.speed > this.maxSpeed ? this.speed = this.maxSpeed : this.speed < -this.maxSpeed && (this.speed = -this.maxSpeed)
        }, b.prototype.shootPod = function() {
            "rotate" == this._podStatus && (this.miner.setStaticDown(), this.currentDir = this.rotation + Math.PI / 2, this.startRopeX = this.x, this.changeStateTo("shoot"), Music.music.audioEffects(Music.music.down))
        }, b.prototype.updateStatus = function() {
            switch (this._podStatus) {
                case "rotate":
                    this.rod.rotation = this.rotation, this.move();
                    break;
                case "shoot":
                    this.slowDown = 0, this.x += 10 * Math.cos(this.currentDir), this.y += 10 * Math.sin(this.currentDir), this.rod.rotation = this.rotation, this.hotSpotX = this.x + 40 * Math.cos(this.currentDir), this.hotSpotY = this.y + 40 * Math.sin(this.currentDir), (this.hotSpotY > this.game.world.height || this.hotSpotX < 0 || this.hotSpotX > this.game.world.width) && (this.changeStateTo("rewind"), this.miner.setUp(), this.empty = !0), this.rod.moveTo(this.startRopeX, this.yPoint), this.rod.lineTo(this.x, this.y);
                    break;
                case "rewind":
                    this.hotSpotX = this.x + 40 * Math.cos(this.currentDir), this.hotSpotY = this.y + 40 * Math.sin(this.currentDir), this.x -= (10 + Game.bonuses.strenght - this.slowDown) * Math.cos(this.currentDir), this.y -= (10 + Game.bonuses.strenght - this.slowDown) * Math.sin(this.currentDir), this.y < this.yPoint ? (this.y = this.yPoint, this.x = this.startRopeX, this.setToSimple(), this.empty ? this.changeStateTo("rotate") : this.changeStateTo("wait"), this.miner.setStaticWait()) : (this.rod.moveTo(this.startRopeX, this.yPoint), this.rod.lineTo(this.x, this.y))
            }
        }, b.prototype.setCustomHv = function(a) {
            var b = this;
            this.frameName = "hv" + a + ".png", this.anchor.y = 0, 5 == a && (this.addChild(this.waveAnim), this.waveAnim.animations.play("getAnim", 20).onComplete.addOnce(function() {
                b.removeChild(b.waveAnim)
            }, this))
        }, b.KEY = "atlas", b
    }(Phaser.Sprite),
    Rod = function(a) {
        function b(c, d, e) {
            a.call(this, c, d, e, b.KEY, "line.png"), this.x += this.width / 2, this.anchor.set(.5, 0), this.height = 26
        }
        return __extends(b, a), b.prototype.moveTo = function(a, b) {
            this.x = a + this.width / 2, this.y = b
        }, b.prototype.lineTo = function(a, b) {
            this.height = Math.sqrt(Math.pow(a - this.x, 2) + Math.pow(b - this.y, 2)) + 20
        }, b.KEY = "atlas", b
    }(Phaser.Sprite),
    Miner = function(a) {
        function b(c, d, e) {
            a.call(this, c, d, e, b.KEY), this.scale.set(.9, .9), this.myX = d, this.myY = e, this.animations.add("Dinamite", ["minerD1.png", "minerD2.png", "minerD3.png", "minerD4.png", "minerD5.png"]), this.animations.add("Str", ["minerStr0001.png", "minerStr0002.png", "minerStr0001.png", "minerStr0002.png"]), this.animations.add("Down", ["minerDown0001.png", "minerDown0002.png", "minerDown0003.png", "minerDown0004.png"]);
            this.animations.add("Up", ["minerUp0001.png", "minerUp0002.png", "minerUp0003.png", "minerUp0004.png", "minerUp0005.png", "minerUp0006.png", "minerUp0007.png", "minerUp0008.png", "minerUp0009.png", "minerUp0010.png", "minerUp0011.png", "minerUp0012.png", "minerUp0013.png", "minerUp0014.png", "minerUp0015.png", "minerUp0016.png", "minerUp0017.png"])
        }
        return __extends(b, a), b.prototype.update = function() {
            84 != this.frame && 92 != this.frame || Music.music.audioEffects(Music.music.up)
        }, b.prototype.setStaticWait = function() {
            this.scale.set(.9, .9), this.x = this.myX - 2, this.y = this.myY + 8, this.animations.stop(), this.frameName = "minerDown0001.png"
        }, b.prototype.setStaticDown = function() {
            this.scale.set(.9, .9), this.animations.currentAnim.stop(!0), this.x = this.myX - 2, this.y = this.myY + 8, this.animations.play("Down", 15, !1)
        }, b.prototype.setAnimDinamite = function() {
            this.scale.set(.9, .9), this.animations.currentAnim.stop(!0), this.x = this.myX, this.y = this.myY, this.animations.play("Dinamite", 20, !1).onComplete.addOnce(this.setUp, this)
        }, b.prototype.setAnimStr = function() {
            this.scale.set(1.05, 1.05), this.animations.currentAnim.stop(!0), this.x = this.myX + 1, this.y = this.myY + 1, this.animations.play("Str", 5, !1).onComplete.addOnce(this.setStaticWait, this)
        }, b.prototype.setUp = function(a) {
            void 0 === a && (a = 30), this.scale.set(.9, .9), this.animations.currentAnim.stop(!0), this.x = this.myX - 2, this.y = this.myY + 6, this.animations.play("Up", a, !0, !1)
        }, b.KEY = "atlas", b
    }(Phaser.Sprite),
    BoulderData = function() {
        function a() {}
        return a.init = function() {
            a.data["sprite205.png"] = {
                type: 2,
                price: 50,
                weight: 3,
                bonusAdd: 0
            }, a.data["sprite207.png"] = {
                type: 5,
                price: 500,
                weight: 9,
                bonusAdd: 0
            }, a.data["sprite210.png"] = {
                type: 8,
                price: 20,
                weight: 9,
                bonusAdd: 40
            }, a.data["sprite217.png"] = {
                type: 3,
                price: 100,
                weight: 7,
                bonusAdd: 0
            }, a.data["sprite220.png"] = {
                type: 14,
                price: 11,
                weight: 8,
                bonusAdd: 22
            }, a.data["sprite290.png"] = {
                type: 11,
                price: 600,
                weight: 2,
                bonusAdd: 300
            }, a.data["sprite311.png"] = {
                type: 5,
                price: 500,
                weight: 9,
                bonusAdd: 0
            }, a.data["sprite312.png"] = {
                type: 2,
                price: 50,
                weight: 3,
                bonusAdd: 0
            }, a.data["sprite313.png"] = {
                type: 14,
                price: 11,
                weight: 8,
                bonusAdd: 22
            }, a.data["sprite314.png"] = {
                type: 3,
                price: 100,
                weight: 7,
                bonusAdd: 0
            }, a.data["sprite316.png"] = {
                type: 4,
                price: 250,
                weight: 8,
                bonusAdd: 0
            }, a.data["sprite319.png"] = {
                type: 6,
                price: 2,
                weight: 3,
                bonusAdd: 0
            }, a.data["sprite321.png"] = {
                type: 10,
                price: 602,
                weight: 5,
                bonusAdd: 300
            }, a.data["sprite324.png"] = {
                type: 7,
                price: 7,
                weight: 3,
                bonusAdd: 0
            }, a.data["sprite326.png"] = {
                type: 13,
                price: 20,
                weight: 2,
                bonusAdd: 0
            }, a.data["sprite332.png"] = {
                type: 9,
                price: 1,
                weight: 2,
                bonusAdd: 0
            }
        }, a.reciveSprite215 = function() {
            var b = new a;
            b.type = 12;
            var c, d;
            return Game.bonuses.cloverBonus ? (c = this.random(6) + 1, 2 >= c ? b.price = "power" : 4 >= c ? Game.bonuses.stick <= 3 ? b.price = "dynamite" : b.price = this.random(300) + 300 : b.price = 700) : (c = this.random(6) + 1, 3 >= c ? b.price = this.random(600) + 1 : 4 == c ? b.price = "power" : 5 == c ? Game.bonuses.stick <= 3 ? b.price = "dynamite" : b.price = this.random(100) + 100 : b.price = 800), d = this.random(4) + 1, 1 == d ? b.weight = this.random(9) + 1 : 2 == d ? b.weight = -1 * (this.random(5) + 1) : 3 == d ? b.weight = 9 : d >= 4 && (b.weight = -1), b
        }, a.random = function(a) {
            return this.random2(0, a)
        }, a.random2 = function(a, b) {
            return Math.floor(Math.random() * (b - a)) + a
        }, a.data = {}, a
    }(),
    Bonuses = function() {
        function a() {
            this.stick = 0, this.strenght = 0
        }
        return a
    }(),
    DynamiteMenu = function(a) {
        function b(c, d, e) {
            a.call(this, c, d, e, b.KEY, b.SPRITE + 1 + ".png")
        }
        return __extends(b, a), b.prototype.setCount = function(a) {
            Game.bonuses.stick = a, 0 == Game.bonuses.stick ? this.visible = !1 : Game.bonuses.stick <= 5 && (this.frameName = b.SPRITE + Game.bonuses.stick + ".png"), 1 == Game.bonuses.stick && (this.visible = !0)
        }, b.prototype.addOne = function() {
            Game.bonuses.stick++, this.setCount(Game.bonuses.stick)
        }, b.prototype.removeOne = function() {
            Game.bonuses.stick--, this.setCount(Game.bonuses.stick)
        }, b.prototype.has = function() {
            return Game.bonuses.stick > 0
        }, b.KEY = "atlas", b.SPRITE = "dnMenu", b
    }(Phaser.Sprite),
    MainMenu = function(a) {
        function b() {
            a.apply(this, arguments)
        }
        return __extends(b, a), b.prototype.init = function() {
            ScaleManager.inSplash = !1, ScaleManager.SizeCalculation(this.game)
        }, b.prototype.create = function() {
            Music.music = new Music(this.game), FadeInOut.Init(this.game), Goal.TYPE = ScreenType.Goal, Game.bonuses = new Bonuses, this.game.stage.backgroundColor = 0;
            var a = this.game.add.sprite(170, 172, b.KEY, "menuGlow.png");
            a.anchor.set(.5, .5), a.scale.set(2, 2), this.game.add.tween(a.scale).to({
                x: 2.2,
                y: 2.2
            }, 1e3, "Linear", !0, 0, 9999, !0);
            var c = this.game.add.button(70, 68, b.KEY, this.startButtonClick, this);
            c.animations.add("gold", ["goldBig_0001.png", "goldBig_0002.png", "goldBig_0003.png", "goldBig_0004.png", "goldBig_0005.png", "goldBig_0006.png", "goldBig_0007.png", "goldBig_0008.png", "goldBig_0009.png"]), c.animations.play("gold", 15, !0), this.startText = this.game.make.sprite(c.width / 2, c.height / 2, b.KEY, "start.png"), this.startText.anchor.set(.5, .5), c.addChild(this.startText), a = this.game.add.sprite(this.game.world.width, this.game.world.height, b.KEY, "menuMiner.png"), a.anchor.set(1, 1), FadeInOut.instance.fadeOut();
            var d = this.game.add.sprite(20, 330, b.KEY, "goalBack.png"),
                e = this.game.make.bitmapText(d.width / 2, 10, "font", "Controls", 26);
            if (e.tint = 16776960, e.cacheAsBitmap = !0, e.x -= e.width / 2, d.addChild(e), this.game.device.desktop && window.innerWidth > 900) {
                var f = this.game.make.sprite(82, 84, b.KEY, "arrow.png");
                f.anchor.set(.5, .5), f.scale.set(1.5, 1.5), f.rotation = Math.PI, d.addChild(f);
                var g = this.game.make.bitmapText(f.x, f.y - f.height / 2 - 24, "font", "Drop Claw", 22);
                g.tint = 0, g.cacheAsBitmap = !0, g.x -= g.width / 2, d.addChild(g), g = this.game.make.bitmapText(f.x, f.y + f.height / 2 + 4, "font", "press", 24), g.tint = 0, g.cacheAsBitmap = !0, g.x -= g.width / 2, d.addChild(g), f = this.game.make.sprite(d.width - 82, 84, b.KEY, "arrow.png"), f.anchor.set(.5, .5), f.scale.set(1.5, 1.5), d.addChild(f), g = this.game.make.bitmapText(f.x, f.y - f.height / 2 - 24, "font", "Toss Dynamite", 22), g.tint = 0, g.cacheAsBitmap = !0, g.x -= g.width / 2, d.addChild(g), g = this.game.make.bitmapText(f.x, f.y + f.height / 2 + 4, "font", "press", 24), g.tint = 0, g.cacheAsBitmap = !0, g.x -= g.width / 2, d.addChild(g)
            } else {
                var f = this.game.make.sprite(82, 84, b.KEY, "tap.png");
                f.anchor.set(.5, .5), f.scale.set(1.5, 1.5), d.addChild(f);
                var g = this.game.make.bitmapText(f.x, f.y - f.height / 2 - 24, "font", "Drop Claw", 22);
                g.tint = 0, g.cacheAsBitmap = !0, g.x -= g.width / 2, d.addChild(g), g = this.game.make.bitmapText(f.x, f.y + f.height / 2 + 2, "font", "tap", 24), g.tint = 0, g.cacheAsBitmap = !0, g.x -= g.width / 2, d.addChild(g), f = this.game.make.sprite(d.width - 82, 84, b.KEY, "bonus1.png"), f.anchor.set(.5, .5), f.scale.set(1.5, 1.5), d.addChild(f), g = this.game.make.bitmapText(f.x, f.y - f.height / 2 - 24, "font", "Toss Dynamite", 22), g.tint = 0, g.cacheAsBitmap = !0, g.x -= g.width / 2, d.addChild(g), g = this.game.make.bitmapText(f.x, f.y + f.height / 2 + 2, "font", "Bomb button", 24), g.tint = 0, g.cacheAsBitmap = !0, g.x -= g.width / 2, d.addChild(g)
            }
            var h = this.game.add.button(10, this.game.world.height - 40, Game.KEY, Music.music.statusSound, Music.music);
            h.scale.set(.5, .5), h.frameName = Music.music.getNameObject("sound"), ScaleManager.SizeCalculation(this.game)
        }, b.prototype.startButtonClick = function() {
            Game.bonuses = new Bonuses, Goal.TYPE = ScreenType.Goal, FadeInOut.instance.fadeTo("Goal")
        }, b.KEY = "atlas", b
    }(Phaser.State),
    Goal = function(a) {
        function b() {
            a.apply(this, arguments)
        }
        return __extends(b, a), b.prototype.create = function() {
            this.game.stage.backgroundColor = 8605230;
            var a = this.game.add.sprite(0, 0, b.KEY, "goldBig_0009.png");
            a.anchor.set(.5, .5);
            var c = (a.width, a.height),
                d = 0,
                e = this.game.add.group();
            e.add(a);
            for (var f = 0; 6 > f; f++)
                for (var g = 0; 8 > g; g++) {
                    for (a = this.game.make.sprite(this.game.world.width / 7 * g, c / 2 * f, b.KEY, "goldBig_0009.png"), a.anchor.set(.5, .5), a.rotation = 2 * Math.PI * Math.random(); .7 > d;) d = Math.random();
                    a.scale.set(d, d), e.addChildAt(a, Math.round((e.countLiving() - 1) * Math.random())), d = 0
                }
            var h = this.game.add.sprite(this.game.world.width / 2, this.game.world.height / 2, b.KEY, "goalBack.png");
            h.anchor.set(.5, .5);
            var i = this.game.add.sprite(this.game.world.width / 2, this.game.world.height / 2 - h.height, b.KEY, "goldMiner.png");
            i.anchor.set(.5, .5);
            var j = this.game.time.create(!0);
            this.classicGoalTimer = j;
            if (b.TYPE == ScreenType.Goal) {
                Music.music.audioEffects(Music.music.goal);
                var k = this.game.make.bitmapText(this.game.world.width / 2, this.game.world.height / 2 - 30, "font", "Your Next Goal is", 36);
                k.tint = 16776960, k.x -= k.width / 2, k.cacheAsBitmap = !0, this.game.add.existing(k), k = this.game.make.bitmapText(this.game.world.width / 2, this.game.world.height / 2 + 18, "font", "$" + Game.getGoal(Game.currentLevelNum), 36), k.tint = 52224, k.x -= k.width / 2, k.cacheAsBitmap = !0, this.game.add.existing(k), j.add(2e3, this.goGameAfterDelay, this)
            } else if (b.TYPE == ScreenType.Loose) {
                i.destroy();
                var k = this.game.make.bitmapText(0, 0, "font", "You Did Not\nReach Your Goal", 36, "center");
                k.tint = 16776960, k.x -= k.width / 2, k.y -= k.height / 2, k.cacheAsBitmap = !0, h.addChild(k), h.alpha = 0, this.game.add.tween(h).to({
                    alpha: 1
                }, 800, Phaser.Easing.Linear.None, !0), j.add(3e3, this.goToMenuAfterDelay, this), Game.currentLevelNum = 1, Game.currentMoney = 0
            } else {
                Music.music.audioEffects(Music.music.win);
                var k = this.game.make.bitmapText(0, 0, "font", "You made it to\nthe next Level!", 36, "center");
                k.tint = 16776960, k.x -= k.width / 2, k.y -= k.height / 2, k.cacheAsBitmap = !0, h.addChild(k), h.scale.set(.1, .1), this.game.add.tween(h.scale).to({
                    x: 1,
                    y: 1
                }, 800, Phaser.Easing.Bounce.Out, !0), j.add(3e3, this.goToShopAfterDelay, this)
            }
            j.start(), FadeInOut.instance.fadeOut()
        }, b.prototype.goGameAfterDelay = function() {
            this.game.state.start("Game")
        }, b.prototype.goToShopAfterDelay = function() {
            this.game.state.start("Shop")
        }, b.prototype.goToMenuAfterDelay = function() {
            FadeInOut.instance.fadeTo("MainMenu")
        }, b.KEY = "atlas", b
    }(Phaser.State),
    ScreenType;
! function(a) {
    a[a.Win = 0] = "Win", a[a.Loose = 1] = "Loose", a[a.Goal = 2] = "Goal"
}(ScreenType || (ScreenType = {}));
