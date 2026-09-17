import {Menu,Map,SlidersHorizontal,RefreshCw,ArrowUpRight,PanelTop,UserRound,ChevronRight} from 'lucide-react';
import {GuestRequests} from './components/GuestRequests';
import {VoidOrders} from './features/pos/VoidOrders';
import {PosBilling} from './features/cashier/PosBilling';
import {PosAdmin} from './features/pos/PosAdmin';
import {FloorMap} from './features/pos/FloorMap';
import {AccountTools} from './features/pos/AccountTools';
import { ArrowRight,Bell,Check,CheckCircle2,ChefHat,Clock,LayoutGrid,LogOut,Receipt,Settings,ShoppingBag,UtensilsCrossed,Wallet,X } from 'lucide-react';
import { useEffect,useMemo,useRef,useState } from 'react';
import { coalescedRefresh } from '../../shared/network.mjs';
import { action,cachedState,demo,demoState,getPending,getState,logout,restoreSession,subscribe } from './api';
import { Empty } from './components/Empty';
import { Stat } from './components/Stat';
import { Admin } from './features/admin/Admin';
import { Login } from './features/auth/Login';
import { Billing } from './features/cashier/Billing';
import { KitchenScreen } from './features/kitchen/KitchenScreen';
import { WaiterScreen } from './features/waiter/WaiterScreen';
import { money } from './lib/format';
import type { Role,State,User } from './types';

export const restoredUser=demo?demoState().users[0]:restoreSession();

export const roles:{role:Role;name:string;icon:typeof LayoutGrid}[]=[{role:'Waiter',name:'Orders',icon:LayoutGrid},{role:'Kitchen',name:'Kitchen',icon:ChefHat},{role:'Cashier',name:'Payments',icon:Receipt},{role:'Admin',name:'Management',icon:Settings}];

