# Roadmap implementation

## First increment: web feature boundaries

Completed September 12, 2026:

- `web/src/main.tsx` now only mounts the app and registers the service worker.
- `web/src/App.tsx` owns authentication, shared workspace state, refresh coordination, pending submission recovery, navigation, and notifications.
- `web/src/features/waiter/` contains the waiter workspace and draft order entry.
- `web/src/features/kitchen/` contains the kitchen board.
- `web/src/features/cashier/` contains billing and payment confirmation.
- `web/src/features/admin/` contains administration and reports.
- `web/src/features/auth/` contains sign-in.
- `web/src/components/` contains shared order tickets, empty states, and statistics.
- `web/src/lib/` contains formatting helpers and the shared action callback type.

Screens receive state and actions through props. The app remains the owner of shared state; a new store dependency is not required for this extraction. Existing order acknowledgement, draft persistence, role visibility, payment confirmation, and offline behavior are preserved.

This increment changes code organization, not restaurant workflow. Existing component bodies were extracted without redesigning the interface. Mobile modularization and further extraction of state hooks remain pending.

## Next increments

1. Modularize the mobile role screens and connection hooks.
2. Implement receipt and KOT document models and printing adapters. Validate physical output against the restaurant's actual printer model and connection type.
3. Implement server-authoritative tax, discount authorization, and split-payment persistence with migrations and accounting tests.
4. Add device registration and background notifications, followed by KDS timers and alerts.

The remaining inventory, guest ordering, payment gateway, reporting, scaling, and multi-branch phases are not yet implemented. GitHub publication also remains pending a destination repository.
