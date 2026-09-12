# Tableflow Roadmap Implementation Progress

## Implementation Status: Complete Across All Phases (Phases 1–5)

Completed September 12, 2026:

### Phase 1: Core POS & Hardware Integration
- **ESC/POS Thermal Printing**:
  - Native Android Bluetooth module with RFCOMM socket communication (`ThermalPrinterPackage.kt` via `mobile/plugins/with-thermal-printer.js`).
  - Mobile waiter/cashier printing interface in `mobile/features/PrinterTools.tsx` supporting 58mm (32-col) and 80mm (48-col) KOT and guest check formats.
  - Server endpoints in `server/Restaurant.Api/IntegrationEndpoints.cs` (`/api/pos/print/kot/{id}` and `/api/pos/print/bill/{id}`) with raw byte ESC/POS command generation.
  - Browser web printing adapter in `web/src/features/pos/PrintButton.tsx`.
- **Background Push Notifications**:
  - `expo-notifications` integration in `mobile/hooks/usePushNotifications.ts` registering push tokens with `/api/notifications/register`.
  - Server-side `DeviceToken` entity and background notification dispatching via `OutboxWorker.cs`.
- **Tax & Discount Calculation Engine**:
  - Server-authoritative ledger and quotation service in `server/Restaurant.Api/PosService.cs`.
  - Configurable tax rules (e.g. CGST, SGST, VAT) applied after discounts and service charges.
  - Manager-authorized percent and flat discount rules in `server/Restaurant.Api/PosEndpoints.cs` (`/pos/admin/sessions/{id}/discount`).
- **Split Billing & Multi-Tender Reconciliation**:
  - Split options (Equal, Item, Custom) in `PosEndpoints.cs` (`/pos/sessions/{id}/split`).
  - Partial payments ledger with idempotency via `requestId` until balance is settled.
  - Dynamic payment UI in `web/src/features/cashier/PosBilling.tsx` and `mobile/features/PosBilling.tsx`.

### Phase 2: Architecture Refactoring & Frontend UX
- **Web Modularization**:
  - Extracted from monolithic `main.tsx` into domain features:
    - `web/src/features/waiter/`: WaiterScreen, OrderEntry, AdvancedOrderEntry.
    - `web/src/features/kitchen/`: KitchenScreen.
    - `web/src/features/cashier/`: Billing, PosBilling.
    - `web/src/features/admin/`: Admin, PosAdmin.
    - `web/src/features/pos/`: FloorMap, MenuPicker, GuestPortal, QrCode, PrintButton, AccountTools.
    - `web/src/components/`: Empty, Orders, Stat.
- **Mobile Modularization**:
  - Extracted from monolithic `App.tsx` into modular feature components:
    - `mobile/features/WaiterScreen.tsx`: Order flow & own tickets.
    - `mobile/features/KitchenScreen.tsx`: Ticket progression board.
    - `mobile/features/CashierScreen.tsx`: POS billing & split payment.
    - `mobile/features/AdminScreen.tsx`: Administration, sales, and order history.
    - `mobile/features/OrderComposer.tsx`: Offline draft composer with portion sizes and modifier groups.
    - `mobile/features/OrderTicket.tsx`: Individual ticket card with SLA styling.
    - `mobile/features/StaffTools.tsx`: Branch switcher, 4-digit PIN unlock, table transfer/merge, and guest request resolution.
    - `mobile/features/PrinterTools.tsx`: Bluetooth thermal printer discovery and printing.
    - `mobile/hooks/useWorkspace.tsx`: Centralized workspace state, sync, and network resiliency.
- **Kitchen Display System (KDS) SLA Timers & Audio Cues**:
  - Dynamic elapsed time calculations in `shared/kitchen.mjs` (`normal` < 10m, `warning` 10–15m, `delayed` > 15m).
  - Visual color badges on web (`ticket sla-*`) and mobile border indicators.
  - Web Audio synthesizer chimes for new incoming kitchen tickets in `KitchenScreen.tsx`.
