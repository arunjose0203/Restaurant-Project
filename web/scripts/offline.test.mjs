import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import vm from 'node:vm';
test('installed app shell works offline and never caches API or hub traffic',async()=>{
 const listeners={},stored=new Map();let offline=false;
 const cache={put:async(key,value)=>stored.set(key,value),match:async key=>stored.get(key)};
 const context={self:{location:{origin:'https://restaurant.test'},clients:{claim:async()=>{}},addEventListener:(name,fn)=>listeners[name]=fn},caches:{open:async()=>cache,keys:async()=>[],delete:async()=>true,match:async key=>stored.get(key)},URL,fetch:async path=>{if(offline)throw Error('offline');return {ok:true,redirected:false,clone(){return this;},text:async()=>path==='/index.html'?'<div id="root"></div>':'asset',path};}};
 vm.runInNewContext(readFileSync('dist/sw.js','utf8'),context);
 let installed;listeners.install({waitUntil:p=>installed=p});await installed;offline=true;
 let response;listeners.fetch({request:{method:'GET',url:'https://restaurant.test/',mode:'navigate'},respondWith:p=>response=p});assert.equal((await response).path,'/index.html');
 for(const path of ['/api/state','/hubs/orders','/api/sessions/test/pay']){let intercepted=false;listeners.fetch({request:{method:'GET',url:'https://restaurant.test'+path},respondWith:()=>intercepted=true});assert.equal(intercepted,false);}
});
