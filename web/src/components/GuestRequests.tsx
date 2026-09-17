import {BellRing,Check,CheckCircle2} from 'lucide-react';
import type {Runner} from '../lib/actions';
export function GuestRequests({requests,busy,run}:{requests:any[];busy:boolean;run:Runner}){
 return <section className="requests-panel"><div className="section-heading"><h2><BellRing size={18}/> Guest requests</h2><span className="count-pill">{requests.length}</span></div>{requests.length?requests.map(r=><div className="request-card" key={r.id}><div className="request-icon"><BellRing size={18}/></div><div><strong>Table {r.tableId}</strong><small>{r.kind==='Bill'?'Ready for the bill':'Would like some help'}</small></div><button className="secondary" disabled={busy} onClick={()=>run('/pos/guest-requests/'+r.id+'/resolve',{},'Request completed')}><Check size={16}/>Done</button></div>):<div className="quiet-state"><CheckCircle2 size={22}/><div><strong>All taken care of</strong><p>New requests from your guests will appear here.</p></div></div>}</section>;
}
