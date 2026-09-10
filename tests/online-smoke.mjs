// Explicit opt-in: creates real records in a dedicated test PostgreSQL database.
// TABLEFLOW_TEST_API, TABLEFLOW_TEST_EMAIL, TABLEFLOW_TEST_PASSWORD,
// TABLEFLOW_TEST_ALLOW_WRITES=yes node tests/online-smoke.mjs
import assert from 'node:assert/strict';
import {randomUUID} from 'node:crypto';
import signalr from '../web/node_modules/@microsoft/signalr/dist/cjs/index.js';
const {HubConnectionBuilder}=signalr;
const base=process.env.TABLEFLOW_TEST_API;
if(!base||process.env.TABLEFLOW_TEST_ALLOW_WRITES!=='yes')throw Error('Set test API, test admin credentials, and TABLEFLOW_TEST_ALLOW_WRITES=yes. Use a dedicated database.');
async function req(path,method='GET',body,token,expected=200){const r=await fetch(`${base}/api${path}`,{method,headers:{'Content-Type':'application/json',...(token?{Authorization:`Bearer ${token}`}:{})},body:body?JSON.stringify(body):undefined});const data=await r.json().catch(()=>null);assert.equal(r.status,expected,`${method} ${path}: ${JSON.stringify(data)}`);return data;}
const admin=await req('/auth/login','POST',{email:process.env.TABLEFLOW_TEST_EMAIL,password:process.env.TABLEFLOW_TEST_PASSWORD});
const nonce=randomUUID().slice(0,8);const password=`Test-${randomUUID()}`;
const accounts={};for(const role of ['Waiter','Kitchen','Cashier']){const email=`smoke-${role}-${nonce}@example.test`;await req('/admin/users/00000000-0000-0000-0000-000000000000','PUT',{name:`Smoke ${role}`,email,password,role,active:true},admin.token);accounts[role]=await req('/auth/login','POST',{email,password});}
const table=await req('/admin/tables/0','PUT',{name:`Smoke-${nonce}`,seats:4,active:true},admin.token);const state=await req('/state','GET',undefined,admin.token);const menu=state.menu.find(m=>m.active);const method=state.paymentMethods.find(m=>m.active);
await req('/admin/tables/0','PUT',{name:'Forbidden',seats:4,active:true},accounts.Waiter.token,403);
await req('/state','GET',undefined,undefined,401);
const hub=new HubConnectionBuilder().withUrl(`${base}/hubs/orders`,{accessTokenFactory:()=>accounts.Waiter.token}).build();let readyCount=0;hub.on('FoodReady',()=>readyCount++);await hub.start();
try{
const order=await req('/orders','POST',{tableId:table.id,items:[{menuItemId:menu.id,quantity:2}],instructions:'Integration test'},accounts.Waiter.token,201);
const round=await req('/orders','POST',{tableId:table.id,items:[{menuItemId:menu.id,quantity:1}]},accounts.Waiter.token,201);assert.equal(order.sessionId,round.sessionId);
await req(`/orders/${order.id}/status`,'PATCH',{status:'Served'},accounts.Waiter.token,409);
await req(`/sessions/${order.sessionId}/pay`,'POST',{paymentMethodId:method.id,expectedTotal:menu.price*3},accounts.Cashier.token,409);
for(const o of [order,round]){await req(`/orders/${o.id}/status`,'PATCH',{status:'Preparing'},accounts.Kitchen.token);await req(`/orders/${o.id}/status`,'PATCH',{status:'Ready'},accounts.Kitchen.token);await req(`/orders/${o.id}/status`,'PATCH',{status:'Served'},accounts.Waiter.token);}
const bill=await req(`/sessions/${order.sessionId}/bill`,'GET',undefined,accounts.Cashier.token);assert.equal(bill.canPay,true);assert.equal(bill.orders.length,2);assert.equal(bill.total,menu.price*3);
await req(`/sessions/${order.sessionId}/pay`,'POST',{paymentMethodId:method.id,expectedTotal:0},accounts.Cashier.token,409);
const payments=await Promise.all([1,2].map(()=>req(`/sessions/${order.sessionId}/pay`,'POST',{paymentMethodId:method.id,expectedTotal:bill.total,reference:'smoke'},accounts.Cashier.token)));assert.equal(payments[0].id,payments[1].id);
const after=await req('/state','GET',undefined,admin.token);assert.ok(!after.sessions.some(s=>s.id===order.sessionId));assert.equal(after.orders.find(o=>o.id===order.id).status,'Paid');
const waiterState=await req('/state','GET',undefined,accounts.Waiter.token);assert.equal(waiterState.notifications.filter(n=>n.orderId===order.id||n.orderId===round.id).length,2);
await new Promise(r=>setTimeout(r,500));assert.equal(readyCount,2);
console.log('PASS: roles, order rounds, transitions, SignalR, persisted notifications, bill totals, concurrent idempotent payment and table closure.');
}finally{await hub.stop();}
