import {useEffect,useState} from 'react';
import {readDraft,writeDraft} from '../../api';
import type {State} from '../../types';
import {MenuPicker,type Line} from '../pos/MenuPicker';
export function AdvancedOrderEntry({state,userId,busy,onSubmit}:{state:State;userId:string;busy:boolean;onSubmit:(body:any)=>Promise<boolean>}){
 const [saved]=useState(()=>readDraft(userId));const [table,setTable]=useState(saved?.table||state.tables[0]?.id||1);const [lines,setLines]=useState<Line[]>(saved?.lines||[]);const [notes,setNotes]=useState(saved?.notes||'');const [error,setError]=useState('');
 useEffect(()=>{try{writeDraft(userId,{table,lines,notes});setError('');}catch{setError('Draft storage unavailable. Keep this page open.');}},[userId,table,lines,notes]);
 return <section><label className="field-label">Table<select value={table} onChange={e=>setTable(Number(e.target.value))}>{state.tables.filter(t=>t.active).map(t=><option key={t.id} value={t.id}>{t.section||'Main Hall'} · {t.name}</option>)}</select></label><MenuPicker menu={state.menu} lines={lines} setLines={setLines}/><label className="field-label">Allergies and instructions<textarea value={notes} maxLength={500} onChange={e=>setNotes(e.target.value)}/></label>{error&&<p role="alert">{error}</p>}<button className="primary" disabled={busy||!lines.length} onClick={async()=>{if(await onSubmit({tableId:table,items:lines,instructions:notes})){setLines([]);setNotes('');}}}>Send to kitchen</button></section>;
}
