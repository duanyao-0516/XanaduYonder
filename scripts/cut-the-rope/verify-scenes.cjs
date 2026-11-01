#!/usr/bin/env node
// Instrument only the HTTP response in an isolated browser; never writes probes into the package.
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'/Users/bytedance/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root=path.resolve(__dirname,'../..'),out=path.join(root,'artifacts/cut-the-rope/evidence');
const base=process.env.CTR_URL||'http://127.0.0.1:8767/offline/';
(async()=>{
 const browser=await chromium.launch({executablePath:'/Applications/Google Chrome.app/Contents/MacOS/Google Chrome',headless:true,args:['--autoplay-policy=no-user-gesture-required','--use-angle=swiftshader','--enable-unsafe-swiftshader']});
 const report={viewports:[],errors:[],external:[],failed:[]};
 try {
  for(const viewport of [{name:'desktop',width:960,height:720},{name:'phone',width:390,height:844}]){
   const context=await browser.newContext({viewport,deviceScaleFactor:viewport.name==='phone'?2:1,isMobile:viewport.name==='phone',hasTouch:viewport.name==='phone'}),page=await context.newPage();
   page.on('pageerror',e=>report.errors.push(e.stack));page.on('console',m=>{if(m.type()==='error')report.errors.push(m.text())});page.on('requestfailed',r=>report.failed.push({url:r.url(),error:r.failure()}));
   await page.route('**/*',async route=>{
    const url=route.request().url();if(!url.startsWith(base)&&!url.startsWith('blob:')&&!url.startsWith('data:')){report.external.push(url);return route.abort()}
    if(url===base+'ctr.js'){
     const source=fs.readFileSync(path.join(root,'artifacts/cut-the-rope/offline/ctr.js'),'utf8');
     assert.equal(source.split('  m.yP = [').length,2);
     return route.fulfill({contentType:'text/javascript',body:source.replace('  m.yP = [','  window.__ctr = {ua,m,w,z,Rb,ja,ac,Xd,tf,p,N,nd,od,Gc};\n  m.yP = [')});
    }
    return route.continue();
   });
   await page.goto(base);await page.waitForFunction(()=>window.__ctr?.ua.instance.ca.$v()?.hg?.Ea()==='FamobiMenuScene',null,{timeout:60000});
   await page.screenshot({path:path.join(out,viewport.name+'-menu-scenes.png')});
   const results=await page.evaluate(async()=>{
    const {ua,m,w,z,Rb,p}=__ctr,u=ua.instance,results=[];
    u.zf.stop();
    for(let box=1;box<=17;box++){
     u.ca.clear();if(box>1)p.ul(box-1);
     w.Jd(box<=5?1:box<=10?2:3);w.jg(box);w.bi(1);
     const required=Object.assign(Object.create(Rb.prototype),{system:u}).qc();
     for(const id of required){if(m.Vi(id)==null)throw new Error('Missing route '+id+' in box '+box)}
     const loader=u.Hm(Rb),deadline=Date.now()+60000;
     while(!loader.Fw()){if(Date.now()>deadline)throw new Error('Box '+box+' load timeout');await new Promise(r=>setTimeout(r,20));}
     for(let level=1;level<=25;level++){
      u.ca.clear();w.bi(level);u.ca.rh(Rb);
      const scene=u.ca.$v()?.hg;
      if(!(scene instanceof Rb)||!scene.T)throw new Error('No game world at '+box+'-'+level);
      // Let the real scene transition call an()/bb(), then draw its original map.
      try { for(let i=0;i<80;i++)u.update(0.016); } catch(error) { throw new Error(box+'-'+level+': '+error.stack); }
      u.jd(1);
      if(!scene.T.Na)throw new Error('Candy not instantiated at '+box+'-'+level);
      window.__sceneProgress={box,level};results.push({box,level,resources:required.map(id=>m.Vi(id)),world:true});
     }
    }
    return results;
   });
   assert.equal(results.length,425);
   await page.screenshot({path:path.join(out,viewport.name+'-level425.png')});
   // Test the final result's real outro continuation without synthesizing video completion.
   const final=await page.evaluate(async()=>{
    const {ua,z,w,tf}=__ctr,u=ua.instance,scene=u.ca.$v().hg;
    for(let b=0;b<17;b++)for(let l=0;l<25;l++){z.nf[b][l]=3;z.ae[b][l]=true}
    z.Ri=false;scene.Tp=3;scene.Ag=false;scene.cG();
    for(let i=0;i<300;i++){u.update(.016);await Promise.resolve();}
    const result=u.ca.Nr(tf,u.ca);u.jd(1);CutTheRopeOffline.flush();
    return {box:w.box,level:w.level,gameWon:z.Ri,savedGameWon:JSON.parse(localStorage.getItem('cut-the-rope:CTR')).gameWon,resultState:result?.state,video:document.querySelectorAll('video').length};
   });
   assert.equal(final.gameWon,true);assert.equal(final.savedGameWon,true);assert.equal(final.video,0);assert.equal(final.resultState,7);
   await page.screenshot({path:path.join(out,viewport.name+'-final-result.png')});
   report.viewports.push({viewport,levels:results,final});console.log(viewport.name,results.length,final);
   await context.close();
  }
  assert.deepEqual(report.errors,[]);assert.deepEqual(report.external,[]);assert.deepEqual(report.failed,[]);
 } finally {fs.writeFileSync(path.join(out,'all-levels.json'),JSON.stringify(report,null,2));await browser.close()}
})().catch(e=>{console.error(e);process.exitCode=1});
