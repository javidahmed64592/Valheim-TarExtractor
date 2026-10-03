# Changelog

## 0.1.1

- Fixed method signature for Player.PlacePiece patch.

## 0.1.0

- New buildable piece, the **Tar Extractor**, available from the Hammer's Crafting tab.
  - Built at a Workbench using the same materials as the Sap Extractor: 10 Yggdrasil wood, 5 Black metal and 1 Dvergr extractor.
  - Reuses the Sap Extractor model with a darker, tar-like tint.
- Passive Tar collection: produces 1 Tar every 60 seconds and holds a maximum of 200 Tar.
  - Progress is saved on the piece, so it keeps accumulating while you are away.
  - Interact with the extractor to collect the stored Tar, which is dropped next to it.
  - Hover text shows how much Tar is currently stored.
- Placement restriction: the Tar Extractor can only be placed inside a tar pit.
  - The placement ghost turns red outside a tar pit.
  - A message is shown if you try to place it somewhere invalid.
- Admin-synced configuration options for `Seconds Per Tar` (default `60`) and `Max Tar` (default `200`).
