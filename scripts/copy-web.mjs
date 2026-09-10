import {cpSync,mkdirSync} from 'node:fs';
mkdirSync(new URL('../dist/',import.meta.url),{recursive:true});
cpSync(new URL('../web/dist/',import.meta.url),new URL('../dist/',import.meta.url),{recursive:true});
