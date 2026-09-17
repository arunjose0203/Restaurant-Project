import type {useWorkspace} from '../hooks/useWorkspace';
import {OrderComposer} from './OrderComposer';
import {s} from '../ui';
import {View,Text,Pressable} from 'react-native';
import type {Order} from '../../web/src/types';
export function WaiterScreen({workspace,card}:{workspace:ReturnType<typeof useWorkspace>;card:(o:Order)=>React.ReactNode}){
 const {data,user,role,tab,setTab,localKey,busy,run}=workspace;const own=data!.orders.filter(o=>role!=='Waiter'||o.waiterId===user!.id);const orders=own.filter(o=>tab==='Food ready'?o.status==='Ready':!['Paid','Voided'].includes(o.status));
 return <><View style={s.sectionTabs}>{[['Order','New order'],['My orders','My orders'],['Food ready','Ready']].map(([id,label])=><Pressable key={id} accessibilityRole="tab" accessibilityState={{selected:tab===id}} onPress={()=>setTab(id)} style={[s.sectionTab,tab===id&&s.sectionTabActive]}><Text style={[s.sectionTabText,tab===id&&s.sectionTabTextActive]}>{label}{id==='Food ready'?` (${own.filter(o=>o.status==='Ready').length})`:''}</Text></Pressable>)}</View>{tab==='Order'?<OrderComposer data={data!} storageScope={localKey(user!.id,'pos')} busy={busy} onSubmit={body=>run('/orders',body)}/>:orders.length?orders.map(card):<View style={[s.card,s.empty]}><Text style={s.emptyIcon}>✓</Text><Text style={s.h2}>{tab==='Food ready'?'Nothing on the pass yet':'You’re all caught up'}</Text><Text style={[s.body,{textAlign:'center',fontSize:13}]}>{tab==='Food ready'?'Orders appear here as soon as the kitchen marks them ready.':'New orders you send will appear here.'}</Text></View>}</>;
}
