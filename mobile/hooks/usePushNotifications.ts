import {useEffect,useState} from 'react';
import {Platform} from 'react-native';
import Constants from 'expo-constants';
import * as Notifications from 'expo-notifications';

Notifications.setNotificationHandler({handleNotification:async()=>({shouldShowBanner:true,shouldShowList:true,shouldPlaySound:true,shouldSetBadge:false})});
export function usePushNotifications(token:string,register:(path:string,method:string,body:unknown)=>Promise<any>){
 const [status,setStatus]=useState('');
 useEffect(()=>{if(!token)return;let cancelled=false;let registered='';
  void(async()=>{try{
   const projectId=Constants.expoConfig?.extra?.eas?.projectId||Constants.easConfig?.projectId;
   if(!projectId){setStatus('Background alerts need an Expo project and push credentials. Foreground alerts remain active.');return;}
   if(Platform.OS==='android')await Notifications.setNotificationChannelAsync('orders',{name:'Kitchen orders',importance:Notifications.AndroidImportance.MAX,vibrationPattern:[0,500,250,500],sound:'default'});
   let permissions=await Notifications.getPermissionsAsync();if(!permissions.granted)permissions=await Notifications.requestPermissionsAsync();
   if(!permissions.granted){setStatus('Notifications disabled in device settings.');return;}
   const result=await Notifications.getExpoPushTokenAsync({projectId});if(cancelled)return;
   await register('/pos/devices','PUT',{token:result.data});registered=result.data;setStatus('Background order alerts enabled.');
  }catch(e){setStatus('Background alert registration failed. '+(e as Error).message);}})();
  return()=>{cancelled=true;if(registered)void register('/pos/devices/remove','POST',{token:registered}).catch(()=>{});};
 },[token]);
 return status;
}
