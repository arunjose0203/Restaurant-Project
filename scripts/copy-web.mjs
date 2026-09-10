import {cpSync,mkdirSync,rmSync} from 'node:fs';
import {fileURLToPath} from 'node:url';
import {dirname,resolve} from 'node:path';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'..');
const output=fileURLToPath(new URL('../dist/',import.meta.url));
if(resolve(output)!==resolve(root,'dist'))throw Error('Unexpected build output path');
// Remove only this project's generated output so stale bundles are never packaged.
rmSync(output,{recursive:true,force:true});
mkdirSync(output,{recursive:true});
cpSync(resolve(root,'web/dist'),output,{recursive:true});