export function App(){
 const [page,setPage]=useState<'workspace'|'floor'|'settings'|'account'>('workspace');const [mobileMenu,setMobileMenu]=useState(false);
 const [user,setUser]=useState<User|null>(restoredUser);const [role,setRole]=useState<Role>(restoredUser?.role||'Waiter');const [state,setState]=useState<State|null>(demo?demoState():cachedState(restoredUser));const [tab,setTab]=useState('new');const [toast,setToast]=useState('');const [error,setError]=useState('');const [connection,setConnection]=useState('Connecting…');const [busy,setBusy]=useState(false);const [showNotifications,setShowNotifications]=useState(false);
 const [syncError,setSyncError]=useState('');const [lastSync,setLastSync]=useState(0);const [syncing,setSyncing]=useState(false);const [pending,setPending]=useState<any>(user?getPending(user.id):null);const submitting=useRef(false);const currentUserId=useRef(user?.id);currentUserId.current=user?.id;
 const refresh=useMemo(()=>coalescedRefresh(async()=>{const id=user?.id;setSyncing(true);try{const result=await getState();if(id===currentUserId.current){setState(result);setSyncError('');setLastSync(Date.now());if(!demo)setConnection('Connected');}}catch(e){if(id===currentUserId.current)setSyncError((e as Error).message);}finally{if(id===currentUserId.current)setSyncing(false);}}),[user?.id]);
 useEffect(()=>{const changed=()=>setPending(user?getPending(user.id):null);changed();window.addEventListener('pending-change',changed);window.addEventListener('storage',changed);return()=>{window.removeEventListener('pending-change',changed);window.removeEventListener('storage',changed);};},[user?.id]);
 useEffect(()=>{if(!user)return;void refresh();return subscribe(refresh,setToast,setConnection);},[user?.id]);
 useEffect(()=>{if(toast){const t=setTimeout(()=>setToast(''),5000);return()=>clearTimeout(t);}},[toast]);
 useEffect(()=>{const expired=()=>{logout();setUser(null);setState(null);};window.addEventListener('session-expired',expired);return()=>window.removeEventListener('session-expired',expired);},[]);
 useEffect(()=>{const t=setInterval(()=>setState(s=>s?{...s}:s),60000);return()=>clearInterval(t);},[]);
 async function run(path:string,body:unknown,message:string,method='POST'){if(submitting.current||!user)return false;submitting.current=true;setBusy(true);setError('');try{await action(path,body,user,method);void refresh();setToast(message);return true;}catch(e){setError((e as Error).message);return false;}finally{submitting.current=false;setBusy(false);}}
 function switchRole(r:Role){setRole(r);setTab('new');setPage('workspace');setMobileMenu(false);if(demo)setUser(state!.users.find(u=>u.role===r)!);}
 if(!user)return <Login onLogin={u=>{setUser(u);setRole(u.role);}}/>;
 if(!state)return <div className="loading"><UtensilsCrossed/><h2>Opening your workspace…</h2>{(error||syncError)&&<><p>{error||syncError}</p><button onClick={()=>void refresh()}>Retry connection</button><button onClick={()=>{logout();setUser(null);}}>Back to sign in</button></>}</div>;
 const own=state.orders.filter(o=>role!=='Waiter'||o.waiterId===user.id);const active=own.filter(o=>!['Paid','Voided'].includes(o.status));const ready=active.filter(o=>o.status==='Ready');const notifications=state.notifications.filter(n=>n.userId===user.id&&!n.read);

 const title=page==='floor'?'A place for every guest.':page==='settings'?'Make it your restaurant.':page==='account'?'Your workspace, your way.':role==='Waiter'?'Good service starts here.':role==='Kitchen'?'A little heat. A lot of care.':role==='Cashier'?'A great meal, all settled.':'The bigger picture.';
 const subtitle=page==='floor'?'See your tables at a glance and keep service moving.':page==='settings'?'Simple controls for your menu, receipts, team, and reports.':page==='account'?'Switch your branch or securely hand over to the next team member.':role==='Waiter'?'Choose a table, find a favourite, and send it to the kitchen.':role==='Kitchen'?'Every ticket in its place. Keep an eye on what needs you next.':role==='Cashier'?'Review the bill, split it if needed, and record the payment.':'Keep your menu, people, and restaurant running smoothly.';
 const sections=roles.filter(r=>demo||user.role==='Admin'||r.role===user.role);
 const navigate=(next:typeof page)=>{setPage(next);setMobileMenu(false);};
 return <div className="app redesigned">
  <a className="skip-link" href="#workspace-main">Skip to workspace</a>
  {mobileMenu&&<button className="navigation-scrim" aria-label="Close navigation" onClick={()=>setMobileMenu(false)}/>}
  <aside className={'sidebar '+(mobileMenu?'is-open':'')}>
   <a className="brand" href="#" onClick={e=>{e.preventDefault();navigate('workspace');}}><span className="brand-icon"><UtensilsCrossed size={22}/></span><span>tableflow<span className="brand-dot">.</span></span></a>
   <div className="restaurant"><span className="restaurant-avatar">T</span><div><strong>{demo?'The Table House':'Restaurant workspace'}</strong><small>{demo?'Demo restaurant':'Branch '+(user.branchId||1)}</small></div><ChevronRight size={15}/></div>
   <div className="nav-label">YOUR WORKSPACE</div>
   <nav aria-label="Main navigation">{sections.map(r=><button key={r.role} aria-current={page==='workspace'&&role===r.role?'page':undefined} className={page==='workspace'&&role===r.role?'nav-item selected':'nav-item'} onClick={()=>switchRole(r.role)}><r.icon size={19}/><span>{r.name}</span>{r.role==='Kitchen'&&<em>{state.orders.filter(o=>['New','Preparing'].includes(o.status)).length}</em>}</button>)}<button className={page==='floor'?'nav-item selected':'nav-item'} aria-current={page==='floor'?'page':undefined} onClick={()=>navigate('floor')}><Map size={19}/><span>Floor plan</span></button></nav>
   <div className="nav-label secondary-nav-label">RESTAURANT</div><nav aria-label="Restaurant navigation">{!demo&&user.role==='Admin'&&<button className={page==='settings'?'nav-item selected':'nav-item'} onClick={()=>navigate('settings')}><SlidersHorizontal size={19}/>Settings & reports</button>}{!demo&&<button className={page==='account'?'nav-item selected':'nav-item'} onClick={()=>navigate('account')}><UserRound size={19}/>Staff & branch</button>}</nav>
   <div className="sidebar-bottom"><div className="service-note"><span className="service-dot"/><strong>Ready for a great service</strong><p>A little more ease.<br/>A little more hospitality.</p></div><div className="profile"><div className="avatar">{user.name.split(' ').map(x=>x[0]).slice(0,2).join('')}</div><div><strong>{user.name}</strong><small>{user.role==='Waiter'?'Floor team':user.role}</small></div>{!demo&&<button className="icon-button" aria-label="Sign out" onClick={()=>{logout();setUser(null);setState(null);}}><LogOut size={18}/></button>}</div></div>
  </aside>
  <div className="workspace"><header className="topbar"><div className="topbar-location"><button className="icon-button mobile-menu-toggle" aria-label="Open navigation" aria-expanded={mobileMenu} onClick={()=>setMobileMenu(true)}><Menu size={22}/></button><span className="breadcrumb">Restaurant</span><ChevronRight size={13}/><strong>{page==='workspace'?roles.find(r=>r.role===role)?.name:page==='floor'?'Floor plan':page==='settings'?'Settings & reports':'Staff & branch'}</strong></div><div className="top-actions"><span className={'connection '+(demo?'demo':'')}><span/>{demo?'Demo mode':connection}</span><span className="date">{new Date().toLocaleDateString('en-IN',{day:'numeric',month:'short',year:'numeric'})}</span><button className="notification-button" aria-label="Notifications" aria-expanded={showNotifications} onClick={()=>setShowNotifications(!showNotifications)}><Bell size={20}/>{notifications.length>0&&<b>{notifications.length}</b>}</button></div></header>
  <main id="workspace-main"><div className="page-heading"><div><div className="eyebrow">{page==='workspace'?'A SMOOTHER SHIFT':page==='floor'?'ON THE FLOOR':'BEHIND THE SCENES'}</div><h1>{title}</h1><p>{subtitle}</p></div><div className="shift"><span className="service-dot"/><div><strong>Today’s service</strong><small>{new Date().toLocaleDateString('en-IN',{weekday:'long'})}</small></div></div></div>
  {demo&&<div className="demo-banner"><span><strong>Take a look around.</strong> Try the complete service flow in this demo.</span><span>Saved on this device <CheckCircle2 size={14}/></span></div>}
  {error&&<div role="alert" className="error">{error}<button className="icon-button" onClick={()=>setError('')} aria-label="Dismiss error"><X size={16}/></button></div>}
  {!demo&&<div className={syncError?'sync-strip stale':'sync-strip'} role="status"><span>{syncError?'Connection needs attention · '+syncError:syncing?'Updating your workspace…':lastSync?'Updated at '+new Date(lastSync).toLocaleTimeString('en-IN',{hour:'2-digit',minute:'2-digit'}):'Showing your saved workspace'}</span><button className="text-button" disabled={syncing} onClick={()=>void refresh()}><RefreshCw size={14} className={syncing?'spin':''}/>Refresh</button></div>}
  {pending&&<div className="pending-order" role="status"><div><strong>Let’s check your last order</strong><p>Table {pending.body.tableId} · Your order is saved. Confirm it before sending another.</p></div><button disabled={busy} className="primary" onClick={()=>run('/orders',pending.body,'Order confirmed with the kitchen')}>Check confirmation</button></div>}
  {(page==='workspace'||page==='floor')&&<div className="stats"><Stat icon={LayoutGrid} label="Tables in service" value={state.sessions.filter(s=>!s.closedAt).length} sub={state.tables.filter(t=>t.active).length+' tables on the floor'}/><Stat icon={ShoppingBag} label="Active orders" value={active.filter(o=>o.status!=='Served').length} sub="Fresh orders, moving along"/><Stat icon={CheckCircle2} label="Ready to serve" value={ready.length} sub="A little happiness, on the pass" accent/><Stat icon={role==='Cashier'||role==='Admin'?Wallet:Check} label={role==='Cashier'||role==='Admin'?'Sales today':'Served today'} value={role==='Cashier'||role==='Admin'?money(state.bills.filter(b=>new Date(b.paidAt).toDateString()===new Date().toDateString()).reduce((s,b)=>s+b.total,0)):own.filter(o=>['Served','Paid'].includes(o.status)&&new Date(o.createdAt).toDateString()===new Date().toDateString()).length} sub="Today’s completed service"/></div>}
  {page==='workspace'&&<>{role==='Waiter'&&<WaiterScreen state={state} user={user} tab={tab} setTab={setTab} active={active} ready={ready} busy={busy} run={run}/>}{role==='Kitchen'&&<KitchenScreen state={state} busy={busy} run={run}/>}{role==='Cashier'&&(demo?<Billing state={state} busy={busy} run={run}/>:<PosBilling state={state} user={user}/>)}{role==='Admin'&&<><Admin state={state} busy={busy} run={run}/>{!demo&&<VoidOrders orders={state.orders} busy={busy} run={run}/>}</>}{!demo&&<GuestRequests requests={(state as any).guestRequests||[]} busy={busy} run={run}/>}</>}
  {page==='floor'&&<FloorMap state={state} user={user} refresh={refresh}/>}
  {page==='settings'&&!demo&&user.role==='Admin'&&<PosAdmin state={state} user={user} refresh={refresh}/>}
  {page==='account'&&!demo&&<AccountTools user={user}/>}
  <footer><UtensilsCrossed size={14}/><strong>tableflow.</strong><span>A little more room for hospitality.</span></footer></main></div>
  {showNotifications&&<><button className="notification-scrim" aria-label="Close notifications" onClick={()=>setShowNotifications(false)}/><section className="notification-panel" aria-label="Notifications"><div className="panel-heading"><div><div className="eyebrow">YOUR UPDATES</div><h2>Ready when you are.</h2></div><button className="icon-button" aria-label="Close notification panel" onClick={()=>setShowNotifications(false)}><X size={20}/></button></div>{notifications.length?notifications.map(n=><div className="notification" key={n.id}><CheckCircle2 size={20}/><div><strong>{n.message}</strong><small>Collect this order from the kitchen.</small></div><button className="icon-button" aria-label="Mark read" onClick={()=>run('/notifications/'+n.id+'/read',{},'Notification cleared')}><Check size={18}/></button></div>):<Empty text="You’re all caught up. New updates will appear here."/>}</section></>}
  {toast&&<div className="toast" role="status"><CheckCircle2 size={20}/>{toast}<button className="icon-button" aria-label="Dismiss notification" onClick={()=>setToast('')}><X size={17}/></button></div>}
 </div>;
}
