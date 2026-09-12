import { Bell,Plus,ShoppingBag } from 'lucide-react';
import { Orders } from '../../components/Orders';
import type { Runner } from '../../lib/actions';
import type { Order,State,User } from '../../types';
import { OrderEntry as LegacyOrderEntry } from './OrderEntry';
import {AdvancedOrderEntry} from './AdvancedOrderEntry';
import {demo} from '../../api';
const OrderEntry=demo?LegacyOrderEntry:AdvancedOrderEntry;

export function WaiterScreen({state,user,tab,setTab,active,ready,busy,run}:{state:State;user:User;tab:string;setTab:(tab:string)=>void;active:Order[];ready:Order[];busy:boolean;run:Runner}){return <><div className="tabs"><button className={tab==='new'?'active':''} onClick={()=>setTab('new')}><Plus size={17}/>Take an order</button><button className={tab==='orders'?'active':''} onClick={()=>setTab('orders')}><ShoppingBag size={17}/>My orders <span>{active.length}</span></button><button className={tab==='ready'?'active':''} onClick={()=>setTab('ready')}><Bell size={17}/>Food ready <span>{ready.length}</span></button></div>{tab==='new'?<OrderEntry key={user.id} userId={user.id} state={state} busy={busy} onSubmit={(r)=>run('/orders',r,'Order sent to the kitchen')}/>:<Orders orders={tab==='ready'?ready:active} state={state} role="Waiter" busy={busy} run={run}/>}</>;}

