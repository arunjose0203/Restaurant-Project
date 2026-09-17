import {GuestPortal} from './features/pos/GuestPortal';
import './features/pos/pos.css';
import { createRoot } from 'react-dom/client';
import { App } from './App';
import './style.css';
import './design.css';

const guestToken=window.location.pathname.match(/^\/t\/([a-fA-F0-9]{64})$/)?.[1];
createRoot(document.getElementById('root')!).render(guestToken?<GuestPortal token={guestToken}/>:<App/>);

if ((import.meta as any).env.PROD && 'serviceWorker' in navigator) {
  window.addEventListener('load', () => {
    void navigator.serviceWorker.register('/sw.js').catch(() => {});
  });
}
