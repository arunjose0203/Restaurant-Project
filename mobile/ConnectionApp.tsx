import {SafeAreaView} from 'react-native-safe-area-context';
import React, {useEffect, useState} from 'react';
import {ActivityIndicator, KeyboardAvoidingView, Platform, Pressable,  ScrollView, StyleSheet, Text, TextInput, View} from 'react-native';
import AsyncStorage from '@react-native-async-storage/async-storage';
import {CryptoDigestAlgorithm, digestStringAsync} from 'expo-crypto';
import {StatusBar} from 'expo-status-bar';
import RestaurantApp from './App';
import {normalizeServerAddress} from './server-address';
import {jsonRequest} from '../shared/network.mjs';

const serverStorageKey = 'tableflow:server-address';
type Connection = {url: string; sessionKey: string};
async function connectionFor(url: string): Promise<Connection> {
  return {url, sessionKey: `session.${await digestStringAsync(CryptoDigestAlgorithm.SHA256, url)}`};
}

export default function ConnectionApp() {
  const [connection, setConnection] = useState<Connection | null>(null);
  const [address, setAddress] = useState('');
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  useEffect(() => {
    void (async () => {
      try {
        const saved = await AsyncStorage.getItem(serverStorageKey);
        const value = saved || process.env.EXPO_PUBLIC_API_URL;
        if (value) {
          const url = normalizeServerAddress(value);
          setAddress(url);
          setConnection(await connectionFor(url));
        }
      } catch { setError('Your saved connection could not be opened. Enter the server address again.'); }
      finally { setLoading(false); }
    })();
  }, []);

  async function connect() {
    setBusy(true);
    setError('');
    try {
      const url = normalizeServerAddress(address);
      const health = await jsonRequest(`${url}/health`, {timeoutMs: 12000, retries: 0});
      if (health?.status !== 'ok' || health?.database !== 'PostgreSQL') {
        throw new Error('This address did not respond as a Tableflow restaurant server. Check the address with your administrator.');
      }
      const next = await connectionFor(url);
      await AsyncStorage.setItem(serverStorageKey, url);
      setAddress(url);
      setConnection(next);
    } catch (e) { setError((e as Error).message); }
    finally { setBusy(false); }
  }

  if (loading) return <SafeAreaView style={styles.safe}><View style={styles.center}><ActivityIndicator color="#165b45"/><Text style={styles.body}>Opening Tableflow…</Text></View></SafeAreaView>;
  if (connection) return <RestaurantApp key={connection.url} API={connection.url} sessionKey={connection.sessionKey} onChangeServer={() => setConnection(null)}/>;
  return <SafeAreaView style={styles.safe}>
    <StatusBar style="dark"/>
    <KeyboardAvoidingView style={{flex: 1}} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
      <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
        <View style={styles.mark}><Text style={styles.markText}>t.</Text></View>
        <Text style={styles.brand}>tableflow.</Text>
        <Text style={styles.heading}>Your restaurant.\nOne shared workspace.</Text>
        <Text style={styles.body}>Orders, kitchen updates and billing — connected across your team's phones.</Text>
        <View style={styles.card}>
          <Text style={styles.title}>Connect your restaurant</Text>
          <Text style={styles.body}>Enter the secure server address provided by your restaurant administrator.</Text>
          <Text style={styles.label}>Restaurant server address</Text>
          <TextInput accessibilityLabel="Restaurant server address" style={styles.input} value={address} onChangeText={setAddress} placeholder="https://restaurant.example.com" placeholderTextColor="#78857b" autoCapitalize="none" autoCorrect={false} keyboardType="url" editable={!busy} returnKeyType="go" onSubmitEditing={() => {if (!busy && address.trim()) void connect();}}/>
          {!!error && <Text accessibilityRole="alert" style={styles.error}>{error}</Text>}
          <Pressable accessibilityRole="button" style={[styles.button, (busy || !address.trim()) && {opacity: .5}]} disabled={busy || !address.trim()} onPress={() => void connect()}><Text style={styles.buttonText}>{busy ? 'Checking connection…' : 'Connect & continue'}</Text></Pressable>
        </View>
        <View style={styles.note}><Text style={styles.title}>No server yet?</Text><Text style={styles.body}>You can keep this app installed. Once your restaurant's server is ready, enter its address here and sign in with your staff account.</Text></View>
        <Text style={styles.footer}>WAITER · KITCHEN · CASHIER · ADMIN</Text>
      </ScrollView>
    </KeyboardAvoidingView>
  </SafeAreaView>;
}

const styles = StyleSheet.create({
  safe: {flex: 1, backgroundColor: '#f6f8f5'},
  center: {flex: 1, justifyContent: 'center', alignItems: 'center', gap: 12},
  content: {padding: 24, paddingTop: 36, paddingBottom: 40, gap: 16, maxWidth: 600, width: '100%', alignSelf: 'center'},
  mark: {width: 62, height: 62, borderRadius: 20, backgroundColor: '#165b45', alignItems: 'center', justifyContent: 'center'},
  markText: {color: '#fff', fontSize: 40, fontWeight: '800'},
  brand: {fontSize: 28, fontWeight: '800', color: '#174e39', letterSpacing: -1},
  heading: {fontSize: 30, fontWeight: '700', color: '#263e30', lineHeight: 37},
  title: {fontSize: 18, fontWeight: '600', color: '#314d38'},
  body: {fontSize: 15, color: '#53675a', lineHeight: 23},
  card: {backgroundColor: '#fff', padding: 20, borderRadius: 16, borderWidth: 1, borderColor: '#dce5db', gap: 12},
  label: {fontSize: 14, fontWeight: '600', color: '#314d38', marginTop: 8},
  input: {borderWidth: 1, borderColor: '#cbd8c9', borderRadius: 8, padding: 13, fontSize: 15, color: '#263e30', backgroundColor: '#fafcf9'},
  button: {backgroundColor: '#165b45', padding: 16, borderRadius: 8, alignItems: 'center', minHeight: 48},
  buttonText: {fontSize: 15, fontWeight: '600', color: '#fff'},
  error: {color: '#a13b2e', backgroundColor: '#fff0e9', padding: 12, borderRadius: 8, lineHeight: 21},
  note: {backgroundColor: '#e8f0e3', borderRadius: 12, padding: 18, gap: 8},
  footer: {fontSize: 11, color: '#53675a', textAlign: 'center', letterSpacing: 1.4, marginTop: 8},
});
