#!/usr/bin/env python3
"""Reproducible PNG/offline derivative of the immutable 4399 game snapshot."""
import argparse, hashlib, io, json, pathlib, re, shutil, struct, subprocess, tempfile, zipfile
from PIL import Image
ROOT=pathlib.Path(__file__).resolve().parents[2]
SOURCE_SHA='3abeb1a030d04d9807c1b5fa860138b83845eec5a7d12d4def83f5e45d009b0a'
MANIFEST_SHA='3cde84de9ffb00f4e63f6a9b29e5d6e4f72aedbeb12fcfea0686d7e23d073545'
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def replace_one(source,old,new):
 if source.count(old)!=1:raise ValueError('Patch anchor count differs: '+old[:100])
 return source.replace(old,new)
def replace_section(source,start,end,replacement):
 if source.count(start)!=1 or source.count(end)!=1:raise ValueError('Input method anchors changed: '+start)
 begin=source.index(start);finish=source.index(end,begin)
 return source[:begin]+replacement+source[finish:]
def patch(source,files,texture_names):
 # Resource IDs remain stable. Unavailable alternatives are null, never shifted.
 match=re.search(r'  m.Ve =\s*"([^"]+)"\.split\(\s*" ",?\s*\);',source)
 if not match:raise ValueError('Missing resource table')
 routes=[]
 for route in match[1].split(' '):
  if route.endswith('.webp') or route.startswith('video/') or 'xmas' in route or 'obj_candy4' in route:routes.append(None);continue
  route=route.replace('{image}','png').replace('.avif','.png')
  routes.append(route)
 source=source[:match.start()]+'  m.Ve = '+json.dumps(routes,separators=(',',':'))+';'+source[match.end():]
 source=replace_one(source,'      this.Ck && this.window.xk() <= 2 && this.window.Qc.x < 1e3 && m.Ly(1);','      // Offline art is always the complete 2x set.')
 source=replace_one(source,'      qb.Qj != null && m.BF("dds");\n      qb.kH != null && m.BF("ktx");','      m.BF("png");')
 source=replace_one(source,'      m.Qo = "avif";','      m.Qo = "png";')
 source=replace_one(source,'      m.VB = a;','      m.VB = "png";')
 source=replace_one(source,'      m.Ml = a;','      m.Ml = "aac";')
 # Only English has the retained bitmap glyphs. Keep kf's original atlas index order.
 source=replace_one(source,'      return "ru nl ko ja it fr es en de br".split(" ").slice();','      return ["en"];')
 source=replace_one(source,'      z.ei(a.language);','      this.offlineLanguageChanged = this.version === this.bp() && a.language !== "en";\n      z.ei(a.language);')
 # Parse/schema errors retain the original reset fallback. Durable write failures
 # must escape instead of entering that fallback and overwriting existing progress.
 source=replace_one(source,'      this.storage.load(function (c) {\n        let d = !1;','      this.storage.load(function (c) {\n        let d = !1, migrated = !1;')
 source=replace_one(source,'''            for (c = !1; b.version < b.bp(); )
              (b.ct(b.version + 1), b.version++, (c = !0));
            if (c) {
              b.save(a);
              return;
            }''','''            for (; b.version < b.bp(); )
              (b.ct(b.version + 1), b.version++, (migrated = !0));''')
 source=replace_one(source,'        d ? (b.reset(), b.save(a)) : a();','        if (d) b.reset();\n        d || migrated ? b.save(a) : a();')
 # Force AAC before the platform preference test, preserving its SPR sprite container.
 source=replace_one(source,'          var c = a != null ? a : Q.FO();','          var c = "aac";')
 # No video resources or video DOM are used. Outro returns through the original caller,
 # which sets gameWon, persists, and enters the result screen's terminal state.
 start=source.index('  class Hc extends ea {');end=source.index('  Hc.i = !0;',start)
 source=source[:start]+'''  class Hc extends ea {
    dc() { return 0; }
    qc() { return []; }
    bb() { super.bb(); this.xE(); }
    Xc() { super.Xc(); }
    Ea() { return "OfflineVideoContinuation"; }
  }
'''+source[end:]
 source=replace_one(source,'      w.gp() == 0 && a.push(this.system.window.Zo() > 1 ? m.Su : m.Tu);','      // Intro is disabled and is not preloaded.')
 source=replace_one(source,'      return [ua.instance.window.Zo() > 1 ? m.Su : m.Tu];','      return [];')
 source=replace_one(source,'      return [this.system.window.Zo() > 1 ? m.OA : m.PA];','      return [];')
 # A slow fixed-timestep frame can update again before a Promise continuation.
 # Use the existing paused level state until the completion callback creates its animation.
 source=replace_one(source,'? (N.rU(w.gp(), Zb()),','? (this.ba(4), N.rU(w.gp(), Zb()),')
 source=replace_one(source,'            a.lz = !1;\n            a.rk();','            a.lz = !1;\n            a.ba(5);\n            a.rk();')
 # The pinned snapshot has only 20 pictures. Its orphan 21st reward has no image.
 source=replace_one(source,'              -1, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,','              -1, 1, 0, 0, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,')
 source=replace_one(source,'            0.66, 0, 2172, 1627, 0.65, -5, 2586, 1680, 0.75, -2, 2855, 1624,','            0.66, 0, 2172, 1627, 0.65, -5, 2586, 1680,')
 source=replace_one(source,'for (a = 1; a < 22; ) {','for (a = 1; a < 21; ) {')
 source=replace_one(source,'(z.qj = a.pictures),\n        (z.Tk = a.picturesBadgeCounter));','(this.offlinePicturesChanged = a.pictures.includes("2-10")),\n        (z.qj = a.pictures.filter(key => key !== "2-10")),\n        (z.Tk = this.offlinePicturesChanged ? Math.min(a.picturesBadgeCounter, z.qj.length) : a.picturesBadgeCounter));')
 source=replace_one(source,'  class z extends pd {','  class z extends pd {\n    load(done) {\n      this.offlinePicturesChanged = this.offlineLanguageChanged = false;\n      super.load(() => {\n        if (this.offlinePicturesChanged || this.offlineLanguageChanged) {\n          this.offlinePicturesChanged = this.offlineLanguageChanged = false;\n          this.save(done);\n        } else done();\n      });\n    }')
 # p.Ol is created from m.yu; releasing m.Oj instead destroys a live hook atlas.
 source=replace_one(source,'p.Ol != null && b(4096) && (p.ka(m.Oj), (p.Ol = null));','p.Ol != null && b(4096) && (p.ka(m.yu), (p.Ol = null));')
 # Keep pointer DOWN/UP coordinates and state across a short, unconsumed frame.
 source=replace_one(source,'  class kc extends Cc {\n    constructor() {\n      super();','  class kc extends Cc {\n    constructor() {\n      super();\n      this.offlinePendingReleases = new Set();\n      this.offlinePressPositions = new Map();\n      this.offlineReleasePositions = new Map();')
 source=replace_one(source,'    update(a) {\n      super.update(a);\n      for (a = 0; a < 9; ) {','    update(a) {\n      for (const id of this.offlinePendingReleases) {\n        const button = this.$o(id);\n        if (button && !button.Tl) {\n          this.offlinePendingReleases.delete(id);\n          const end = this.offlineReleasePositions.get(id);\n          if (end) {\n            this.sf[id][1] = end.x !== this.position[id].x || end.y !== this.position[id].y;\n            this.position[id].x = end.x;\n            this.position[id].y = end.y;\n            this.offlineReleasePositions.delete(id);\n          }\n          super.release(id);\n          if (id >= 4) this.qt[id] = 2;\n        }\n      }\n      super.update(a);\n      for (a = 0; a < 9; ) {')
 source=replace_one(source,'    release(a) {\n      super.release(a);\n      a >= 4 && (this.qt[a] = 2);\n    }','    sj(a) {\n      if (this.offlinePendingReleases.delete(a)) this.$o(a).reset();\n      this.offlineReleasePositions.delete(a);\n      this.qt[a] = 0;\n      this.sf[a][0] = this.sf[a][1] = false;\n      this.offlinePressPositions.set(a, {x:this.position[a].x,y:this.position[a].y});\n      super.sj(a);\n    }\n    release(a) {\n      const button = this.$o(a);\n      if (button && button.state === 1 && button.Tl) {\n        this.offlinePendingReleases.add(a);\n        this.offlineReleasePositions.set(a, {x:this.position[a].x,y:this.position[a].y});\n        const start = this.offlinePressPositions.get(a);\n        this.position[a].x = start.x;\n        this.position[a].y = start.y;\n        this.sf[a][0] = this.sf[a][1] = false;\n        return;\n      }\n      super.release(a);\n      a >= 4 && (this.qt[a] = 2);\n    }\n    cancel(a) {\n      if (a == null || a < 0 || a >= 9) return;\n      this.offlinePendingReleases.delete(a);\n      this.offlinePressPositions.delete(a);\n      this.offlineReleasePositions.delete(a);\n      const button = this.$o(a);\n      if (button) button.reset();\n      this.qt[a] = 0;\n      this.sf[a][0] = this.sf[a][1] = false;\n      this.position[a].x = this.position[a].y = kc.Tq;\n    }\n    ZN() {\n      for (let id = 0; id < 9; id++) this.cancel(id);\n      super.ZN();\n    }')
 source=replace_section(source,'    CT(a) {\n','    NE(a) {\n','    nextPointerSlot() {\n      const used = new Set(Object.values(this.touches));\n      for (let slot = 4; slot < 9; slot++) if (!used.has(slot)) return slot;\n      throw new Error("No free pointer slot");\n    }\n    CT(a) {\n      if (!this.enabled) return;\n      if (a.pointerType === "mouse") {\n        const slot = a.button + 1;\n        if (slot > 3) return;\n        this.Ce(a, slot);\n        this.positionToPrimary(slot);\n        this.state.sj(slot);\n        this.state.sj(0);\n      } else if (this.Ix < this.ZD) {\n        const slot = this.nextPointerSlot();\n        this.touches[a.pointerId] = slot;\n        this.Ce(a, slot);\n        this.Ix++;\n        this.state.sj(slot);\n        if (slot === 4) { this.positionToPrimary(slot); this.state.sj(0); }\n      }\n    }\n    positionToPrimary(slot) {\n      this.state.position[0].x = this.state.position[slot].x;\n      this.state.position[0].y = this.state.position[slot].y;\n    }\n')
 source=replace_section(source,'    NE(a) {\n','    BT(a) {\n','    NE(a) {\n      a.stopPropagation();\n      if (!this.enabled) return;\n      if (a.pointerType === "mouse") {\n        const slot = a.button + 1;\n        if (slot > 3) return;\n        this.Ce(a, slot);\n        this.positionToPrimary(slot);\n        this.state.release(slot);\n        this.state.release(0);\n      } else {\n        const slot = this.touches[a.pointerId];\n        if (slot == null) return;\n        delete this.touches[a.pointerId];\n        this.Ce(a, slot);\n        if (slot === 4) this.positionToPrimary(slot);\n        this.state.release(slot);\n        this.Ix--;\n        if (slot === 4) this.state.release(0);\n      }\n    }\n')
 source=replace_one(source,'    BT(a) {\n      this.NE(a);\n    }','    BT(a) {\n      const slot = this.touches[a.pointerId];\n      this.NE(a);\n      if (a.pointerType === "mouse") {\n        for (let key = 0; key < 4; key++) this.state.cancel(key);\n        cancelOfflineGamePointer(0);\n      } else {\n        this.state.cancel(slot);\n        if (slot === 4 || this.Ix === 0) this.state.cancel(0);\n        if (slot != null) cancelOfflineGamePointer(slot - 4);\n      }\n    }')
 source=replace_one(source,'    GV(a, b) {','    cancelPointer(pointer) {\n      this.Hi[pointer] = false;\n      this.hC = false;\n      if (this.hs === pointer) this.hs = -1;\n      for (const hook of this.Nd) {\n        if (hook.gu === pointer) { hook.gu = -1; if (hook.ah) hook.ah.hl(0); }\n      }\n      for (const record of this.fe) {\n        if (record.Ys === pointer) { record.Ys = record.Xy = -1; record.HF(false); record.IF(false); }\n      }\n      // dT/cT include click actions; removing ownership must not execute those actions.\n      for (const gear of this.Ij) gear.qe.remove(pointer);\n      for (const rail of this.Uc) {\n        if (rail.Nn === pointer) rail.Nn = -1;\n        if (rail.Wm === pointer) rail.Wm = -1;\n      }\n      for (const pump of this.vj) if (pump.LG === pointer) { pump.LG = -1; pump.zq = 0; }\n      this.vc.kz(0, 0, pointer);\n      this.vc.qe.remove(pointer);\n      if (pointer === 0 && this.mf) { this.Na.qT(); this.yg.GF(false); }\n    }\n    GV(a, b) {')
 # System pointer cancellation can emit a noncancelable touchcancel event.
 for event in ('touchcancel','touchend','touchstart'):
  old='        "'+event+'",\n        function (b) {\n          b.preventDefault();'
  new='        "'+event+'",\n        function (b) {\n          if (b.cancelable) b.preventDefault();'
  source=replace_one(source,old,new)
 # Auxiliary tutorial flags use the same durable storage boundary as progress.
 source=replace_one(source,'        let a = window.localStorage;','        let a = window.GameInterface.storage;')
 # Correct upstream v3 serializer typo: telekinesis must not become a level array.
 source=replace_one(source,'(z.wq = a.levelCleared),','(z.wq = a.telekinesisUsed),')
 # Delete the source map request; never package external SDK scripts.
 source=source.replace('//# sourceMappingURL=ctr.js.map','')
 # Inject private host persistence hook, not a debugging surface.
 source=replace_one(source,'  m.yP = [','  function cancelOfflineGamePointer(pointer) {\n    const app = ua.instance;\n    if (!app || !app.ca) return;\n    const level = app.ca.Nr(ja, app.ca);\n    if (level && level.T && level.T.Hi) level.T.cancelPointer(pointer);\n  }\n  window.CutTheRopeOffline.attachGame(function () {\n    if (z.instance && z.py) z.instance.save(function () {});\n  }, function () {\n    for (let pointer = 0; pointer < 5; pointer++) cancelOfflineGamePointer(pointer);\n    if (ua.instance && ua.instance.window) ua.instance.Yd().ZN();\n  }, function () {\n    const app = ua.instance;\n    const entry = app && app.ca ? app.ca.$v() : null;\n    const scene = entry && entry.hg;\n    return scene && scene.We === "Running" ? scene.Ea() : null;\n  });\n  m.yP = [')
 # Keep exact resource lengths for loading progress, dropping nonexistent downloads.
 match=re.search(r'  m.bytes =\s*"[^"]+"\.split\(\s*" ",?\s*\);',source)
 if not match:raise ValueError('Missing byte table')
 sizes=[p.relative_to(files).as_posix()[4:]+':'+str(p.stat().st_size) for p in sorted((files/'res').rglob('*')) if p.is_file()]
 source=source[:match.start()]+'  m.bytes = '+json.dumps(sizes,separators=(',',':'))+';'+source[match.end():]
 return source

