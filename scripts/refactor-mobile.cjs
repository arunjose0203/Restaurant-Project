const fs=require('node:fs');const ts=require('../web/node_modules/typescript');const path=require('node:path');
const root=path.resolve(__dirname,'../mobile');const source=fs.readFileSync(path.join(root,'App.tsx'),'utf8');const tree=ts.createSourceFile('App.tsx',source,ts.ScriptTarget.Latest,true,ts.ScriptKind.TSX);
const app=tree.statements.find(n=>ts.isFunctionDeclaration(n)&&n.name?.text==='App');
const decl=name=>tree.statements.find(n=>n.name?.text===name||n.declarationList?.declarations[0]?.name?.text===name).getText(tree);
const imports=tree.statements.filter(ts.isImportDeclaration).map(n=>n.getText(tree)).join('\n');
fs.writeFileSync(path.join(root,'ui.tsx'),`import {Pressable,Text,StyleSheet} from 'react-native';\nimport type {Order} from '../web/src/types';\n`+['money','sum','Button','s'].map(n=>'export '+decl(n)).join('\n'));
const statements=app.body.statements;const firstIf=statements.findIndex(ts.isIfStatement);
const controller=statements.slice(0,firstIf).map(n=>n.getText(tree)).join('\n');
const names=[];for(const n of statements.slice(0,firstIf)){if(ts.isFunctionDeclaration(n))names.push(n.name.text);if(n.declarationList)for(const d of n.declarationList.declarations){if(ts.isArrayBindingPattern(d.name))names.push(...d.name.elements.filter(ts.isBindingElement).map(e=>e.name.getText(tree)));else names.push(d.name.getText(tree));}}
names.push('API','sessionKey','onChangeServer');
let hookImports=imports.replaceAll("'../shared/","'../../shared/").replaceAll("'../web/","'../../web/");
fs.mkdirSync(path.join(root,'hooks'),{recursive:true});
fs.writeFileSync(path.join(root,'hooks/useWorkspace.tsx'),hookImports+"\nimport {Button,s,money,sum} from '../ui';\n"+decl('draftBody')+'\nexport function useWorkspace('+app.parameters[0].getText(tree)+'){\n'+controller+'\nreturn {'+names.join(',')+'};\n}\n');
let render=statements.slice(firstIf).map(n=>n.getText(tree)).join('\n');
const cardNode=statements.find(n=>n.declarationList?.declarations.some(d=>d.name.getText(tree)==='card'));
const card=cardNode.declarationList.declarations[0].initializer;
fs.writeFileSync(path.join(root,'features/OrderTicket.tsx'),`import {View,Text} from 'react-native';\nimport {Button,s,money,sum} from '../ui';\nimport type {Order,Role} from '../../web/src/types';\nexport function OrderTicket({o,role,busy,run}:{o:Order;role:Role;busy:boolean;run:(path:string,body:any)=>Promise<boolean>}){return ${card.body.getText(tree)};}\n`);
render=render.replace(cardNode.getText(tree),`const card=(o:Order)=><OrderTicket o={o} role={role} busy={busy} run={run} key={o.id}/>;`);
for(const [role,next] of [['Waiter','Kitchen'],['Kitchen','Cashier'],['Cashier','Admin']]){
 const start=render.indexOf(`{role === '${role}' &&`);const end=render.indexOf(`{role === '${next}' &&`,start);let jsx=render.slice(start,end).trim();jsx=jsx.slice(`{role === '${role}' &&`.length,-1);
 if(role==='Cashier')jsx='<PosBilling data={data} api={api} storageScope={localKey(user.id,"pos")} refresh={refresh}/>';
 if(role==='Waiter'){
  const begin=jsx.indexOf("{tab === 'Order' ?");const otherwise=jsx.indexOf(' : <>{own.filter',begin);
  jsx=jsx.slice(0,begin)+"{tab === 'Order' ? <OrderComposer data={data} storageScope={localKey(user.id,'pos')} busy={busy} onSubmit={body=>run('/orders',body)}/>"+jsx.slice(otherwise);
 }
 const component=`import type {useWorkspace} from '../hooks/useWorkspace';\nimport {OrderComposer} from './OrderComposer';\nimport {PosBilling} from './PosBilling';\nimport {Button,s,money,sum} from '../ui';\nimport {View,Text,ScrollView,TextInput,Pressable,Alert} from 'react-native';\nimport type {Order} from '../../web/src/types';\nexport function ${role}Screen({workspace,card}:{workspace:ReturnType<typeof useWorkspace>;card:(o:Order)=>React.ReactNode}){const {${names.filter(n=>!['data','user'].includes(n)).join(',')}}=workspace;const data=workspace.data!;const user=workspace.user!;const own=data.orders.filter(o=>role!=='Waiter'||o.waiterId===user.id);return ${jsx};}\n`;
 fs.writeFileSync(path.join(root,'features',role+'Screen.tsx'),component);
 render=render.slice(0,start)+`{role === '${role}' && <${role}Screen workspace={workspace} card={card}/>}\n`+render.slice(end);
}
const start=render.indexOf("{role === 'Admin' && <>");const end=render.lastIndexOf('</ScrollView>');let jsx=render.slice(start,end).trim();jsx=jsx.slice("{role === 'Admin' &&".length,-1);
fs.writeFileSync(path.join(root,'features/AdminScreen.tsx'),`import type {useWorkspace} from '../hooks/useWorkspace';\nimport {Button,s,money,sum} from '../ui';\nimport {View,Text} from 'react-native';\nimport {useEffect,useState} from 'react';\nimport type {State,Order} from '../../web/src/types';\nexport function AdminScreen({workspace}:{workspace:ReturnType<typeof useWorkspace>}){const {${names.filter(n=>!['data','user'].includes(n)).join(',')}}=workspace;const data=workspace.data!;const user=workspace.user!;return ${jsx};}\n`+decl('MobileSales')+'\n'+decl('MobileHistory'));
render=render.slice(0,start)+"{role === 'Admin' && <AdminScreen workspace={workspace}/>}\n"+render.slice(end);
fs.writeFileSync(path.join(root,'App.tsx'),imports+`\nimport {useWorkspace} from './hooks/useWorkspace';\nimport {Button,s,money,sum} from './ui';\nimport {OrderTicket} from './features/OrderTicket';\nimport {WaiterScreen} from './features/WaiterScreen';\nimport {KitchenScreen} from './features/KitchenScreen';\nimport {CashierScreen} from './features/CashierScreen';\nimport {AdminScreen} from './features/AdminScreen';\nexport default function App(config:${app.parameters[0].type.getText(tree)}){const workspace=useWorkspace(config);const {${names.join(',')}}=workspace;\n`+render+'\n}\n');
