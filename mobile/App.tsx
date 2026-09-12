import {StaffTools} from './features/StaffTools';
import {PrinterTools} from './features/PrinterTools';
import { SafeAreaView } from 'react-native-safe-area-context';
import React, { useState, useEffect, useRef, useMemo } from 'react';
import { ScrollView, View, Text, TextInput, Pressable, StyleSheet, Alert, ActivityIndicator, AppState, KeyboardAvoidingView, Platform } from 'react-native';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { randomUUID } from 'expo-crypto';
import { jsonRequest, RequestError, sendOrder, orderSignature, coalescedRefresh } from '../shared/network.mjs';
import * as SecureStore from 'expo-secure-store';
import { StatusBar } from 'expo-status-bar';
import { HubConnectionBuilder } from '@microsoft/signalr';
import type { State, User, Order, Role } from '../web/src/types';
import {useWorkspace} from './hooks/useWorkspace';
import {Button,s,money,sum} from './ui';
import {OrderTicket} from './features/OrderTicket';
import {WaiterScreen} from './features/WaiterScreen';
import {KitchenScreen} from './features/KitchenScreen';
import {CashierScreen} from './features/CashierScreen';
import {AdminScreen} from './features/AdminScreen';
export default function App(config:{ API: string; sessionKey: string; onChangeServer: () => void }){const workspace=useWorkspace(config);const {pushStatus,acceptSession,localKey,token,setToken,user,setUser,data,setData,role,setRole,tab,setTab,email,setEmail,password,setPassword,busy,setBusy,error,setError,live,setLive,table,setTable,cart,setCart,instructions,setInstructions,query,setQuery,category,setCategory,paymentMethod,setPaymentMethod,reference,setReference,adminSection,setAdminSection,edit,setEdit,lastSync,setLastSync,syncError,setSyncError,syncing,setSyncing,pending,setPending,draftReady,setDraftReady,draftError,setDraftError,posting,currentAuth,draftRef,writes,api,refresh,run,signIn,field,API,sessionKey,onChangeServer}=workspace;
if (!user) return <SafeAreaView style={s.safe}><KeyboardAvoidingView behavior={Platform.OS === 'ios' ? 'padding' : undefined} style={{ flex: 1 }}><ScrollView contentContainerStyle={s.login}><Text style={s.brand}>tableflow.</Text><Text style={s.h1}>Welcome to your shift.</Text><Text style={s.body}>Sign in to your restaurant workspace.</Text><Text style={s.meta}>{API}</Text>{field('Email', email, setEmail)}{field('Password', password, setPassword, true)}{!!error && <Text style={s.error}>{error}</Text>}<Button title={busy ? 'Signing in…' : 'Sign in'} disabled={busy || !email || !password} onPress={signIn} /><Button title="Change restaurant server" secondary disabled={busy} onPress={onChangeServer} /></ScrollView></KeyboardAvoidingView></SafeAreaView>;
if (!data) return <SafeAreaView style={s.safe}><View style={s.content}>{syncing && <ActivityIndicator />}<Text>{error || syncError || 'Opening your workspace…'}</Text><Button title="Retry connection" disabled={syncing} onPress={() => void refresh()} /><Button title="Back to sign in" secondary onPress={() => { void SecureStore.deleteItemAsync(sessionKey); setUser(null); setToken(''); setData(null); }} /></View></SafeAreaView>;
const add = (id: number, delta: number) => setCart(c => ({ ...c, [id]: Math.max(0, Math.min(99, (c[id] || 0) + delta)) }));
const own = data.orders.filter(o => role !== 'Waiter' || o.waiterId === user.id);
const card=(o:Order)=><><PrinterTools API={API} token={token} kind="kot" id={o.id}/><OrderTicket o={o} role={role} busy={busy} run={run} key={o.id}/></>;
return <SafeAreaView style={s.safe}><StatusBar style="dark" /><View style={s.header}><Text style={s.brand}>tableflow.</Text><Text style={s.meta}>{live}</Text><Pressable accessibilityRole="button" onPress={async () => { await writes.current; await AsyncStorage.removeItem(localKey(user.id, 'state')); await SecureStore.deleteItemAsync(sessionKey); setToken(''); setUser(null); setData(null); setDraftReady(''); setCart({}); setInstructions(''); }}><Text style={s.link}>Sign out</Text></Pressable></View><KeyboardAvoidingView style={{ flex: 1 }} behavior={Platform.OS === 'ios' ? 'padding' : undefined}><ScrollView contentContainerStyle={s.content} keyboardShouldPersistTaps="handled"><Text style={s.meta}>{user.name} · {role}</Text><Text style={s.h1}>{role === 'Waiter' ? 'Service, made simple.' : role === 'Kitchen' ? 'On the pass.' : role === 'Cashier' ? 'Billing & payments' : 'Restaurant control'}</Text>{user.role === 'Admin' && <View style={s.wrap}>{(['Waiter', 'Kitchen', 'Cashier', 'Admin'] as Role[]).map(r => <Button title={r} key={r} secondary={r !== role} onPress={() => setRole(r)} />)}</View>}{!!error && <Text accessibilityRole="alert" style={s.error}>{error}</Text>}{data.notifications.filter(n => n.userId === user.id && !n.read).map(n => <View key={n.id} style={s.note}><Text>{n.message}</Text><Button title="Dismiss" secondary onPress={() => run(`/notifications/${n.id}/read`, {})} /></View>)}
        {<View style={s.note}><Text>{syncing ? 'Checking for updates…' : lastSync ? 'Last updated ' + new Date(lastSync).toLocaleTimeString() : 'Showing the saved workspace'}</Text>{!!syncError && <Text>{syncError}</Text>}<Button title="Refresh status" secondary disabled={syncing} onPress={() => void refresh()} /></View>}
        {!!pending && <View style={s.note}><Text style={s.h2}>Order confirmation pending · Table {pending.body.tableId}</Text><Text>Your order is saved on this device. Check confirmation before placing it again.</Text><Button title="Check order confirmation" disabled={busy} onPress={async () => { if (await run('/orders', pending.body)) Alert.alert('Order confirmed', 'The kitchen received the order.'); }} /></View>}
        {role === 'Waiter' && <WaiterScreen workspace={workspace} card={card}/>}
{role === 'Kitchen' && <KitchenScreen workspace={workspace} card={card}/>}
{role === 'Cashier' && <CashierScreen workspace={workspace} card={card}/>}
{role === 'Admin' && <AdminScreen workspace={workspace}/>}
{!!pushStatus&&<Text style={s.meta}>{pushStatus}</Text>}<StaffTools user={user} data={data} api={api} onSession={acceptSession} refresh={refresh}/></ScrollView></KeyboardAvoidingView></SafeAreaView>;
}
