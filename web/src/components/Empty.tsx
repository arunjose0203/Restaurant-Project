import { CheckCircle2 } from 'lucide-react';

export function Empty({text}: {text:string}){return <div className="empty"><CheckCircle2 size={30}/><p>{text}</p></div>;}
