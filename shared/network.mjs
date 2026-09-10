export class RequestError extends Error {
  constructor(message,status=0){super(message);this.status=status;}
}
// A timeout covers headers AND body. Writes are never retried silently.
export async function jsonRequest(url,{method='GET',body,token='',timeoutMs=15000,fetchImpl=fetch,retries=method==='GET'?1:0,sleep=ms=>new Promise(r=>setTimeout(r,ms))}={}){
  for(let attempt=0;;attempt++){
    const controller=new AbortController();let timer;
    try {
      return await Promise.race([
        (async()=>{const response=await fetchImpl(url,{method,headers:{'Content-Type':'application/json',...(token?{Authorization:`Bearer ${token}`}:{})},body:body===undefined?undefined:JSON.stringify(body),signal:controller.signal});
          const value=response.status===204?null:await response.json().catch(()=>{if(response.ok)throw new RequestError('The response was interrupted. Please retry.');return null;});
          if(!response.ok)throw new RequestError(value?.message||(response.status===401?'Please sign in again.':response.status===429?'Too many attempts. Wait a moment before retrying.':`Unable to complete the request (${response.status}).`),response.status);
          return value;
        })(),
        new Promise((_,reject)=>{timer=setTimeout(()=>{controller.abort();reject(new RequestError(method==='GET'?'Connection is slow. Showing the last available information.':'Confirmation was not received. Check the latest status before retrying.'));},timeoutMs);})
      ]);
    } catch(error){
      const status=error instanceof RequestError?error.status:0;
      if(method==='GET'&&attempt<retries&&(status===0||[502,503,504].includes(status))){await sleep(700*(attempt+1));continue;}
      throw error instanceof RequestError?error:new RequestError('Connection interrupted. Your input is still available.');
    } finally {clearTimeout(timer);controller.abort();}
  }
}
// Collapse event bursts into one request, with one follow-up if an event arrives in flight.
export function coalescedRefresh(work){let running=null,again=false;return function refresh(){if(running){again=true;return running;}running=(async()=>{do{again=false;await work();}while(again);})().finally(()=>{running=null;});return running;};}
export function orderSignature(body){return JSON.stringify({tableId:body.tableId,instructions:(body.instructions||'').trim(),items:[...body.items].sort((a,b)=>a.menuItemId-b.menuItemId).map(i=>({menuItemId:i.menuItemId,quantity:i.quantity}))});}
// Persist before sending. A different order cannot replace an unconfirmed submission.
export async function sendOrder({read,write,remove,send,body,uuid}){
  let pending=await read();
  if(pending&&orderSignature(pending.body)!==orderSignature(body))throw new RequestError('An earlier order is awaiting confirmation. Use “Check order confirmation” before sending a different order.');
  if(!pending){pending={body:{...body,clientRequestId:uuid()},createdAt:new Date().toISOString()};await write(pending);}
  try {const result=await send(pending.body);await remove(true);return result;}
  catch(error){
    // These responses definitively reject this request; an interrupted/5xx response is ambiguous.
    if(error instanceof RequestError&&[400,403,404,422].includes(error.status))await remove(false);
    throw error;
  }
}
