# TarExtractor

Adds a buildable Tar Extractor that passively collects Tar from Plains tar pits.

## Example

The below screenshot demonstrates the Tar Extractor in action, showing it passively collecting Tar from a Plains tar pit.

![TarExtractor Example](https://github.com/javidahmed64592/Valheim-TarExtractor/raw/main/game_screenshot.png)

## How it works

The Tar Extractor works like the Sap Extractor, but for tar pits instead of Ancient Roots:

- Place it inside a tar pit (it also works on drained pits). Outside a tar pit the placement ghost turns red and a message tells you why.
- It produces **1 Tar every 60 seconds** and holds up to **200 Tar**.
- Interact with it to collect the stored Tar. It drops next to the extractor.
- Progress is saved on the piece, so it keeps filling up while you are away.

## Building it

Find it in the Hammer's **Crafting** tab. It needs a Workbench nearby and the same materials as the Sap Extractor:

| Material        | Amount |
| --------------- | ------ |
| Yggdrasil wood  | 10     |
| Black metal     | 5      |
| Dvergr extractor | 1     |

## Configuration

| Setting          | Default | Description                                       |
| ---------------- | ------- | ------------------------------------------------- |
| `Seconds Per Tar` | `60`   | Seconds it takes to produce 1 Tar.                |
| `Max Tar`         | `200`  | Maximum amount of Tar one extractor can hold.     |
