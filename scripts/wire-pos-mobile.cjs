const fs=require('node:fs');
let p='mobile/hooks/useWorkspace.tsx';let s=fs.readFileSync(p,'utf8');s="import {usePushNotifications} from './usePushNotifications';\n"+s;
s=s.replace('`tableflow:${API}:${id}:${kind}`','`tableflow:${API}:${user?.branchId||1}:${id}:${kind}`');
s=s.replace("path !== '/auth/login'", "!['/auth/login','/auth/pin-login'].includes(path)");
s=s.replaceAll('[user?.id]', '[user?.id,user?.branchId]');
s=s.replace('return {localKey,',`const pushStatus=usePushNotifications(token,api);
async function acceptSession(session:any){await writes.current;await SecureStore.setItemAsync(sessionKey,JSON.stringify(session));setData(null);setToken(session.token);setUser(session.user);setRole(session.user.role);setDraftReady('');setCart({});setInstructions('');}
return {pushStatus,acceptSession,localKey,`);
fs.writeFileSync(p,s);
p='mobile/App.tsx';s=fs.readFileSync(p,'utf8');s="import {StaffTools} from './features/StaffTools';\nimport {PrinterTools} from './features/PrinterTools';\n"+s;s=s.replace('const {localKey,','const {pushStatus,acceptSession,localKey,');s=s.replace('<OrderTicket o={o}', '<><PrinterTools API={API} token={token} kind="kot" id={o.id}/><OrderTicket o={o}');s=s.replace('key={o.id}/>;', 'key={o.id}/></>;');s=s.replace('</ScrollView></KeyboardAvoidingView></SafeAreaView>;\n}', '{!!pushStatus&&<Text style={s.meta}>{pushStatus}</Text>}<StaffTools user={user} data={data} api={api} onSession={acceptSession} refresh={refresh}/></ScrollView></KeyboardAvoidingView></SafeAreaView>;\n}');fs.writeFileSync(p,s);
p='mobile/features/OrderTicket.tsx';s=fs.readFileSync(p,'utf8');s="import {kitchenAge} from '../../shared/kitchen.mjs';\n"+s;s=s.replace('style={s.card} key={o.id}',"style={[s.card,role==='Kitchen'&&o.status!=='Ready'?{borderLeftWidth:5,borderLeftColor:kitchenAge(o.createdAt).level==='delayed'?'#c64f4f':kitchenAge(o.createdAt).level==='warning'?'#dba41c':'#2d9161'}:{}]} key={o.id}");s=s.replace('<Text style={s.meta}>#','<Text style={s.meta}>{kitchenAge(o.createdAt).minutes} min · #');fs.writeFileSync(p,s);
p='mobile/app.json';const config=JSON.parse(fs.readFileSync(p,'utf8'));config.expo.plugins.push('expo-notifications','./plugins/with-thermal-printer');fs.writeFileSync(p,JSON.stringify(config,null,2)+'\n');
