import type {useWorkspace} from '../hooks/useWorkspace';
import {OrderComposer} from './OrderComposer';
import {PosBilling} from './PosBilling';
import {Button,s,money,sum} from '../ui';
import {View,Text,ScrollView,TextInput,Pressable,Alert} from 'react-native';
import type {Order} from '../../web/src/types';
export function CashierScreen({workspace,card}:{workspace:ReturnType<typeof useWorkspace>;card:(o:Order)=>React.ReactNode}){const {localKey,token,setToken,setUser,setData,role,setRole,tab,setTab,email,setEmail,password,setPassword,busy,setBusy,error,setError,live,setLive,table,setTable,cart,setCart,instructions,setInstructions,query,setQuery,category,setCategory,paymentMethod,setPaymentMethod,reference,setReference,adminSection,setAdminSection,edit,setEdit,lastSync,setLastSync,syncError,setSyncError,syncing,setSyncing,pending,setPending,draftReady,setDraftReady,draftError,setDraftError,posting,currentAuth,draftRef,writes,api,refresh,run,signIn,field,API,sessionKey,onChangeServer}=workspace;const data=workspace.data!;const user=workspace.user!;const own=data.orders.filter(o=>role!=='Waiter'||o.waiterId===user.id);return <PosBilling data={data} api={api} storageScope={localKey(user.id,"pos")} refresh={refresh}/>;}
