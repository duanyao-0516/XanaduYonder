#!/usr/bin/env python3
"""Reproduce the pinned offline release; --verify compares without writing."""
import argparse
import hashlib
import io
import json
from pathlib import Path
import re
import zipfile
from verify_original import verify as verify_original

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'ThirdParty/LevelDevil'
OUTPUT = SOURCE / 'generated'
AAR = ROOT / 'Assets/Plugins/Android/LevelDevilContent.aar'
JS_HASH = '5af62be64b44d294acec64c695ad1afe933856d581749992b98924e4cff7217f'

def sha(data):
    return hashlib.sha256(data).hexdigest()

def patch(source):
    assert sha(source) == JS_HASH, 'Unreviewed game release'
    text = source.decode('utf-8')
    patches = []
    def replace(label, old, new, count=1):
        nonlocal text
        actual = text.count(old)
        assert actual == count, f'{label}: expected {count} matches, found {actual}'
        text = text.replace(old, new)
        patches.append({'name': label, 'matches': count, 'originalSha256': sha(old.encode()), 'replacementSha256': sha(new.encode())})
    def span(label, start, end, replacement):
        assert text.count(start) == 1 and text.count(end) == 1, label
        begin = text.index(start); finish = text.index(end, begin)
        replace(label, text[begin:finish], replacement)
    replace('local boot', 'checkSiteLock:function(){var a=this;A.pokiSDK=PokiSDK;A.pokiSDK.init().then(function(b){a.pokiSdkLoaded()}).catch(function(b){A.adBlock=!0;a.pokiSdkLoaded()})}',
            'checkSiteLock:function(){var a=this;Promise.resolve().then(function(){a.pokiSdkLoaded()})}')
    # Universal.initScreen selects IMG_BASE before the packed library loads.
    # The release preloads all four equivalent sprite sets (1,709 images).
    # Decode only the selected 425 sprites plus nine shared images; retain the
    # entire checked PAK and the original loadImage/getImage paths for all scales.
    # A later original fullscreen action calls initScreen again. Keep the
    # texture resolution selected for this WebView lifetime: its synchronous
    # reloadScaledResources cannot consume an as-yet undecoded HTML image.
    # Root/display scaling below this selection still follows the viewport.
    replace('retain preloaded image scale on screen reload',
            'f=ha.forceHiResAssets?f:d;4==f&&',
            'f=ha.forceHiResAssets?f:d;null!=ba.xanaduAssetScale&&(f=ba.xanaduAssetScale);4==f&&')
    replace('preload selected image scale',
            'if(null==this.promise){this.promise=new de;this.bytesLoadedCache=new pa;this.assetsLoaded=0;this.assetsTotal=2;',
            'if(null==this.promise){if(["1x","1.5x","2x","4x"].indexOf(k.IMG_BASE)<0)throw Error("Image scale not initialized");ba.xanaduAssetScale=k.SCALE;'
            'var ldImages=0;for(var ldPath of Object.keys(this.preload.h)){if(this.preload.h[ldPath]&&this.types.h[ldPath]==="IMAGE"){'
            'var ldScale=/^assets\\/graphics\\/(1x|1\\.5x|2x|4x)\\//.exec(ldPath);'
            'if(ldScale&&ldScale[1]!==k.IMG_BASE){this.preload.h[ldPath]=false;this.bytesTotal-=this.sizes.h[ldPath]||0}else ldImages++}}'
            'xanaduStartup("preload-selected",{scale:k.IMG_BASE,images:ldImages});'
            'this.promise=new de;this.bytesLoadedCache=new pa;this.assetsLoaded=0;this.assetsTotal=2;')
    replace('preload completion timing',
            'this.loaded=!0,this.promise.progress(this.bytesTotal,this.bytesTotal),this.promise.complete(this)',
            'this.loaded=!0,xanaduStartup("preload-complete",{scale:k.IMG_BASE}),this.promise.progress(this.bytesTotal,this.bytesTotal),this.promise.complete(this)')
    replace('engine startup timing',
            'za.preloaderComplete=function(){try{new k(za.universal,',
            'za.preloaderComplete=function(){xanaduStartup("engine-begin");try{new k(za.universal,')
    # Scene 61 loads the save synchronously and finishes its original setup
    # before its first update. Skip only this WebView's initial presentation
    # delays; subsequent visits retain the original wait, fades and curtains.
    # These flags live in the release closure, never in serialized attributes.
    replace('skip first boot wait',
            'c.runLater(100,function(b){a._ReadyToTransition=!0},null);k.engine.getGameAttribute("2P_Level_Order").push(0);',
            'if(xanaduInitialLoadPending)a._ReadyToTransition=!0;else c.runLater(100,function(b){a._ReadyToTransition=!0},null);k.engine.getGameAttribute("2P_Level_Order").push(0);')
    replace('skip first boot fades',
            'this._ReadyToTransition=!1;a=F.get().scenes.h[125].getID();var b=0,d=0,f=0;',
            'this._ReadyToTransition=!1;a=F.get().scenes.h[125].getID();if(xanaduInitialLoadPending){xanaduInitialLoadPending=!1;xanaduInitialMenuPending=!0;c.switchScene(a,null,null);return}var b=0,d=0,f=0;')
    replace('skip initial menu curtains',
            'et.prototype=q(I.prototype,{init:function(){c.engine.moveCamera(c.getSceneWidth()/2,0);c.createRecycledActor(c.getActorType(710)',
            'et.prototype=q(I.prototype,{init:function(){c.engine.moveCamera(c.getSceneWidth()/2,0);if(xanaduInitialMenuPending){xanaduInitialMenuPending=!1;return}c.createRecycledActor(c.getActorType(710)')
    span('remove platform SDK boundary', 'A.gameplayStart=function()', 'A.__super__=nf;',
         'A.gameplayStart=function(){A.stopped=!1};A.gameplayStop=function(){A.stopped=!0};A.localContinue=function(a){xanaduLocalAdapter.continueTransition(a)};A.happyTime=function(){};A.captureError=function(){};\n')
    replace('remove loading telemetry', 'initialize:function(){A.pokiSDK.gameLoadingFinished()}', 'initialize:function(){}')
    replace('local transition continuation', 'A.commercialBreak(function', 'A.localContinue(function', 4)
    # Pin all three reward call sites, preserving their distinct game semantics.
    replace('explicit speedrun unlock', 'A.rewardedBreak(function(a){c.engine.unpause();a&&(k.engine.gameAttributes.h.Speedrun_Unlocked=', 'xanaduLocalAdapter.reward("speedrun-unlock",function(a){c.engine.unpause();a&&(k.engine.gameAttributes.h.Speedrun_Unlocked=', 2)
    replace('explicit optional skip', 'A.rewardedBreak(function(a){c.engine.unpause();\nc.runLater(500,', 'xanaduLocalAdapter.reward("optional-skip",function(a){c.engine.unpause();\nc.runLater(500,')
    replace('completion transactions', '_customEvent_level_complete:function(){var a=this;A.gameplayStop();', '_customEvent_level_complete:function(){var ldCompletion=xanaduLocalAdapter.beginCompletion();var a=this;A.gameplayStop();', 2)
    saves = re.findall(r'c\.runLater\(2009\.9999999999998,\s*function\(a\)\{c\.saveGame\("mySave",\s*function\(a\)\{\}\)\},null\)', text)
    assert len(saves) == 2, 'Completion save checkpoints changed'
    for index, original in enumerate(saves):
        replace(f'completion save result {index+1}', original, 'c.runLater(2009.9999999999998,function(){c.saveGame("mySave",function(ok){xanaduLocalAdapter.finishCompletion(ldCompletion,ok)})},null)')
    span('remove unused web helpers', 'c.openURLInBrowser=function', 'c.convertToPseudoUnicode=function', 'c.openURLInBrowser=function(){};c.visitURL=function(){};c.postToURL=function(){};')
    replace('remove social navigation', 'c.simpleTweet=function(a,b){c.openURLInBrowser("http://twitter.com/home?status="+encodeURIComponent(a+" "+b))}', 'c.simpleTweet=function(){}')
    span('remove remote image helper', 'c.loadImageFromURL=function', 'c.getSubImage=function', 'c.loadImageFromURL=function(){};')
    span('remove external navigation', 'Va.openFile=function', 'Va.__copyMissingFields=function', 'Va.openFile=function(){};Va.openURL=function(){};')
    replace('remove unused motion listener', 'Object.prototype.hasOwnProperty.call(window,\n"Accelerometer")&&window.addEventListener("devicemotion",r(this,this.handleSensorEvent),!1);', '')
    # FileSaver is an unused export/download library following Howler.
    span('remove unused export library', 'var saveAs=saveAs||function', '"function"==typeof define&&define.__amd', 'var saveAs=function(){};\n')
    replace('private adapter install', 'Jb.period=3E-4;za.main()', 'Jb.period=3E-4;var xanaduInitialLoadPending=!0,xanaduInitialMenuPending=!1;function xanaduStartup(stage,details){console.info("LevelDevilStartup:"+JSON.stringify(Object.assign({stage:stage,ms:Math.round(performance.now())},details||{})))}xanaduStartup("runtime-start");var xanaduLocalAdapter=window.__installLevelDevil({Engine:k,Script:c,Input:Q,SharedObject:nd,Serializer:He,Unserializer:Pe,Utils:Y,Timer:Ga,classes:l});delete window.__installLevelDevil;delete window.__installLevelDevilControls;za.main()')
    for forbidden in ('PokiSDK', 'pokiSDK', 'A.rewardedBreak', 'A.commercialBreak', 'window.open(', 'twitter.com'):
        assert forbidden not in text, f'Production retains {forbidden}'
    return text.encode(), patches

