#!/usr/bin/env node
// Test hooks exist only in intercepted responses, never in generated game files.
const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm');
const assert=require('node:assert/strict'),crypto=require('node:crypto');
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'/Users/bytedance/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root=path.resolve(__dirname,'../..'),out=path.join(root,'artifacts/cut-the-rope/language');
const base=process.env.CTR_URL||'http://127.0.0.1:8767/offline/';
const source=fs.readFileSync(path.join(root,'artifacts/cut-the-rope/offline/ctr.js'),'utf8');
function savedProgress(){
 const window={CutTheRopeOffline:{attachGame(){}},document:{body:{style:{}}},addEventListener(){}};
 const context=vm.createContext({window,console,TextEncoder,TextDecoder});
 vm.runInContext(source.replace('  m.yP = [','  globalThis.saveClass=z;\n  m.yP = ['),context);
 const value=JSON.parse(new context.saveClass({}).stringify());
 value.language='nl';value.levelStars[0][0]=3;value.levelStars[0][4]=2;
 value.levelCleared[0][0]=true;value.blueStars[0][0]=1;value.locked[1]=false;
 value.pictures=['1-1'];value.picturesBadgeCounter=1;value.skin=1;
 value.magnetUsed=true;value.telekinesisUsed=true;
 return value;
}
(async()=>{
 fs.mkdirSync(out,{recursive:true});
 const report={gameSha256:crypto.createHash('sha256').update(source).digest('hex'),viewports:[],errors:[],external:[],failed:[]};
 const browser=await chromium.launch({executablePath:'/Applications/Google Chrome.app/Contents/MacOS/Google Chrome',headless:true,args:['--autoplay-policy=no-user-gesture-required','--use-angle=swiftshader','--enable-unsafe-swiftshader']});
 try{
  for(const viewport of [{name:'desktop',width:960,height:720},{name:'phone',width:390,height:844}]){
   const phone=viewport.name==='phone',context=await browser.newContext({viewport,deviceScaleFactor:phone?2:1,isMobile:phone,hasTouch:phone});
   const before=savedProgress(),expected={...before,language:'en'},page=await context.newPage(),requests=[];
   await page.addInitScript(value=>{
    if(!localStorage.getItem('cut-the-rope:CTR'))localStorage.setItem('cut-the-rope:CTR',JSON.stringify(value));
    window.__languageWrites=[];
    const set=Storage.prototype.setItem;
    Storage.prototype.setItem=function(key,value){if(key==='cut-the-rope:CTR')window.__languageWrites.push(value);return set.call(this,key,value)};
   },before);
   page.on('pageerror',error=>report.errors.push(error.stack));
   page.on('console',message=>{if(message.type()==='error')report.errors.push(message.text())});
   page.on('requestfailed',request=>report.failed.push({url:request.url(),error:request.failure()}));
   await page.route('**/*',route=>{
    const url=route.request().url();requests.push(url);
    if(!url.startsWith(base)&&!url.startsWith('blob:')&&!url.startsWith('data:')){report.external.push(url);return route.abort()}
    if(url===base+'ctr.js')return route.fulfill({contentType:'text/javascript',body:source.replace('  m.yP = [','  window.__ctr={ua,z,m,F};\n  m.yP = [')});
    return route.continue();
   });
   const waitScene=async name=>{await page.waitForFunction(name=>{const scene=window.__ctr?.ua.instance.ca.$v()?.hg;return scene?.Ea()===name&&scene.We==='Running'},name,{timeout:60000});await page.waitForTimeout(100)};
   const snapshot=()=>page.evaluate(()=>{const scene=__ctr.ua.instance.ca.$v().hg;return{scene:scene.Ea(),language:__ctr.z.language,resourceLanguage:__ctr.m.ww(),writes:__languageWrites.length,save:JSON.parse(localStorage.getItem('cut-the-rope:CTR')),buttons:scene.buttons.map((button,index)=>button?{index,visible:button.Sf(),text:button.Fc?.effect.text,y:button.Xa()}:null)}});
   const click=async index=>{
    const point=await page.evaluate(index=>{const button=__ctr.ua.instance.ca.$v().hg.buttons[index],position=button.l.hz(new __ctr.F(button.lc.x/2,button.lc.y/2,0,1));return{x:position.x/devicePixelRatio,y:position.y/devicePixelRatio}},index);
    if(phone)await page.touchscreen.tap(point.x,point.y);else await page.mouse.click(point.x,point.y);
   };
   await page.goto(base);await waitScene('FamobiMenuScene');
   const menu=await snapshot();assert.equal(menu.language,'en');assert.equal(menu.resourceLanguage,'en');assert.equal(menu.buttons[1].text,'Play');assert.equal(menu.writes,1);assert.deepEqual(menu.save,expected);
   await page.screenshot({path:path.join(out,viewport.name+'-english-menu.png')});
   await click(2);await waitScene('FamobiOptionsScene');
   const options=await snapshot();assert.equal(options.buttons[3].visible,false);assert.equal(options.buttons[4].visible,true);assert.equal(options.buttons[4].text,'Reset game');assert.equal(options.buttons[4].y,420);
   await page.screenshot({path:path.join(out,viewport.name+'-english-options.png')});
   await click(1);await page.waitForFunction(()=>__ctr.z.Ld===false);await click(1);await page.waitForFunction(()=>__ctr.z.Ld===true);
   await click(2);await page.waitForFunction(()=>__ctr.z.Nc===false);await click(2);await page.waitForFunction(()=>__ctr.z.Nc===true);
   await click(4);await waitScene('FamobiResetScene');
   const reset=await snapshot();assert.equal(reset.buttons[1].text,'Yes');assert.equal(reset.buttons[2].text,'No');
   await page.screenshot({path:path.join(out,viewport.name+'-english-reset.png')});
   await click(2);await waitScene('FamobiOptionsScene');await click(0);await waitScene('FamobiMenuScene');
   assert.deepEqual((await snapshot()).save,expected);
   await page.reload();await waitScene('FamobiMenuScene');const reload=await snapshot();assert.equal(reload.writes,0);assert.deepEqual(reload.save,expected);
   const fonts=[...new Set(requests.filter(url=>url.includes('/lang/')).map(url=>url.substring(base.length)))].sort();
   assert.deepEqual(fonts,['res/lang/font-en.dat','res/lang/font-en.png']);
   report.viewports.push({viewport,migrationWrites:menu.writes,reloadWrites:reload.writes,language:reload.language,progressPreserved:true,languageHidden:!options.buttons[3].visible,resetY:options.buttons[4].y,soundAndMusicToggled:true,resetDialogCancelled:true,fontRequests:fonts});
   await context.close();
  }
  assert.deepEqual(report.errors,[]);assert.deepEqual(report.external,[]);assert.deepEqual(report.failed,[]);
  console.log(JSON.stringify(report,null,2));
 }finally{fs.writeFileSync(path.join(out,'browser.json'),JSON.stringify(report,null,2)+'\n');await browser.close()}
})().catch(error=>{console.error(error);process.exitCode=1});
