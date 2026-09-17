import {useEffect,useRef,type ReactNode} from 'react';
import {X} from 'lucide-react';

export function Dialog({title,description,children,onClose}:{title:string;description?:string;children:ReactNode;onClose:()=>void}) {
 const ref=useRef<HTMLDialogElement>(null);
 useEffect(()=>{ref.current?.showModal();return()=>ref.current?.close();},[]);
 return <dialog ref={ref} className="app-dialog" aria-label={title} onCancel={onClose} onClick={e=>{if(e.target===ref.current)onClose();}}>
  <div className="dialog-heading"><div><h2>{title}</h2>{description&&<p>{description}</p>}</div><button type="button" className="icon-button" aria-label="Close dialog" onClick={onClose}><X size={20}/></button></div>{children}
 </dialog>;
}