- **Interactive Floor Map & Table Transfers**:
  - Section-filtered interactive table grid in `FloorMap.tsx` with color-coded live states (Vacant, Occupied, Food Ready, Bill Requested).
  - Atomic table transfer and table merge endpoints in `PosEndpoints.cs` (`/pos/sessions/{id}/transfer`).
- **Staff Quick-Switch (PIN Unlock)**:
  - 4-digit PIN setup and login endpoints in `server/Restaurant.Api/IdentityEndpoints.cs` (`/auth/pin` and `/auth/pin-login`).
  - Terminal quick-switch in `AccountTools.tsx` and `StaffTools.tsx`.

### Phase 3: Menu Customization & Inventory Controls
- **Modifiers, Sizes & Add-on Groups**:
  - `MenuItem` model enriched with `PortionsJson` and `ModifiersJson`.
  - Line-level portion and modifier tracking in `OrderComposer.tsx` and `AdvancedOrderEntry.tsx`.
- **Real-Time 86ing & Stock Counts**:
  - Quick availability toggle and stock decrement in `PosEndpoints.cs` (`/pos/menu/{id}/availability`).
  - Kitchen stock control panel in `KitchenScreen.tsx`.
- **Order Item Void & Waste Tracking**:
  - Supervisor-authorized order voiding with reason codes in `PosEndpoints.cs` (`/pos/admin/orders/{id}/void`).
  - Comprehensive immutable audit trail stored in `AuditLog` table.

### Phase 4: Customer-Facing & Digital Ordering
- **Dine-in QR Self-Ordering Portal**:
  - Direct guest ordering portal at `/t/{tableToken}` in `web/src/features/pos/GuestPortal.tsx`.
  - Cryptographically secure table tokens stored in `DiningTable.GuestToken`.
  - Digital menu browsing, dietary tags, draft persistence, and idempotent order submission via `sendOrder`.
- **Guest Assistance & Feedback**:
  - "Call Waiter" and "Request Bill" guest endpoints in `server/Restaurant.Api/GuestEndpoints.cs`.
  - Dynamic UPI QR code generation (`upi://pay?pa=...`) for instant guest bill settlement.
  - Post-dining 5-star rating and comment feedback submission in `Feedback` table.

### Phase 5: Scalability, Analytics & Multi-Location
- **Reliable Event Dispatching (Outbox Pattern)**:
  - Background `OutboxWorker` service in `server/Restaurant.Api/OutboxWorker.cs` continuously delivering events and notifications with PostgreSQL `SKIP LOCKED` concurrency.
- **End-of-Day (Z-Report) & Business Intelligence**:
  - Comprehensive sales report endpoint `/api/pos/admin/report` aggregating gross/net sales, discounts, taxes, service charges, tips, payment method breakdown, dish popularity, preparation times, and hourly heatmaps.
  - Accounting-ready CSV export (`/pos/admin/report.csv`) and print/PDF formatting in `PosAdmin.tsx`.
- **Multi-Branch Isolation & Enterprise Management**:
  - `Branch` entity and EF Core global query filters scoping menu, tables, sessions, and orders per branch.
  - Owner endpoints in `server/Restaurant.Api/IdentityEndpoints.cs` to create branches, assign staff roles across branches, and replicate catalog items across locations.

---

## Verification & Test Results
- **.NET Backend Tests**: 17 / 17 passed (`dotnet test tests/Restaurant.Tests/Restaurant.Tests.csproj`).
- **POS End-to-End Test**: 1 / 1 suite passed (`node tests/pos-api.test.mjs` validating migrations, customizations, ledger, audit, guest portals, and branch isolation).
- **Web Frontend Build & Tests**: Build succeeded, 8 / 8 unit tests passed (`npm test` in `web`).
- **Mobile Typecheck & Tests**: 0 TypeScript errors (`npm run typecheck` in `mobile`), 7 / 7 shared network tests passed.
- **Mobile Android Bundle Export**: Succeeded (`npx expo export --platform android`).
