const {test}=require('node:test');
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),vm=require('node:vm');
const root=path.resolve(__dirname,'../..'),folder=path.join(root,'artifacts/cut-the-rope/offline');
function game(native){
 const window={CutTheRopeNative:native,CutTheRopeOffline:{attachGame(){}},document:{body:{style:{}},addEventListener(){}},addEventListener(){},dispatchEvent(){},console,CustomEvent:class{}};
 const context=vm.createContext({window,console,TextDecoder,TextEncoder});
 if(native)vm.runInContext(fs.readFileSync(path.join(root,'ThirdParty/CutTheRopeContent/offline-interface.js'),'utf8'),context);
 let source=fs.readFileSync(path.join(folder,'ctr.js'),'utf8');
 source=source.replace('  m.yP = [','  globalThis.testGame={m,z,p,kf,Ld,Ne,Od};\n  m.yP = [');
 vm.runInContext(source,context);return context.testGame;
}
function fixture(z,version=3){
 const save=new z({});const value=JSON.parse(save.stringify());
 value.v=version;value.language='nl';value.music=false;value.sound=true;
 value.levelStars[0][0]=3;value.levelStars[1][4]=2;value.levelCleared[0][0]=true;
 value.locked[1]=false;value.skin=2;value.hint=4;value.gameWon=true;
 if(version===3){value.blueStars[0][0]=1;value.pictures=['1-1'];value.picturesBadgeCounter=1;value.magnetUsed=true;value.telekinesisUsed=true;}
 else for(const key of ['blueStars','pictures','picturesBadgeCounter','magnetUsed','telekinesisUsed'])delete value[key];
 return value;
}
test('all language setters resolve to English without changing bitmap layer indices',()=>{
 const {m,z,p,kf}=game();assert.deepEqual(Array.from(m.nw()),['en']);
 for(const locale of ['nl','ru','ja','zh','en',null]){z.ei(locale);assert.equal(z.language,'en');assert.equal(m.ai(locale),'en');}
 assert.equal(kf.length,10);assert.equal(p.tw('en','en'),2);
});
test('a v3 Dutch save migrates only its language and writes once',()=>{
 const {z}=game(),before=fixture(z),writes=[];let done=0;
 const storage={load(callback){callback(JSON.stringify(before))},save(value,callback){writes.push(JSON.parse(value));callback(0)}};
 new z(storage).load(()=>done++);
 assert.equal(done,1);assert.equal(z.language,'en');assert.equal(writes.length,1);
 assert.deepEqual(writes[0],{...before,language:'en'});
 const normalized=JSON.stringify(writes[0]);storage.load=callback=>callback(normalized);writes.length=0;
 new z(storage).load(()=>done++);assert.equal(writes.length,0);assert.equal(done,2);
});
test('a v2 Dutch save persists English in its existing upgrade write',()=>{
 const {z}=game(),before=fixture(z,2),writes=[];
 new z({load(callback){callback(JSON.stringify(before))},save(value,callback){writes.push(JSON.parse(value));callback(0)}}).load(()=>{});
 assert.equal(writes.length,1);assert.equal(writes[0].v,3);assert.equal(writes[0].language,'en');
 for(const key of ['music','sound','levelStars','levelCleared','locked','skin','hint','gameWon'])assert.deepEqual(writes[0][key],before[key],key);
});
test('a failed language migration does not reset or overwrite saved progress',()=>{
 const {z}=game(),before=fixture(z),persisted=JSON.stringify(before);let writes=0,done=false;
 const save=new z({load(callback){callback(persisted)},save(){writes++;throw Error('durable write rejected')}});
 assert.throws(()=>save.load(()=>{done=true}),/durable write rejected/);
 assert.equal(writes,1);assert.equal(done,false);assert.deepEqual(JSON.parse(save.stringify()),{...before,language:'en'});
 assert.equal(JSON.parse(persisted).language,'nl');
});
for(const version of [1,2])test('v'+version+' native upgrade failure cannot reset or retry over old progress',()=>{
 let persisted,writes=0,done=false;const errors=[];
 const {z,Od}=game({readSave(){return persisted},writeSave(key,value){writes++;if(writes===1)return false;persisted=value;return true},reportError(error){errors.push(error)}});
 const before=fixture(z,version);persisted=JSON.stringify(before);const original=persisted;
 const save=new z(new Od('CTR'));
 assert.throws(()=>save.load(()=>{done=true}),/Local save failed/);
 assert.equal(writes,1);assert.equal(done,false);assert.equal(errors.length,1);assert.equal(persisted,original);
 assert.deepEqual(JSON.parse(save.stringify()).levelStars,before.levelStars);
});
for(const invalid of ['{broken-json','future-version'])test('invalid save keeps the original reset behavior: '+invalid,()=>{
 let persisted=invalid,writes=0,done=false;
 const {z,Od}=game({readSave(){return persisted},writeSave(key,value){writes++;persisted=value;return true},reportError(){}});
 if(invalid==='future-version')persisted=JSON.stringify({...fixture(z),v:99});
 new z(new Od('CTR')).load(()=>{done=true});
 assert.equal(done,true);assert.equal(writes,1);assert.equal(JSON.parse(persisted).levelStars[0][0],0);assert.equal(JSON.parse(persisted).v,3);
});
test('only the original English font pair ships and covers every English string',()=>{
 assert.deepEqual(fs.readdirSync(path.join(folder,'res/lang')).sort(),['font-en.dat','font-en.png']);
 const {Ld,Ne,p}=game(),buffer=fs.readFileSync(path.join(folder,'res/lang/font-en.dat'));
 const pack=new Ld(buffer.buffer.slice(buffer.byteOffset,buffer.byteOffset+buffer.byteLength));
 assert.equal(pack.dr.length,11);
 const strings=JSON.parse(fs.readFileSync(path.join(folder,'res/strings.json'),'utf8'));
 for(const index of [p.tw('en','en'),p.tw('en','en')+1]){
  const ids=new Set(new Ne().mn(pack.dr[index].data).xh.map(glyph=>glyph.id));
  for(const [key,translations]of Object.entries(strings)){
   for(const character of translations.en)if(character.charCodeAt(0)>=32)assert.ok(ids.has(character.charCodeAt(0)),key+': '+character);
  }
 }
});
