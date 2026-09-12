

export function Stat({icon:Icon,label,value,sub,accent=false}:any){return <div className={`stat ${accent?'accent':''}`}><div className="stat-top"><span>{label}</span><Icon size={19}/></div><strong>{value}</strong><small>{sub}</small></div>;}
