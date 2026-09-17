import {useState} from 'react';
import {Trash2} from 'lucide-react';
import {Dialog} from '../../components/Dialog';
import type {Order} from '../../types';
import type {Runner} from '../../lib/actions';
export function VoidOrders({orders,busy,run}:{orders:Order[];busy:boolean;run:Runner}){
 const [selected,setSelected]=useState<Order|null>(null);const [reason,setReason]=useState('');
 return <section className="pos-panel"><h2>Order adjustments</h2><p className="section-description">Cancel an order with a recorded reason. Paid orders cannot be changed here.</p>{orders.filter(o=>!['Paid','Voided'].includes(o.status)).map(o=><div className="payment-row" key={o.id}><div><strong>Table {o.tableId}</strong><small>Order #{o.id.slice(0,8)} · {o.status}</small></div><button className="text-danger" onClick={()=>{setSelected(o);setReason('');}}><Trash2 size={16}/>Cancel order</button></div>)}{selected&&<Dialog title="Cancel this order?" description={`Table ${selected.tableId} · Order #${selected.id.slice(0,8)}. This will be recorded in the activity log.`} onClose={()=>setSelected(null)}><label className="field-label">Reason<select value={reason} onChange={e=>setReason(e.target.value)}><option value="">Choose a reason</option>{['Kitchen Error','Customer Changed Mind','Quality Issue','Spillage'].map(r=><option key={r}>{r}</option>)}</select></label><div className="dialog-actions"><button className="secondary" onClick={()=>setSelected(null)}>Keep order</button><button className="danger-button" disabled={busy||!reason} onClick={async()=>{if(await run('/pos/admin/orders/'+selected.id+'/void',{reason},'Order cancelled'))setSelected(null);}}>Confirm cancellation</button></div></Dialog>}</section>;
}
