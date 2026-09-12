const fs = require('node:fs');
const path = require('node:path');
const { spawn, spawnSync } = require('node:child_process');

const root = path.resolve(__dirname, '..');
const dir = path.join(root, '.tools/pos-test');
const bin = path.join(dir, 'node_modules/@embedded-postgres/windows-x64/native/bin');
const data = path.join(dir, 'data');

if (!fs.existsSync(data)) {
  const r = spawnSync(path.join(bin, 'initdb.exe'), ['-D', data, '-U', 'postgres', '-A', 'trust', '--encoding=UTF8', '--locale=C'], { windowsHide: true, encoding: 'utf8' });
  if (r.status !== 0) throw new Error(r.stderr || r.stdout);
}

// Start PostgreSQL
spawnSync(path.join(bin, 'pg_ctl.exe'), ['-D', data, '-l', path.join(dir, 'postgres.log'), '-o', '-h 127.0.0.1 -p 55439', '-w', 'start'], { windowsHide: true, stdio: 'ignore', timeout: 15000 });

const env = {
  ...process.env,
  ConnectionStrings__Restaurant: 'Host=127.0.0.1;Port=55439;Database=postgres;Username=postgres',
  Jwt__Key: 'test-secret-key-that-is-at-least-32-chars-long-123456789',
  Jwt__Issuer: 'pos-test',
  Jwt__Audience: 'pos-test',
  Bootstrap__Email: 'admin@pos.test',
  Bootstrap__Password: 'Test-only-password-123!',
  ASPNETCORE_URLS: 'http://127.0.0.1:5189',
  ASPNETCORE_ENVIRONMENT: 'Development',
  Cors__Origins__0: 'http://localhost:5190',
  Cors__Origins__1: 'http://localhost:5173',
  Cors__Origins__2: 'http://127.0.0.1:5173',
  Cors__Origins__3: 'http://127.0.0.1:5190'
};

const dll = path.join(root, 'server/Restaurant.Api/bin/Debug/net10.0/Restaurant.Api.dll');

// Run migration
console.log('Ensuring database schema is up-to-date...');
spawnSync('dotnet', [dll, '--migrate'], { cwd: root, env, windowsHide: true, stdio: 'inherit' });

console.log('Starting Restaurant.Api server on http://127.0.0.1:5189...');
const child = spawn('dotnet', [dll], { cwd: root, env, windowsHide: true, stdio: 'inherit' });

child.on('exit', (code) => {
  console.log('API server exited with code ' + code);
  process.exit(code || 0);
});

process.on('SIGINT', () => { child.kill('SIGINT'); });
process.on('SIGTERM', () => { child.kill('SIGTERM'); });