def build():
    files = {}
    manifest = json.loads((SOURCE / 'source-manifest.json').read_text())
    expected_paths = {item['path'] for item in manifest['files']}
    assert len(expected_paths) == 21
    assert {str(p.relative_to(SOURCE / 'original')) for p in (SOURCE / 'original').rglob('*') if p.is_file()} == expected_paths
    for item in manifest['files']:
        data = (SOURCE / 'original' / item['path']).read_bytes()
        assert len(data) == item['bytes'] and sha(data) == item['sha256'], f"Source modified: {item['path']}"
        files[item['path']] = data
    verify_original(SOURCE / 'original')
    files['Level Devil.js'], patches = patch(files['Level Devil.js'])
    schema = (SOURCE / 'adapter/save-schema.json').read_text()
    files['host.js'] = ('window.__levelDevilSaveSchema=' + schema.strip() + ';\n').encode() + (SOURCE / 'adapter/host.js').read_bytes()
    files['menu.js'] = (SOURCE / 'adapter/menu.js').read_bytes()
    files['controls.js'] = (SOURCE / 'adapter/controls.js').read_bytes()
    files['index.html'] = (SOURCE / 'adapter/index.html').read_bytes()
    files['entry.js'] = (SOURCE / 'adapter/entry.js').read_bytes()
    files['style.css'] = (SOURCE / 'adapter/style.css').read_bytes()
    types = {'.html':'text/html','.js':'application/javascript','.css':'text/css','.mp3':'audio/mpeg','.pak':'application/octet-stream','.png':'image/png'}
    content_manifest = {'files':[{'path':name,'sha256':sha(data),'bytes':len(data),'mime':types[Path(name).suffix]} for name,data in sorted(files.items())]}
    files['manifest.json'] = (json.dumps(content_manifest,ensure_ascii=False,indent=2)+'\n').encode()
    archive = io.BytesIO()
    with zipfile.ZipFile(archive,'w',compression=zipfile.ZIP_STORED) as output:
        entries = {'AndroidManifest.xml':b'<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="com.xanaduyonder.leveldevil.content"><uses-sdk android:minSdkVersion="26" android:targetSdkVersion="36" /></manifest>', **{'assets/level-devil/'+name:data for name,data in files.items()}}
        for name,data in sorted(entries.items()):
            info=zipfile.ZipInfo(name,(2026,1,1,0,0,0));info.compress_type=zipfile.ZIP_STORED;info.external_attr=0o644<<16
            output.writestr(info,data)
    return files, archive.getvalue(), patches

def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--verify',action='store_true');args=parser.parse_args()
    files,archive,patches=build()
    if args.verify:
        assert AAR.is_file() and AAR.read_bytes()==archive, 'Content AAR is missing or stale; run build-content.py'
        assert {str(p.relative_to(OUTPUT)) for p in OUTPUT.rglob('*') if p.is_file()} == set(files), 'Generated file set differs'
        for name,data in files.items(): assert (OUTPUT/name).read_bytes()==data, f'Stale generated file: {name}'
        assert json.loads((SOURCE/'patch-report.json').read_text())==patches, 'Patch report is stale'
    else:
        OUTPUT.mkdir(parents=True,exist_ok=True)
        for name,data in files.items():
            target=OUTPUT/name;target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(data)
        for p in OUTPUT.rglob('*'):
            if p.is_file() and str(p.relative_to(OUTPUT)) not in files: p.unlink()
        AAR.parent.mkdir(parents=True,exist_ok=True);AAR.write_bytes(archive)
        (SOURCE/'patch-report.json').write_text(json.dumps(patches,indent=2)+'\n')
    print(json.dumps({'verified':args.verify,'files':len(files),'aarBytes':len(archive),'aarSha256':sha(archive),'patches':len(patches)}))

if __name__=='__main__':main()
