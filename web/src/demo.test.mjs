import test from 'node:test';
import assert from 'node:assert/strict';
import {initialState,mutate} from './demo.mjs';
test('complete service flow combines rounds, preserves prices and closes once',()=>{
 const s=initialState();const [waiter,kitchen,cashier]=s.users;
 mutate(s,'/orders',{tableId:1,items:[{menuItemId:1,quantity:2}],instructions:'No chilli'},waiter);
 const first=s.orders[0];const session=first.sessionId;const price=first.items[0].unitPrice;s.menu[0].price=999;
 mutate(s,'/orders',{tableId:1,items:[{menuItemId:6,quantity:1}]},waiter);const second=s.orders[0];assert.equal(second.sessionId,session);assert.equal(first.items[0].unitPrice,price);
 assert.throws(()=>mutate(s,`/sessions/${session}/pay`,{paymentMethodId:1,expectedTotal:630},cashier),/served/);
 for(const o of [first,second]){mutate(s,`/orders/${o.id}/status`,{status:'Preparing'},kitchen);mutate(s,`/orders/${o.id}/status`,{status:'Ready'},kitchen);mutate(s,`/orders/${o.id}/status`,{status:'Served'},waiter);}
 assert.equal(s.notifications.filter(n=>n.userId===waiter.id).length,2);
 assert.throws(()=>mutate(s,`/sessions/${session}/pay`,{paymentMethodId:1,expectedTotal:1},cashier),/changed/);
 mutate(s,`/sessions/${session}/pay`,{paymentMethodId:1,expectedTotal:630},cashier);
 mutate(s,`/sessions/${session}/pay`,{paymentMethodId:1,expectedTotal:630},cashier);
 assert.equal(s.bills.filter(b=>b.sessionId===session).length,1);assert.ok(s.sessions.find(x=>x.id===session).closedAt);assert.equal(first.status,'Paid');
 mutate(s,'/orders',{tableId:1,items:[{menuItemId:1,quantity:1}]},waiter);assert.notEqual(s.orders[0].sessionId,session);
});
test('roles and transitions reject unauthorized or skipped operations',()=>{
 const s=initialState();const [waiter,kitchen,cashier]=s.users;const o=s.orders.find(o=>o.status==='Preparing');
 assert.throws(()=>mutate(s,`/orders/${o.id}/status`,{status:'Served'},waiter),/transition/);
 assert.throws(()=>mutate(s,`/orders/${o.id}/status`,{status:'Ready'},cashier),/permitted/);
 assert.throws(()=>mutate(s,'/orders',{tableId:1,items:[{menuItemId:1,quantity:1}]},kitchen),/waiters/);
 const ready=s.orders.find(o=>o.status==='Ready');assert.throws(()=>mutate(s,`/orders/${ready.id}/status`,{status:'Served'},{...waiter,id:'other'}),/permitted/);
});
