# A81 WinUI tertiary theme brush source

- Checked: 2026-10-10
- Official source: [Theming in Windows apps](https://learn.microsoft.com/en-us/windows/apps/develop/ui/theming)
- Relevant guidance: WinUI's theme-brush example uses `TextFillColorTertiaryBrush` for lower-emphasis text in a themed item template. The brush is a system theme resource, so it follows the active Windows theme.
- Application: use this brush for the two language-menu chevrons and the “下次录音生效” hint, matching SwiftUI `.tertiary` emphasis while preserving `.secondary` for language labels and transcript metadata.
