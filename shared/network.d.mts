export class RequestError extends Error {status:number;constructor(message:string,status?:number);}
export function jsonRequest(url:string,options?:{method?:string;body?:unknown;token?:string;timeoutMs?:number;fetchImpl?:typeof fetch;retries?:number;sleep?:(ms:number)=>Promise<void>}):Promise<any>;
export function coalescedRefresh(work:()=>Promise<void>):()=>Promise<void>;
export function orderSignature(body:any):string;
export function sendOrder(options:{read:()=>Promise<any>;write:(pending:any)=>Promise<void>;remove:(confirmed?:boolean)=>Promise<void>;send:(body:any)=>Promise<any>;body:any;uuid:()=>string}):Promise<any>;
