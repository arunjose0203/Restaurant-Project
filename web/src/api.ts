import {HubConnectionBuilder} from '@microsoft/signalr';
import {initialState,mutate} from './demo.mjs';
import type {State,User} from './types';
export const apiUrl=(import.meta as any).env.VITE_API_URL?.replace(/\/$/,'')||'';
export const demo=!apiUrl;
let token='';
const key='tableflow-demo-v1';
export function demoState():State{try{return JSON.parse(localStorage.getItem(key)||'null')||initialState();}catch{return initialState() as State;}}
export async function request(path:string,method='GET',body?:unknown):Promise<any>{const r=await fetch(`${apiUrl}/api${path}`,{method,headers:{'Content-Type':'application/json',Authorization:`Bearer ${token}`},body:body?JSON.stringify(body):undefined});if(!r.ok){if(r.status===401&&path!=='/auth/login')window.dispatchEvent(new Event('session-expired'));const e=await r.json().catch(()=>({}));throw Error(e.message||(r.status===401?'Your session expired. Sign in again.':`Request failed (${r.status})`));}return r.status===204?null:r.json();}
export async function login(email:string,password:string){const r=await request('/auth/login','POST',{email,password});token=r.token;return r.user as User;}
export function logout(){token='';}
export async function getState(){return demo?demoState():request('/state');}
export async function action(path:string,body:unknown,user:User,method='POST'){if(!demo)return request(path,method,body);const change=()=>{const s=mutate(demoState(),path,body,user);localStorage.setItem(key,JSON.stringify(s));window.dispatchEvent(new Event('demo-change'));};if(navigator.locks)await navigator.locks.request('tableflow-demo',change);else change();}
export function subscribe(refresh:()=>void,notify:(s:string)=>void,connection:(s:string)=>void){if(demo){const cb=()=>refresh();window.addEventListener('storage',cb);window.addEventListener('demo-change',cb);connection('Demo workspace');return()=>{window.removeEventListener('storage',cb);window.removeEventListener('demo-change',cb);};}let stopped=false;let retry:ReturnType<typeof setTimeout>;const hub=new HubConnectionBuilder().withUrl(`${apiUrl}/hubs/orders`,{accessTokenFactory:()=>token}).withAutomaticReconnect().build();hub.on('StateChanged',refresh);hub.on('FoodReady',n=>{notify(n.message);refresh();});hub.onreconnecting(()=>connection('Reconnecting…'));hub.onreconnected(()=>{connection('Live updates');refresh();});const start=()=>hub.start().then(()=>{connection('Live updates');refresh();}).catch(()=>{connection('Connection interrupted');if(!stopped)retry=setTimeout(start,5000);});hub.onclose(()=>{if(!stopped)retry=setTimeout(start,5000);});void start();const fallback=setInterval(refresh,30000);return()=>{stopped=true;clearTimeout(retry);clearInterval(fallback);void hub.stop();};}
