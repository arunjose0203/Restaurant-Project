import {usePushNotifications} from './usePushNotifications';
import { SafeAreaView } from 'react-native-safe-area-context';
import React, { useState, useEffect, useRef, useMemo } from 'react';
import { ScrollView, View, Text, TextInput, Pressable, StyleSheet, Alert, ActivityIndicator, AppState, KeyboardAvoidingView, Platform } from 'react-native';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { randomUUID } from 'expo-crypto';
import { jsonRequest, RequestError, sendOrder, orderSignature, coalescedRefresh } from '../../shared/network.mjs';
import * as SecureStore from 'expo-secure-store';
import { StatusBar } from 'expo-status-bar';
import { HubConnectionBuilder } from '@microsoft/signalr';
import type { State, User, Order, Role } from '../../web/src/types';
import {Button,s,money,sum} from '../ui';
const draftBody = (d: any) => ({ tableId: d.table, items: Object.entries(d.cart || {}).filter(([, q]) => Number(q) > 0).map(([id, q]) => ({ menuItemId: Number(id), quantity: Number(q) })), instructions: d.notes || '' });
export function useWorkspace({ API, sessionKey, onChangeServer }: { API: string; sessionKey: string; onChangeServer: () => void }){
const localKey = (id: string, kind: string) => `tableflow:${API}:${user?.branchId||1}:${id}:${kind}`;
const [token, setToken] = useState('');
const [user, setUser] = useState<User | null>(null);
const [data, setData] = useState<State | null>(null);
const [role, setRole] = useState<Role>('Waiter');
const [tab, setTab] = useState('Order');
const [email, setEmail] = useState('');
const [password, setPassword] = useState('');
const [busy, setBusy] = useState(false);
const [error, setError] = useState('');
const [live, setLive] = useState('Connecting');
const [table, setTable] = useState(1);
const [cart, setCart] = useState<Record<number, number>>({});
const [instructions, setInstructions] = useState('');
const [query, setQuery] = useState('');
const [category, setCategory] = useState(0);
const [paymentMethod, setPaymentMethod] = useState(1);
const [reference, setReference] = useState('');
const [adminSection, setAdminSection] = useState('menu');
const [edit, setEdit] = useState<any>(null);
const [lastSync, setLastSync] = useState(0);
const [syncError, setSyncError] = useState('');
const [syncing, setSyncing] = useState(false);
const [pending, setPending] = useState<any>(null);
const [draftReady, setDraftReady] = useState('');
const [draftError, setDraftError] = useState('');
const posting = useRef(false);
const currentAuth = useRef(token);
currentAuth.current = token;
const draftRef = useRef({ table, cart, notes: instructions });
draftRef.current = { table, cart, notes: instructions };
const writes = useRef(Promise.resolve());
async function api(path: string, method = 'GET', body?: unknown, auth = token) { try { return await jsonRequest(API + '/api' + path, { method, body, token: auth }); } catch (e) { if (e instanceof RequestError && e.status === 401 && !['/auth/login','/auth/pin-login'].includes(path) && currentAuth.current === auth) { await SecureStore.deleteItemAsync(sessionKey); setToken(''); setUser(null); setData(null); } throw e; } }
const refresh = useMemo(() => coalescedRefresh(async () => { const auth = token; setSyncing(true); try { const next = await api('/state'); if (currentAuth.current !== auth) return; setData(next); setSyncError(''); setLastSync(Date.now()); setLive('Connected'); if (user) void AsyncStorage.setItem(localKey(user.id, 'state'), JSON.stringify({ at: Date.now(), value: next })).catch(() => { }); } catch (e) { if (currentAuth.current === auth) { setSyncError((e as Error).message); setLive('Connection interrupted'); } } finally { if (currentAuth.current === auth) setSyncing(false); } }), [token, user?.id]);
useEffect(() => { SecureStore.getItemAsync(sessionKey).then(v => { if (v) { try { const session = JSON.parse(v); if (Date.parse(session.expires) <= Date.now()) { void SecureStore.deleteItemAsync(sessionKey); return; } setToken(session.token); setUser(session.user); setRole(session.user.role); } catch { void SecureStore.deleteItemAsync(sessionKey); } } }); }, []);
useEffect(() => { if (!user) return; let cancelled = false; const id = user.id; setDraftReady(''); void (async () => { try { const [draft, cached, unconfirmed] = await Promise.all(['draft', 'state', 'pending'].map(kind => AsyncStorage.getItem(localKey(id, kind)))); if (cancelled) return; const d = draft ? JSON.parse(draft) : { table: 1, cart: {}, notes: '' }; setTable(d.table); setCart(d.cart); setInstructions(d.notes); setPending(unconfirmed ? JSON.parse(unconfirmed) : null); if (cached) { const c = JSON.parse(cached); if (Date.now() - c.at < 86400000) setData(previous => previous || c.value); } setDraftReady(id); } catch { if (!cancelled) setDraftError('Unable to restore device storage. Keep this screen open.'); } })(); return () => { cancelled = true; }; }, [user?.id,user?.branchId]);
useEffect(() => { if (!user || draftReady !== user.id) return; const key = localKey(user.id, 'draft'); const value = JSON.stringify({ table, cart, notes: instructions }); writes.current = writes.current.catch(() => { }).then(() => AsyncStorage.setItem(key, value)).then(() => setDraftError('')).catch(() => setDraftError('Draft could not be saved on this device. Keep this screen open.')); }, [user?.id, draftReady, table, cart, instructions]);
useEffect(() => { if (!token || !user) return; let stopped = false; let retry: ReturnType<typeof setTimeout>; let debounce: ReturnType<typeof setTimeout>; let attempt = 0; let foreground = AppState.currentState === 'active'; const sync = () => { clearTimeout(debounce); debounce = setTimeout(() => { if (!stopped && foreground) void refresh(); }, 350); }; void refresh(); const hub = new HubConnectionBuilder().withUrl(API + '/hubs/orders', { accessTokenFactory: () => token }).withAutomaticReconnect([0, 2000, 5000, 15000, 30000]).build(); hub.on('StateChanged', sync); hub.on('FoodReady', n => { Alert.alert('Food ready', n.message); sync(); }); hub.onreconnecting(() => setLive('Reconnecting')); hub.onreconnected(() => { setLive('Connected'); sync(); }); const start = () => { if (stopped || !foreground || hub.state !== 'Disconnected') return; void hub.start().then(() => { attempt = 0; setLive('Connected'); sync(); }).catch(() => { setLive('Reconnecting'); if (!stopped) retry = setTimeout(start, Math.min(30000, 2000 * 2 ** Math.min(attempt++, 4))); }); }; hub.onclose(() => { if (!stopped) retry = setTimeout(start, 5000); }); start(); const interval = setInterval(sync, 90000); const listener = AppState.addEventListener('change', state => { foreground = state === 'active'; if (foreground) { sync(); start(); } }); return () => { stopped = true; clearTimeout(retry); clearTimeout(debounce); clearInterval(interval); listener.remove(); void hub.stop(); }; }, [token, user?.id]);
async function run(path: string, body: any, method = 'POST') { if (posting.current || !user) return false; posting.current = true; setBusy(true); setError(''); const id = user.id; try { if (path === '/orders') { await sendOrder({ body, uuid: randomUUID, read: async () => { const value = await AsyncStorage.getItem(localKey(id, 'pending')); return value ? JSON.parse(value) : null; }, write: async value => { await AsyncStorage.setItem(localKey(id, 'pending'), JSON.stringify(value)); if (currentAuth.current === token) setPending(value); }, remove: async confirmed => { if (confirmed) { await writes.current; const saved = await AsyncStorage.getItem(localKey(id, 'draft')); if (saved && orderSignature(draftBody(JSON.parse(saved))) === orderSignature(body)) await AsyncStorage.removeItem(localKey(id, 'draft')); } await AsyncStorage.removeItem(localKey(id, 'pending')); if (currentAuth.current === token) setPending(null); }, send: payload => api(path, method, payload) }); if (currentAuth.current !== token) return false; if (orderSignature(draftBody(draftRef.current)) === orderSignature(body)) { setCart({}); setInstructions(''); } } else await api(path, method, body); void refresh(); return true; } catch (e) { setError((e as Error).message); return false; } finally { posting.current = false; setBusy(false); } }
async function signIn() { setBusy(true); try { const session = await api('/auth/login', 'POST', { email, password }); await SecureStore.setItemAsync(sessionKey, JSON.stringify(session)); setToken(session.token); setUser(session.user); setRole(session.user.role); setPassword(''); setError(''); } catch (e) { setError((e as Error).message); } finally { setBusy(false); } }
const field = (label: string, value: string, onChange: (v: string) => void, secure = false) => <View style={s.field}><Text style={s.label}>{label}</Text><TextInput style={s.input} value={value} onChangeText={onChange} secureTextEntry={secure} autoCapitalize="none" /></View>;
const pushStatus=usePushNotifications(token,api);
async function acceptSession(session:any){await writes.current;await SecureStore.setItemAsync(sessionKey,JSON.stringify(session));setData(null);setToken(session.token);setUser(session.user);setRole(session.user.role);setDraftReady('');setCart({});setInstructions('');}
return {pushStatus,acceptSession,localKey,token,setToken,user,setUser,data,setData,role,setRole,tab,setTab,email,setEmail,password,setPassword,busy,setBusy,error,setError,live,setLive,table,setTable,cart,setCart,instructions,setInstructions,query,setQuery,category,setCategory,paymentMethod,setPaymentMethod,reference,setReference,adminSection,setAdminSection,edit,setEdit,lastSync,setLastSync,syncError,setSyncError,syncing,setSyncing,pending,setPending,draftReady,setDraftReady,draftError,setDraftError,posting,currentAuth,draftRef,writes,api,refresh,run,signIn,field,API,sessionKey,onChangeServer};
}
