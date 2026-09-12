import type { Order } from '../types';

export const money=(n:number)=>new Intl.NumberFormat('en-IN',{style:'currency',currency:'INR',maximumFractionDigits:2}).format(n);

export const total=(o:Order)=>o.items.reduce((s,i)=>s+i.quantity*i.unitPrice,0);

export const short=(id:string)=>id.slice(0,6).toUpperCase();

export const elapsed=(date:string)=>`${Math.max(0,Math.floor((Date.now()-new Date(date).getTime())/60000))} min`;
