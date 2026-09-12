import {useEffect,useState} from 'react';
import QRCode from 'qrcode';
export function QrCode({value,label}:{value:string;label:string}){const [src,setSrc]=useState('');useEffect(()=>{let active=true;QRCode.toDataURL(value,{width:220,margin:2}).then(url=>{if(active)setSrc(url);});return()=>{active=false;};},[value]);return src?<figure><img src={src} alt={label}/><figcaption>{label}</figcaption></figure>:null;}
