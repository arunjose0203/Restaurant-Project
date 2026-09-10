# API and real-time contract

Base URL: `/api`. JSON uses camelCase. Protected endpoints require `Authorization: Bearer <token>`. Invalid input returns 400, invalid credentials 401, insufficient role 403, missing record 404, conflicting state 409. Responses include a `message` where available. Login is limited to ten attempts per minute per remote IP; when deploying behind a proxy, configure trusted proxy forwarding if per-client IP limits are needed.

| Method | Path | Access | Purpose |
|---|---|---|---|
| POST | `/auth/login` | Public | `{email,password}` → `{token,expires,user}` |
| GET | `/state` | Signed in | Operational menu, tables, sessions, role-filtered orders, own notifications; admin users and cashier/admin bills |
| POST | `/orders` | Waiter, Admin | `{tableId,items:[{menuItemId,quantity}],instructions}`; server resolves prices and open visit |
| PATCH | `/orders/{id}/status` | Kitchen, Waiter, Admin | `{status}`; legal transition and ownership enforced |
| POST | `/notifications/{id}/read` | Recipient | Mark own notification read |
| GET | `/sessions/{id}/bill` | Cashier, Admin | Full table visit, all orders, authoritative total, canPay and existing payment |
| POST | `/sessions/{id}/pay` | Cashier, Admin | `{paymentMethodId,reference,expectedTotal}`; atomically record paid bill and close visit |
| PUT | `/admin/menu/{id}` | Admin | Menu item create/update, including categoryId, price, active, vegetarian |
| PUT | `/admin/categories/{id}` | Admin | Category name create/update |
| PUT | `/admin/tables/{id}` | Admin | Table name, seats, active |
| PUT | `/admin/paymentMethods/{id}` | Admin | Method name, active |
| PUT | `/admin/users/{id}` | Admin | Name, email, role, password, active; blank password preserves current hash |
| GET | `/admin/sales?from=...&to=...` | Admin | Half-open UTC timestamp range; total, count and payment-method breakdown |
| GET | `/admin/history?page=1` | Admin | 50 newest orders per page with lines and price snapshots |
| GET | `/health` | Public | Process liveness (not a database readiness probe) |

Use `0` to create numeric-ID records; `00000000-0000-0000-0000-000000000000` to create users. All monetary values use decimal arithmetic on the server and PostgreSQL numeric(12,2). Timestamps are UTC and rendered locally by clients. Table payment totals are rechecked under lock.

SignalR hub: `/hubs/orders`, authenticated with the same bearer token. Query access tokens are accepted only on this hub path for browser WebSockets/SSE. Configure infrastructure to redact query strings from hub access logs. Hub connections close on token expiration.

- `StateChanged`: invalidation only; clients fetch their authorized state.
- `FoodReady`: notification addressed to the placing waiter's user ID, with id, userId, orderId, message, createdAt, read.

Notifications persist in PostgreSQL in the same transaction as Ready. Clients recover on reconnect and every 90 seconds while visible. Event transport is best-effort; there is no transactional outbox. For multi-instance scale, add a SignalR backplane and an outbox dispatcher if guaranteed low-latency retries are required.

Database tables: Users, Categories, MenuItems, Tables, PaymentMethods, Sessions, Orders, OrderItems, Bills, Notifications, OrderEvents. Entity Framework migrations include foreign keys, unique email/category/table/method names, one open session per table, one bill per session, decimal precision and legal state/role/quantity constraints. `docs/database.sql` is generated from the same migrations.

Security boundaries: no password hashes leave API responses; PBKDF2 password hashing via ASP.NET Identity; exact CORS allowlist; JWT signature/issuer/audience/lifetime checks; active-role validation per request; no automatic schema modifications at application startup. Record archived history rather than hard-deleting referenced data.

Order creation also accepts clientRequestId (UUID). Reuse the same ID and payload after a lost response. The server returns the existing order for matching retries and rejects changed payloads. Status retries acknowledge an already-reached stage without moving backwards. See LOW-NETWORK.md.
