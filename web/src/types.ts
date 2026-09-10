export type Role='Waiter'|'Kitchen'|'Cashier'|'Admin';
export type User={id:string;name:string;email:string;role:Role;active:boolean};
export type MenuItem={id:number;name:string;description:string;categoryId:number;price:number;active:boolean;vegetarian:boolean};
export type Table={id:number;name:string;seats:number;active:boolean};
export type Order={id:string;sessionId:string;tableId:number;waiterId:string;status:string;instructions:string;createdAt:string;items:{menuItemId:number;name:string;quantity:number;unitPrice:number}[]};
export type State={categories:{id:number;name:string}[];menu:MenuItem[];tables:Table[];paymentMethods:{id:number;name:string;active:boolean}[];sessions:{id:string;tableId:number;closedAt:string|null}[];orders:Order[];notifications:{id:string;userId:string;message:string;read:boolean}[];bills:{id:string;sessionId:string;tableId:number;total:number;paymentMethodId:number;paidAt:string;reference:string}[];users:User[];roles:Role[]};
