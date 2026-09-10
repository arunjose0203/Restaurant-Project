# Slow-network and interruption handling

Both clients now use the same tested request/recovery policy. This improves intermittent connections; it does not allow a disconnected device to notify another device instantly.

## Staff experience

- Table, quantities and special instructions are saved on the device by user and API origin. A draft survives screen changes and app restarts. If local storage fails, the UI warns staff to keep the screen open.
- Previously fetched operational screens remain visible while reconnection runs. The interface shows when information was last updated and offers Refresh status. Cached state is limited to 24 hours and is cleared on sign-out; drafts and unconfirmed submissions remain scoped to the same staff account.
- After one successful production web visit, a versioned service worker caches only the interface and its scripts/styles. It never caches API, payment, authentication or SignalR requests. New versions wait for existing tabs to close so an update does not interrupt service. First-time sign-in and the first data download need a connection.
- The external font request was removed, and touch controls were enlarged.

## Safe requests and recovery

- Requests have a 15-second deadline, including reading the response body. GET requests retry one transient failure; mutations are never silently retried.
- Before sending an order, its payload and a random client request ID are stored durably. After a timeout, staff use **Check order confirmation** to retry that exact payload and ID. A different order cannot overwrite an unconfirmed submission.
- The API uses the client request ID as the unique order primary key, checks staff ownership and normalized payload, and returns the existing order on a retry. This works even if the kitchen has advanced the order or the table has closed. No schema migration is required for this change.
- Status retries acknowledge an already-reached state without creating another notification or moving the order backwards. Existing role and waiter-ownership checks still apply. Payments retain their unique bill-per-table-session protection.
- A successful write is acknowledged immediately; the UI does not wait for another full snapshot download before acknowledging it. Screens update from server-confirmed data rather than optimistic paid/served states.
- SignalR bursts are debounced and snapshot fetches are coalesced. Fallback reconciliation runs every 90 seconds instead of 30 seconds and pauses when the app is hidden/backgrounded. Reconnects use bounded exponential backoff; foreground/online events trigger refresh.
- The API compresses operational state responses, while excluding token/authentication responses from compression.

Drafts are **not automatically sent** when connectivity returns. Staff review the table and send them explicitly. Cash/Card/UPI payments require server acknowledgement; the app never treats an offline payment as a closed bill. A lost payment response can be checked by refreshing status or retrying the same table session.

## Validation

Tests cover a hanging response body, transient read retry, no hidden write retries, durable request identity after a lost acknowledgement, storage failure before transmission, event-burst coalescing, normalized order retry matching, role-protected status acknowledgement and offline service-worker behavior. The worker test confirms that API and hub requests are not intercepted.

Online PostgreSQL integration and physical-device network throttling are still pending the online API/database setup. The private demo remains browser-local. No guarantee of live cross-device operation without connectivity is implied.

References: [MDN offline caching](https://developer.mozilla.org/en-US/docs/Web/Progressive_web_apps/Guides/Caching), [ASP.NET Core response compression](https://learn.microsoft.com/en-us/aspnet/core/performance/response-compression?view=aspnetcore-10.0).
