import {useState} from 'react';
import {View,Text,Pressable} from 'react-native';
import type {useWorkspace} from '../hooks/useWorkspace';
import type {Order} from '../../web/src/types';
import {s} from '../ui';
export function KitchenScreen({workspace,card}:{workspace:ReturnType<typeof useWorkspace>;card:(o:Order)=>React.ReactNode}){
 const [status,setStatus]=useState('New');
 const orders=workspace.data?.orders||[];
 const visible=orders.filter(o=>o.status===status);
 return <View style={{gap:16}}>
  <View style={s.sectionTabs}>{['New','Preparing','Ready'].map(value=><Pressable key={value} accessibilityRole="tab" accessibilityState={{selected:status===value}} onPress={()=>setStatus(value)} style={[s.sectionTab,status===value&&s.sectionTabActive]}><Text style={[s.sectionTabText,status===value&&s.sectionTabTextActive]}>{value} · {orders.filter(o=>o.status===value).length}</Text></Pressable>)}</View>
  <Text style={s.meta}>{status==='New'?'Start the next ticket when your station is ready.':status==='Preparing'?'Mark dishes ready when they can leave the kitchen.':'These orders are ready for the service team.'}</Text>
  {visible.length?visible.map(card):<View style={[s.card,s.empty]}><Text style={s.h2}>{status==='New'?'All caught up':`No ${status.toLowerCase()} orders`}</Text><Text style={s.meta}>New updates will appear here automatically.</Text></View>}
 </View>;
}
