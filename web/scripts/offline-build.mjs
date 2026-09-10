import {readFileSync,readdirSync,writeFileSync} from 'node:fs';
import {createHash} from 'node:crypto';
const files=['/index.html',...readdirSync('dist/assets').filter(n=>/\.(js|css|woff2)$/.test(n)).map(n=>'/assets/'+n)];
const version=createHash('sha256').update(files.map(p=>readFileSync('dist'+p)).join('')).digest('hex').slice(0,16);
const worker=`const CACHE='tableflow-shell-${version}';
const FILES=${JSON.stringify(files)};
self.addEventListener('install',event=>event.waitUntil((async()=>{const cache=await caches.open(CACHE);await Promise.all(FILES.map(async path=>{const response=await fetch(path,{cache:'reload'});if(!response.ok||response.redirected)throw Error('Shell unavailable');if(path==='/index.html'&&!(await response.clone().text()).includes('id="root"'))throw Error('Not the application shell');await cache.put(path,response);}));})()));
self.addEventListener('activate',event=>event.waitUntil((async()=>{for(const key of await caches.keys())if(key.startsWith('tableflow-shell-')&&key!==CACHE)await caches.delete(key);await self.clients.claim();})()));
self.addEventListener('fetch',event=>{const request=event.request;const url=new URL(request.url);if(request.method!=='GET'||url.origin!==self.location.origin||url.pathname.startsWith('/api/')||url.pathname.startsWith('/hubs/'))return;
if(request.mode==='navigate'&&(url.pathname==='/'||url.pathname==='/index.html')){event.respondWith((async()=>{const cached=await caches.match('/index.html',{cacheName:CACHE});return cached||fetch(request);})());return;}
if(FILES.includes(url.pathname)){event.respondWith((async()=>{const cached=await caches.match(url.pathname,{cacheName:CACHE});return cached||fetch(request);})());}});
`;
writeFileSync('dist/sw.js',worker);
