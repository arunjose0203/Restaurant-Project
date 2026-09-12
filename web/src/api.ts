import {HubConnectionBuilder,HubConnectionState} from '@microsoft/signalr';
import {initialState,mutate} from './demo.mjs';
import {jsonRequest,RequestError,sendOrder,orderSignature} from '../../shared/network.mjs';
import type {State,User} from './types';
export const apiUrl=(import.meta as any).env.VITE_API_URL?.replace(/\/$/,'')||'';
export const demo=!apiUrl;
let token='';let currentUser:User|null=null;
const scope=`tableflow:${apiUrl||'demo'}:`;
export const userKey=(id:string,kind:string)=>`${scope}${currentUser?.branchId||1}:${id}:${kind}`;
function read(storage:Storage,key:string){try{return JSON.parse(storage.getItem(key)||'null');}catch{return null;}}
export function restoreSession():User|null{const s=read(sessionStorage,scope+'session');if(s&&Date.parse(s.expires)>Date.now()){token=s.token;currentUser=s.user;return s.user;}return null;}
export function cachedState(user:User|null):State|null{if(!user)return null;const c=read(sessionStorage,userKey(user.id,'state'));return c&&Date.now()-c.at<86400000?c.value:null;}
export function readDraft(userId:string){return read(localStorage,userKey(userId,'draft'));}
export function writeDraft(userId:string,draft:unknown){localStorage.setItem(userKey(userId,'draft'),JSON.stringify(draft));}
export function getPending(userId:string){return read(localStorage,userKey(userId,'pending'));}
export const draftBody=(d:any)=>({tableId:d.table,items:d.lines||Object.entries(d.cart||{}).filter(([,q])=>Number(q)>0).map(([id,q])=>({menuItemId:Number(id),quantity:Number(q)})),instructions:d.notes||''});
const emit=(name:string,detail?:unknown)=>window.dispatchEvent(new CustomEvent(name,{detail}));
export async function request(path:string,method='GET',body?:unknown):Promise<any>{
 if(!navigator.onLine)throw new RequestError('You are offline. Keep your draft and reconnect before sending.');
 try{return await jsonRequest(`${apiUrl}/api${path}`,{method,body,token});}
 catch(e){if(e instanceof RequestError&&e.status===401&&!['/auth/login','/auth/pin-login'].includes(path))emit('session-expired');if(!(e instanceof RequestError)||!e.status)emit('connection-lost');throw e;}
}
export function demoState():State{try{return JSON.parse(localStorage.getItem('tableflow-demo-v1')||'null')||initialState();}catch{return initialState() as State;}}
export async function login(email:string,password:string){const r=await request('/auth/login','POST',{email,password});token=r.token;currentUser=r.user;try{sessionStorage.setItem(scope+'session',JSON.stringify(r));}catch{/* Current-tab session still works in memory. */}return r.user as User;}
export function logout(){if(currentUser)sessionStorage.removeItem(userKey(currentUser.id,'state'));token='';currentUser=null;sessionStorage.removeItem(scope+'session');}
export async function getState(){if(demo)return demoState();const owner=currentUser;const value=await request('/state');if(owner!==currentUser)throw new RequestError('Session changed.');try{if(owner)sessionStorage.setItem(userKey(owner.id,'state'),JSON.stringify({at:Date.now(),value}));}catch{/* Keep the current in-memory screen. */}return value;}
export async function action(path:string,body:any,user:User,method='POST'){
 const send=async(payload:any)=>{if(!demo)return request(path,method,payload);const s=mutate(demoState(),path,payload,user);localStorage.setItem('tableflow-demo-v1',JSON.stringify(s));emit('demo-change');return s;};
 const execute=async()=>{
  if(path!=='/orders')return send(body);
  const result=await sendOrder({body,uuid:()=>crypto.randomUUID(),read:async()=>getPending(user.id),write:async p=>{localStorage.setItem(userKey(user.id,'pending'),JSON.stringify(p));emit('pending-change');},remove:async confirmed=>{if(confirmed){const d=readDraft(user.id);if(d&&orderSignature(draftBody(d))===orderSignature(body))localStorage.removeItem(userKey(user.id,'draft'));}localStorage.removeItem(userKey(user.id,'pending'));emit('pending-change');},send});
  const draft=readDraft(user.id);if(draft&&orderSignature(draftBody(draft))===orderSignature(body))localStorage.removeItem(userKey(user.id,'draft'));
  emit('order-confirmed',{signature:orderSignature(body),userId:user.id});return result;
 };
 return navigator.locks?navigator.locks.request(userKey(user.id,'write'),execute):execute();
}
export function subscribe(refresh:()=>Promise<void>,notify:(s:string)=>void,connection:(s:string)=>void){
 if(demo){const cb=()=>{void refresh();};window.addEventListener('storage',cb);window.addEventListener('demo-change',cb);connection('Demo workspace');return()=>{window.removeEventListener('storage',cb);window.removeEventListener('demo-change',cb);};}
 let stopped=false;let retry:ReturnType<typeof setTimeout>;let attempt=0;let debounce:ReturnType<typeof setTimeout>;
 const hub=new HubConnectionBuilder().withUrl(`${apiUrl}/hubs/orders`,{accessTokenFactory:()=>token}).withAutomaticReconnect([0,2000,5000,15000,30000]).build();
 const sync=()=>{clearTimeout(debounce);debounce=setTimeout(()=>{if(!stopped&&navigator.onLine)void refresh();},350);};
 const start=async()=>{if(stopped||!navigator.onLine||hub.state!==HubConnectionState.Disconnected)return;try{await hub.start();attempt=0;connection('Live updates');sync();}catch{connection('Reconnecting…');retry=setTimeout(start,Math.min(30000,2000*2**Math.min(attempt++,4)));}};
 hub.on('StateChanged',sync);hub.on('FoodReady',n=>{notify(n.message);sync();});hub.onreconnecting(()=>connection('Reconnecting…'));hub.onreconnected(()=>{connection('Live updates');sync();});hub.onclose(()=>{if(!stopped)retry=setTimeout(start,5000);});
 const offline=()=>connection('Offline · reconnecting');const online=()=>{connection('Checking connection…');sync();void start();};const visible=()=>{if(!document.hidden)online();};
 window.addEventListener('offline',offline);window.addEventListener('online',online);window.addEventListener('connection-lost',offline);document.addEventListener('visibilitychange',visible);
 void start();const fallback=setInterval(()=>{if(!document.hidden&&navigator.onLine)sync();},90000);
 return()=>{stopped=true;clearTimeout(retry);clearTimeout(debounce);clearInterval(fallback);window.removeEventListener('offline',offline);window.removeEventListener('online',online);window.removeEventListener('connection-lost',offline);document.removeEventListener('visibilitychange',visible);void hub.stop();};
}
export async function acceptSession(session:any){token=session.token;currentUser=session.user;sessionStorage.setItem(scope+'session',JSON.stringify(session));window.location.reload();}
export async function download(path:string){const response=await fetch(apiUrl+'/api'+path,{headers:{Authorization:'Bearer '+token},signal:AbortSignal.timeout(15000)});if(!response.ok)throw new Error('Download failed ('+response.status+').');return response.blob();}