def build(args):
 src=args.source.resolve();out=args.output.resolve();manifest=src.parent/'evidence/download-manifest.json'
 if sha(src/'ctr.min.js')!=SOURCE_SHA or sha(manifest)!=MANIFEST_SHA:raise ValueError('Pinned source or download manifest changed')
 for entry in json.loads(manifest.read_text())['files']:
  if entry.get('status')==200:
   p=src/entry['path']
   if not p.is_file() or sha(p)!=entry['sha256']:raise ValueError('Source integrity failed: '+entry['path'])
 out.parent.mkdir(parents=True,exist_ok=True)
 evidence=out.parent/'evidence';evidence.mkdir(exist_ok=True)
 with tempfile.TemporaryDirectory(prefix='ctr-content-',dir=out.parent) as temp:
  tmp=pathlib.Path(temp);decoded=tmp/'decoded';stage=tmp/'offline';stage.mkdir()
  patch((src/'ctr.min.js').read_text(),stage,[])
  subprocess.run(['node',str(ROOT/'scripts/cut-the-rope/decode-hx.cjs'),str(src/'ctr.min.js'),str(src/'res'),str(decoded)],check=True)
  conversions=[];dds_names=[]
  for p in sorted((src/'res').rglob('*')):
   if not p.is_file() or p.suffix not in {'.dds','.avif','.json','.dat','.aac','.jpg','.png'}:continue
   rel=p.relative_to(src);target=stage/rel
   if p.parent.name=='lang' and p.name.startswith('font-') and p.stem!='font-en':continue
   if p.suffix=='.dds':
    blob=bytearray((decoded/p.relative_to(src/'res')).read_bytes());height,width=struct.unpack_from('<II',blob,12)
    padded=((width+3)&~3,(height+3)&~3);struct.pack_into('<II',blob,12,padded[1],padded[0])
    original=Image.open(io.BytesIO(blob)).convert('RGBA')
    # DDS is premultiplied. PNG/Canvas requires straight RGBA; WebGL premultiplies on upload.
    straight=Image.frombytes('RGBa',original.size,original.tobytes()).convert('RGBA')
    image=straight.transpose(Image.Transpose.FLIP_TOP_BOTTOM)
    target=target.with_suffix('.png');dds_names.append(target.relative_to(stage).as_posix())
    conversions.append({'source':rel.as_posix(),'output':target.relative_to(stage).as_posix(),'logicalSize':[width,height],'size':list(padded),'codec':blob[84:88].decode(),'sourceRgbaSha256':hashlib.sha256(original.tobytes()).hexdigest(),'rgbaSha256':hashlib.sha256(straight.tobytes()).hexdigest()})
   elif p.suffix=='.avif':
    target=target.with_suffix('.png')
    if (src/rel.with_suffix('.dds')).is_file():continue
    image=Image.open(p).convert('RGBA');conversions.append({'source':rel.as_posix(),'output':target.relative_to(stage).as_posix(),'size':list(image.size),'codec':'AVIF','rgbaSha256':hashlib.sha256(image.tobytes()).hexdigest()})
   else:
    target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,target);continue
   target.parent.mkdir(parents=True,exist_ok=True);image.save(target,compress_level=6)
  source=patch((src/'ctr.min.js').read_text(),stage,dds_names)
  (stage/'ctr.js').write_text(source)
  for name in ['index.html','offline-interface.js','start.js']:shutil.copyfile(ROOT/'ThirdParty/CutTheRopeContent'/name,stage/name)
  levels=sum(len(json.loads((stage/f'res/box{box:02}/maps.json').read_text())) for box in range(1,18))
  if levels!=425:raise ValueError('Expected 425 levels')
  inputs=['scripts/cut-the-rope/build-content.py','scripts/cut-the-rope/decode-hx.cjs','ThirdParty/CutTheRopeContent/index.html','ThirdParty/CutTheRopeContent/offline-interface.js','ThirdParty/CutTheRopeContent/start.js']
  report={'buildInputs':{p:sha(ROOT/p) for p in inputs},'sourceSha256':SOURCE_SHA,'levelCount':levels,'conversions':conversions,'files':[{'path':p.relative_to(stage).as_posix(),'bytes':p.stat().st_size,'sha256':sha(p)} for p in sorted(stage.rglob('*')) if p.is_file()]}
  (stage/'manifest.json').write_text(json.dumps(report,indent=2)+'\n')
  if out.exists():shutil.rmtree(out)
  shutil.move(stage,out)
  (evidence/'conversion.json').write_text(json.dumps(conversions,indent=2)+'\n')
 if args.aar:
  aar=args.aar.resolve();aar.parent.mkdir(parents=True,exist_ok=True)
  with zipfile.ZipFile(aar,'w',zipfile.ZIP_DEFLATED) as z:
   z.writestr('AndroidManifest.xml','<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="com.xanaduyonder.cuttherope.content"/>')
   for p in sorted(out.rglob('*')):
    if p.is_file():z.write(p,'assets/cut-the-rope/'+p.relative_to(out).as_posix())
 print(json.dumps({'output':str(out),'files':len(report['files']),'levels':levels,'dds':len(dds_names),'converted':len(conversions),'aar':str(args.aar) if args.aar else None}))
if __name__=='__main__':
 parser=argparse.ArgumentParser(description=__doc__)
 parser.add_argument('--source',type=pathlib.Path,default=ROOT/'artifacts/cut-the-rope-4399-142014/game')
 parser.add_argument('--output',type=pathlib.Path,default=ROOT/'artifacts/cut-the-rope/offline')
 parser.add_argument('--aar',type=pathlib.Path)
 build(parser.parse_args())
