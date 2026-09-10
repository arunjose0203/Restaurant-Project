# Tableflow Restaurant Management

React web app, React Native / Expo mobile app, .NET 10 API, PostgreSQL, and authenticated SignalR notifications.

## Delivery status

- Web dashboard: implemented, with an explicit browser-local interactive demo when `VITE_API_URL` is absent.
- .NET API: implemented for **online PostgreSQL only**. No SQLite or in-memory backend fallback.
- Mobile app: native Android/iOS screens implemented; requires a reachable API URL. Device and store builds require your signing accounts/toolchains.
- Online PostgreSQL and API hosting: **not provisioned or connected**. Supply your database connection securely using the configuration below. The demo is not the shared production system.

## Run the web demo

```powershell
cd web
npm ci
npm run dev
```

Open the URL printed by Vite. Switch between Waiter, Kitchen, Reception, and Admin to exercise the workflow. Demo changes are shared across tabs in the same browser using browser storage and Web Locks; they do not sync between devices. Three example table visits are included. Live mode never imports demo state into PostgreSQL. Amounts use INR; prices are treated as final, with no extra tax or service charge configured.

## Connect online PostgreSQL

1. Create a PostgreSQL database in your Neon, Supabase, Azure, or other provider account. Use a direct or session-pool connection that supports transactions and row locks. Enable certificate-verified TLS.
2. Copy `server/Restaurant.Api/appsettings.Local.example.json` to `server/Restaurant.Api/appsettings.Local.json` (ignored by Git).
3. Set `ConnectionStrings:Restaurant` to the provider's **Npgsql format** connection string, not an unconverted `postgres://` URL. Set a random JWT key of at least 32 characters, and an initial administrator password of at least 12 characters. Never place database passwords in either client app.
4. Run the migration and seed command once, from the API directory:

```powershell
cd server/Restaurant.Api
dotnet restore
dotnet run -- --migrate
dotnet run --urls http://localhost:5080
```

Migrations and the generated idempotent SQL script are included. The initial seed creates the admin, five menu categories, ten menu items, twelve tables, and Cash/Card/UPI methods. It does not create shared staff passwords. Log in as the bootstrap admin and add named users with their roles. Remove the bootstrap password from hosted configuration after initial setup.

For production, deploy `server/Dockerfile` with the `server` folder as build context to a .NET-compatible container host. Configure HTTPS at your reverse proxy and enable WebSockets. Run `dotnet Restaurant.Api.dll --migrate` as an explicit release job before starting the API. Restrict migration permissions to the release identity where supported.

Set these host secrets/settings:

```text
ConnectionStrings__Restaurant=Host=...;Database=...;Username=...;Password=...;SSL Mode=VerifyFull
Jwt__Key=<32-or-more-random-characters>
Jwt__Issuer=Tableflow
Jwt__Audience=Tableflow
Bootstrap__Email=<admin-email>
Bootstrap__Password=<initial-password>
Cors__Origins__0=https://your-web-app.example.com
```

Keep one API instance initially. Multiple instances require a SignalR backplane or Azure SignalR; reconnect recovery alone is not instant cross-instance broadcasting.

## Web with the real API

Copy `web/.env.example` to `web/.env` and set `VITE_API_URL` to the API origin. For local API development use `http://localhost:5080`. Rebuild after changing this setting. Login uses API-issued JWTs held in memory and tab-scoped session storage until expiry; refresh restores the current shift. The server authorizes every API request and validates that the account remains active in its current role.

```powershell
cd web
npm ci
npm run build
```

Serve `web/dist` on an HTTPS static host. Sites can host this frontend; its Workers runtime does not execute the .NET API. Add the web origin to API CORS settings.

## Native mobile app

```powershell
cd mobile
npm ci
# Copy .env.example to .env and set EXPO_PUBLIC_API_URL
npm start
```

Use a reachable HTTPS API URL on physical devices, not `localhost`. Mobile tokens are stored in Expo SecureStore. The app uses real native views, supports all four workspaces, resumes state on foreground/reconnect, and displays food-ready alerts while active. Drafts and pending order confirmations are persisted locally. Notifications received while suspended are shown from persisted unread notifications when reopening. Background OS push delivery is **not implemented**; it requires Expo/APNs/FCM registration and delivery infrastructure.

Android: `npm run android` with Android SDK installed. iOS: `npm run ios` on macOS with Xcode. Native store signing and EAS accounts are not configured in this workspace.

## Workflow and permissions

`New → Preparing → Ready → Served → Paid / table closed`

| Role | Actions |
|---|---|
| Waiter / Supplier | Create table orders, see own tickets, receive food-ready notifications, mark own ready orders served |
| Kitchen | See operational tickets, transition New → Preparing → Ready |
| Reception / Cashier | Review table bills, confirm received Cash/Card/UPI payments, close served visits |
| Admin | All operations; menu, categories, prices, tables, users and role assignment, payment methods, sales and history |

Roles are deliberately fixed workflow roles, assigned through user administration. Menu/table/payment records can be deactivated; historical records are preserved. Card and UPI are manually confirmed payment records, not a gateway integration. Final bill prices snapshot each item at ordering time. All rounds in an open table session are combined. Table row locks serialize ordering, serving and payment; payment requires every order served and the reviewed total unchanged. A unique bill per session makes repeat payment submissions idempotent.

## Checks

```powershell
dotnet test tests/Restaurant.Tests/Restaurant.Tests.csproj
cd web
npm test
npm run build
cd ../mobile
npm run typecheck
npx expo export --platform android --output-dir dist
```

See `docs/API.md`, `docs/SCREENS.md`, `docs/database.sql`, and `tests/online-smoke.mjs`. The online smoke test requires a **dedicated test database** and running API; it creates test staff, tables, orders and payments. Do not run it against operational restaurant data.

## Slow and unreliable connections

See [LOW-NETWORK.md](docs/LOW-NETWORK.md) for saved drafts, cached screens, safe retry behavior, bandwidth improvements, and the limits of offline operation.
