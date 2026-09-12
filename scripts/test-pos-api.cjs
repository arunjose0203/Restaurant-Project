const fs = require('node:fs');
const path = require('node:path');
const { spawn, spawnSync } = require('node:child_process');
const crypto = require('node:crypto');

const root = path.resolve(__dirname, '..');
const dir = path.join(root, '.tools/pos-test');
const bin = path.join(dir, 'node_modules/@embedded-postgres/windows-x64/native/bin');
const data = path.join(dir, 'data');

if (!fs.existsSync(data)) {
  const r = spawnSync(path.join(bin, 'initdb.exe'), ['-D', data, '-U', 'postgres', '-A', 'trust', '--encoding=UTF8', '--locale=C'], { windowsHide: true, encoding: 'utf8' });
  if (r.status !== 0) throw new Error(r.stderr || r.stdout);
}

// Start PostgreSQL if not already running
spawnSync(path.join(bin, 'pg_ctl.exe'), ['-D', data, '-l', path.join(dir, 'postgres.log'), '-o', '-h 127.0.0.1 -p 55439', '-w', 'start'], { windowsHide: true, stdio: 'ignore', timeout: 15000 });

const env = {
  ...process.env,
  ConnectionStrings__Restaurant: 'Host=127.0.0.1;Port=55439;Database=postgres;Username=postgres',
  Jwt__Key: crypto.randomBytes(48).toString('hex'),
  Jwt__Issuer: 'pos-test',
  Jwt__Audience: 'pos-test',
  Bootstrap__Email: 'admin@pos.test',
  Bootstrap__Password: 'Test-only-password-123!',
  ASPNETCORE_URLS: 'http://127.0.0.1:5189',
  ASPNETCORE_ENVIRONMENT: 'Development',
  Cors__Origins__0: 'http://localhost:5190'
};

const dll = path.join(root, 'server/Restaurant.Api/bin/Debug/net10.0/Restaurant.Api.dll');

console.log('Applying database migrations and seeding...');
const migration = spawnSync('dotnet', [dll, '--migrate'], { cwd: root, env, windowsHide: true, encoding: 'utf8' });
fs.writeFileSync(path.join(dir, 'migration.log'), migration.stdout + '\n' + migration.stderr);
if (migration.status !== 0) {
  throw new Error('Migration failed: ' + (migration.stderr || migration.stdout).slice(-6000));
}

console.log('Starting Restaurant.Api server...');
const out = fs.openSync(path.join(dir, 'api.log'), 'w');
const child = spawn('dotnet', [dll], { cwd: root, env, windowsHide: true, stdio: ['ignore', out, out] });

async function waitForHealth(maxWaitMs = 15000) {
  const start = Date.now();
  while (Date.now() - start < maxWaitMs) {
    try {
      const res = await fetch('http://127.0.0.1:5189/health');
      if (res.status === 200) {
        const body = await res.json();
        if (body.database === 'Healthy' || body.database === 'PostgreSQL') {
          console.log('Server is healthy on http://127.0.0.1:5189');
          return;
        }
      }
    } catch {
      // Retry
    }
    await new Promise(resolve => setTimeout(resolve, 300));
  }
  throw new Error('Server did not become healthy within ' + maxWaitMs + 'ms');
}

(async () => {
  try {
    await waitForHealth();
    console.log('Running pos-api.test.mjs...');
    const testProc = spawnSync(process.execPath, ['--test', path.join(root, 'tests/pos-api.test.mjs')], {
      cwd: root,
      env: { ...process.env, TABLEFLOW_TEST_URL: 'http://127.0.0.1:5189' },
      stdio: 'inherit'
    });
    if (testProc.status !== 0) {
      process.exitCode = testProc.status || 1;
    }
  } catch (err) {
    console.error('Error during test execution:', err);
    process.exitCode = 1;
  } finally {
    console.log('Shutting down test server (pid ' + child.pid + ')...');
    try {
      process.kill(child.pid);
    } catch {}
    // If on Windows, ensure taskkill terminates the process tree
    if (process.platform === 'win32') {
      spawnSync('taskkill', ['/pid', String(child.pid), '/f', '/t'], { stdio: 'ignore' });
    }
  }
})();
