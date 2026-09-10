# Validation record

Verified in this workspace on 10 September 2026:

- .NET 10 API build: passed, zero warnings/errors.
- .NET role/transition tests: 11 passed.
- Web workflow tests: 2 passed, covering combined rounds, immutable price snapshots, skipped transitions, owner checks, premature payment rejection, duplicate payment and table reopening.
- React TypeScript check and Vite production build: passed.
- React Native TypeScript check: passed.
- Expo Android and iOS JavaScript / Hermes bundle export: passed. This is not a signed APK/IPA build or physical-device test.
- Dependency audit: web and mobile reported zero known vulnerabilities after pinning Xcode tooling's UUID dependency to a compatible patched major. Xcode uses `uuid.v4()`, supported by this override.
- EF Core generated two PostgreSQL migrations and an idempotent SQL script.
- Local web preview HTTP response: 200.

Not yet verified:

- Real PostgreSQL migration or API execution, because no online connection string was supplied.
- Cross-device SignalR delivery, transactional database concurrency, real login round trips, and online end-to-end flow. `tests/online-smoke.mjs` is supplied for these checks against a dedicated test database.
- Browser interaction or visual testing, physical mobile devices, signed application packages and store distribution.
- Payment gateway settlement and OS background push, which are not implemented.

The deployed web demo is browser-local and explicitly labeled. It does not demonstrate a live database connection.

Implementation references: [Npgsql EF Core 10](https://www.npgsql.org/efcore/release-notes/10.0.html), [ASP.NET Core SignalR authorization](https://learn.microsoft.com/en-us/aspnet/core/signalr/authn-and-authz?view=aspnetcore-10.0), [Expo SDK 55](https://expo.dev/changelog/sdk-55).
