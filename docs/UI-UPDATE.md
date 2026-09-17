# Interface update — September 2026

Restored deleted tracked project files from the latest Git commit before updating both applications.

## Web
- Forest-green navigation, warm surfaces, clearer page hierarchy, and responsive phone navigation.
- Focused pages for orders, kitchen, payments, management, floor plan, and settings.
- Searchable menu, direct add for simple dishes, accessible customization dialogs, and a dedicated order summary.
- Table status cards, room-layout toggle, and confirmation before table transfers or voiding orders.
- Structured tax, portion, and modifier forms instead of raw JSON controls.

## Mobile
- Consistent colors, larger touch controls, role navigation, and a separate staff-tools page.
- Three-step ordering: table, menu, and review, with saved drafts and a customization sheet.
- Kitchen status tabs and clear empty states.

## Verification
- Web production build passed.
- Eight workflow/network tests and one offline service-worker test passed.
- Mobile TypeScript check and Android JavaScript/Hermes export passed.
- Browser checked at phone, tablet, and desktop widths; exercised navigation, search, adding/removing a draft item, customization dialog, and tax form display.
- Native device visual testing and rebuilding an installable APK remain outstanding. This update does not certify completion of every product-roadmap integration.
