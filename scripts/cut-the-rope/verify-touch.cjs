#!/usr/bin/env node
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict'),crypto=require('node:crypto');
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'/Users/bytedance/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root=path.resolve(__dirname,'../..'),out=path.join(root,'artifacts/cut-the-rope/evidence'),base=process.env.CTR_URL||'http://127.0.0.1:8767/offline/';
(async()=>{
 const report={errors:[],external:[],failed:[],checks:[]};
 const source=fs.readFileSync(path.join(root,'artifacts/cut-the-rope/offline/ctr.js'),'utf8');report.gameSha256=crypto.createHash('sha256').update(source).digest('hex');
 const browser=await chromium.launch({executablePath:'/Applications/Google Chrome.app/Contents/MacOS/Google Chrome',headless:true,args:['--autoplay-policy=no-user-gesture-required','--use-angle=swiftshader','--enable-unsafe-swiftshader']});
 try {
  const page=await browser.newPage({viewport:{width:390,height:844},deviceScaleFactor:2,isMobile:true,hasTouch:true});
  page.on('pageerror',e=>report.errors.push(e.stack));page.on('console',m=>{if(m.type()==='error')report.errors.push(m.text())});page.on('requestfailed',r=>report.failed.push({url:r.url(),error:r.failure()}));
  await page.route('**/*',route=>{const url=route.request().url();if(!url.startsWith(base)&&!url.startsWith('blob:')&&!url.startsWith('data:')){report.external.push(url);return route.abort()}
   if(url===base+'ctr.js')return route.fulfill({contentType:'text/javascript',body:source.replace('  m.yP = [','  window.__ctr={ua,w,z};\n  m.yP = [')});return route.continue();});
  await page.goto(base);await page.waitForFunction(()=>window.__ctr?.ua.instance.ca.$v()?.hg?.Ea()==='FamobiMenuScene',null,{timeout:60000});await page.waitForTimeout(500);
  const cdp=await page.context().newCDPSession(page),point={x:195,y:484};
  const start=()=>cdp.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[point]});
  const end=()=>cdp.send('Input.dispatchTouchEvent',{type:'touchEnd',touchPoints:[]});
  const scene=()=>page.evaluate(()=>__ctr.ua.instance.ca.$v()?.hg?.Ea());
  const held={id:1,x:10,y:10};
  await cdp.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[held]});
  for(let id=2;id<12;id++){
   await cdp.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[held,{id,x:25,y:25}]});
   await cdp.send('Input.dispatchTouchEvent',{type:'touchEnd',touchPoints:[held]});
  }
  await end();await page.waitForTimeout(250);assert.equal(await scene(),'FamobiMenuScene');assert.equal(await page.evaluate(()=>__ctr.ua.instance.Gw().Ix),0);report.checks.push('repeated secondary finger taps reuse bounded slots');
  // Hold the frame scheduler only to force DOWN and CANCEL into one unconsumed frame.
  await page.evaluate(()=>__ctr.ua.instance.zf.stop());await start();await cdp.send('Input.dispatchTouchEvent',{type:'touchCancel',touchPoints:[]});await page.evaluate(()=>__ctr.ua.instance.zf.start());await page.waitForTimeout(500);assert.equal(await scene(),'FamobiMenuScene');report.checks.push('pointercancel does not click Play');
  // A completed DOM tap may still await consumption when Android backgrounds the Activity.
  await page.evaluate(()=>__ctr.ua.instance.zf.stop());await start();await end();await page.evaluate(()=>{CutTheRopeOffline.setHostPaused(true);CutTheRopeOffline.setHostPaused(false)});await page.waitForTimeout(500);assert.equal(await scene(),'FamobiMenuScene');report.checks.push('pause cancels an ended but unconsumed tap');
  await page.evaluate(()=>__ctr.ua.instance.zf.stop());await start();await end();await page.evaluate(()=>__ctr.ua.instance.zf.start());
  await page.waitForFunction(()=>__ctr.ua.instance.ca.$v()?.hg?.Ea()==='FamobiLevelScene'&&__ctr.ua.instance.ca.$v().hg.state===1,null,{timeout:60000});report.checks.push('sub-frame touch tap enters first level');
  await page.screenshot({path:path.join(out,'phone-quick-tap-level1.png')});
  await cdp.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[{x:80,y:275}]});
  for(let x=90;x<=300;x+=10){await cdp.send('Input.dispatchTouchEvent',{type:'touchMove',touchPoints:[{x,y:275}]});await page.waitForTimeout(20)}await end();
  await page.waitForFunction(()=>JSON.parse(localStorage.getItem('cut-the-rope:CTR')).levelStars[0][0]===3,null,{timeout:30000});await page.waitForTimeout(3000);
  report.checks.push('continuous touch swipe wins first level with3stars');await page.screenshot({path:path.join(out,'phone-touch-success.png')});
  assert.deepEqual(report.errors,[]);assert.deepEqual(report.external,[]);assert.deepEqual(report.failed,[]);console.log(JSON.stringify(report));
 }finally{fs.writeFileSync(path.join(out,'touch.json'),JSON.stringify(report,null,2));await browser.close()}
})().catch(error=>{console.error(error);process.exitCode=1});
