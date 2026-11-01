#!/usr/bin/env node
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'/Users/bytedance/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root=path.resolve(__dirname,'../..'),out=path.join(root,'artifacts/cut-the-rope/evidence');
const phone=process.argv.includes('--phone');
const base=process.env.CTR_URL||'http://127.0.0.1:8767/offline/';
(async()=>{
 fs.mkdirSync(out,{recursive:true});
 const browser=await chromium.launch({executablePath:'/Applications/Google Chrome.app/Contents/MacOS/Google Chrome',headless:true,args:['--autoplay-policy=no-user-gesture-required','--use-angle=swiftshader','--enable-unsafe-swiftshader']});
 const page=await browser.newPage({viewport:phone?{width:390,height:844}:{width:960,height:720},isMobile:phone,hasTouch:phone,deviceScaleFactor:phone?2:1}),errors=[],external=[],failed=[];
 page.on('console',m=>{if(m.type()==='error')errors.push(m.text())});page.on('pageerror',e=>errors.push(e.stack));
 page.on('requestfailed',r=>failed.push({url:r.url(),error:r.failure()}));
 await page.route('**/*',async route=>{const url=route.request().url();if(!url.startsWith(base)&&!url.startsWith('blob:')&&!url.startsWith('data:')){external.push(url);return route.abort()}
 if(url===base+'ctr.js'){
  let body=fs.readFileSync(path.join(root,'artifacts/cut-the-rope/offline/ctr.js'),'utf8');
  body=body.replace('  m.yP = [','  window.__ctr = {ua,m,w,z,Rb,ja,ac,X d:Xd,tf,p,N,nd};\n  m.yP = ['.replace('X d','Xd'));
  return route.fulfill({contentType:'text/javascript',body});
 }
 return route.continue();});
 if(process.argv.includes('--canvas'))await page.addInitScript(()=>{const original=HTMLCanvasElement.prototype.getContext;HTMLCanvasElement.prototype.getContext=function(type,...rest){return /webgl/i.test(type)?null:original.call(this,type,...rest)}});
 await page.goto(base,{waitUntil:'load'});await page.waitForTimeout(8000);
 await page.screenshot({path:path.join(out,(phone?'phone':'desktop')+'-menu.png')});
 console.log('MENU',await page.evaluate(()=>({canvas:!!document.querySelector('canvas'),state:window.__ctr?.ua.instance.ca.$v()?.hg?.Ea()})),errors,failed);
 if(phone){const cdp=await page.context().newCDPSession(page);await cdp.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[{x:195,y:484}]});await page.waitForTimeout(250);await cdp.send('Input.dispatchTouchEvent',{type:'touchEnd',touchPoints:[]})}else await page.mouse.click(480,468,{delay:200});await page.waitForTimeout(6000);await page.screenshot({path:path.join(out,(phone?'phone':'desktop')+'-level1.png')});
 console.log('LEVEL' ,await page.evaluate(()=>{const c=__ctr.ua.instance.ca;return {keys:Object.keys(c.$v()||{}),names:c.He&&c.He.hg?.Ea(),m:__ctr.m.resolution,box:__ctr.w.box,level:__ctr.w.level,state:c.$v()?.hg?.state}}));
 if(phone){fs.writeFileSync(path.join(out,'phone-input.json'),JSON.stringify({errors,external,failed,level:await page.evaluate(()=>__ctr.w.level)},null,2));await browser.close();return;}
 await page.mouse.move(330,180);await page.mouse.down();for(let x=350;x<=630;x+=20){await page.mouse.move(x,180);await page.waitForTimeout(20)}await page.mouse.up();await page.waitForTimeout(6000);await page.screenshot({path:path.join(out,'desktop-success.png')});console.log('WIN',await page.evaluate(()=>JSON.parse(localStorage.getItem('cut-the-rope:CTR')).levelStars[0][0]),errors);await page.mouse.click(480,668,{delay:200});await page.waitForTimeout(2500);await page.screenshot({path:path.join(out,'desktop-result.png')});await page.mouse.click(480,640,{delay:200});await page.waitForTimeout(3500);await page.screenshot({path:path.join(out,'desktop-level2.png')});console.log('NEXT',await page.evaluate(()=>({box:__ctr.w.box,level:__ctr.w.level,state:__ctr.ua.instance.ca.$v()?.hg?.Ea()})));assert.equal(await page.evaluate(()=>__ctr.w.level),2);
 fs.writeFileSync(path.join(out,'browser-smoke.json'),JSON.stringify({errors,external,failed},null,2));
 await browser.close();if(errors.length||external.length||failed.length)process.exitCode=1;
})().catch(e=>{console.error(e);process.exitCode=1});
