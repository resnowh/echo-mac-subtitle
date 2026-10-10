# Toolbar icon glyph source

Source: [Microsoft Learn — Segoe MDL2 Assets icons](https://learn.microsoft.com/en-us/windows/apps/design/iconography/segoe-ui-symbol-font)

Looked up on 2026-10-10 while checking the toolbar glyph choices. The page identifies Segoe MDL2 Assets as a system font available on Windows and documents these glyph mappings used by the toolbar:

| Code point | Documented glyph | Use in Echo |
|---|---|---|
| `E76F` | GripperBarHorizontal | Drag handle |
| `E72E` | Lock | Locked state |
| `E785` | Unlock | Unlocked state |
| `E962` | Mouse | Click-through enabled |
| `E8B0` | Click | Click-through disabled |
| `E713` | Setting | Open settings |
| `E8BB` | ChromeClose | Close overlay/settings |

The earlier draft mistakenly used `E7C1` (Flag) as the drag handle and `E774` (Globe) as the mouse state; both were corrected before commit. The app targets Windows 10 as its minimum OS, so the existing MDL2 system font is used for compatibility.
