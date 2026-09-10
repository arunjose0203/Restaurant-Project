# Screen designs

Shared design: deep green actions, white working surfaces, compact status labels, clear table numbers. Desktop has a persistent role sidebar, mobile web has a compact workspace bar, and native mobile uses stacked cards and touch-sized actions.

## Waiter / Supplier

Desktop: service counters → Take an order / My orders / Food ready tabs → table grid and searchable category-filtered menu on the left, current order and instructions on the right. Each item has add/decrease quantity controls. Submit returns an empty cart after success; errors preserve input. Ready tickets have a single Mark served action.

Mobile: table selection → searchable menu → cart and instructions → Send to kitchen. Separate My orders and Food ready tabs. Server notifications appear as in-app alerts and durable unread notices. API reconnection and foreground return refresh tickets.

## Kitchen

Desktop: three columns, New / Preparing / Ready. Tickets show table, order ID/time, quantity and special instructions. Buttons advance one state. Mobile: the same groups stacked vertically. Ready tickets wait for waiter collection.

## Reception / Cashier

Desktop: served-table list and recent payments beside a printable receipt with all order rounds. Select Cash/Card/UPI, optionally enter a reference, confirm payment after receipt. A visit with unserved items cannot be paid. Mobile: stacked bill cards with payment method controls and a confirmation dialog.

## Admin

Menu items, Categories, Tables, Users & roles, Payment methods, Daily sales, Order history. Create/edit records in a desktop modal or native inline editor. Deactivate records to retain historical links. Daily sales use the client-local day's UTC boundaries; order history is paginated. Fixed role assignment preserves workflow permissions.

## State handling

- Loading: workspace loading indicator.
- Empty: explains what will appear in each queue.
- Error: visible message; input preserved for correction.
- Pending: buttons disabled to reduce duplicate submissions.
- Disconnected: explicit connection status and retry/reconciliation.
- Demo: persistent banner stating that online PostgreSQL is not connected.

Tax configuration, payment gateways, stock management, cancellation/refunds, offline order entry, OS background push and printer hardware integration are outside this implementation. Printing is browser-based. No simulated payment is presented as a gateway charge.
