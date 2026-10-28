<p align="center">
  <img src="Resources/AppIcon.png" width="128" alt="Ferry icon">
</p>

<h1 align="center">Ferry</h1>

<p align="center">A tiny macOS menu bar app that carries files between desktops.</p>

---

Moving a file to a window on another desktop (Space) usually means juggling
Mission Control mid-drag. Ferry gives you a small floating shelf instead: drop
files on it, switch desktops, and drag them out where they belong.

## How it works

1. Start dragging files in Finder. The shelf slides in at the right edge of the screen.
2. Drop them on the shelf.
3. Switch to another desktop (<kbd>⌃</kbd> <kbd>←</kbd> / <kbd>→</kbd> or a swipe). The shelf comes along.
4. Drag the pile out to its destination. Finder moves the files on the same disk
   and copies them to another disk; hold <kbd>⌥</kbd> to force a copy.

Need the files in different places? Click the pile (or the list button) to see
each file. Drag a single row out on its own, or tick a few with the circles
(<kbd>⌘</kbd>-click and <kbd>⇧</kbd>-click work too) and drag them together.
The rest stay on the shelf for the next stop.

Ferry only keeps references. Files stay where they are until you drag them out,
and they leave the shelf as soon as they land somewhere else.

## Features

- Lives in the menu bar, not the Dock. The ferry icon fills in and shows a count
  when files are waiting.
- The shelf appears on every desktop and over full-screen apps, without ever
  stealing focus from the app you're working in.
- Liquid Glass on macOS 26 and later, a vibrant blur on macOS 14–15.
- Quick Look thumbnails, fanned out like a small pile.
- A file list for picking which files to drag: single rows, ticked files or
  Finder-style ⌘/⇧ selection. Double-click a row to open the file.
- Right-click to open files, show them in Finder, remove them from the shelf or
  clear it.
- Drag the shelf by its edge to put it anywhere; Ferry remembers the spot.
- Menu bar menu: show or hide the shelf, clear it, turn off "Show While Dragging
  Files", launch at login.

Ferry needs no permissions: it watches for drags with a global mouse monitor,
which doesn't require Accessibility access.

## Build and run

Requires macOS 14 or later and Xcode 16 or later (Swift 6).

```bash
make run        # build build/Ferry.app and launch it
make install    # copy it to /Applications and launch it
make app        # just build build/Ferry.app
swift test      # run the unit tests
```

The app is ad-hoc signed for local use. Install it to `/Applications` before
turning on **Launch at Login**.

To work on it in Xcode, open `Package.swift`.

> **Note:** if the repository lives in an iCloud-synced folder (Desktop &
> Documents), iCloud tags build products with Finder info and `codesign` refuses
> to sign the test bundle. Build outside the synced folder, for example
> `swift test --scratch-path /tmp/ferry-build`. `make app` already clears these
> attributes.

## Project layout

| Path | What's inside |
| --- | --- |
| `Sources/Ferry/FerryApp.swift` | Entry point, runs as a menu bar–only app |
| `Sources/Ferry/Shelf.swift` | The shelf model: parked files and their metadata |
| `Sources/Ferry/ShelfPanel.swift` | Floating, non-activating panel on every Space |
| `Sources/Ferry/ShelfPanelController.swift` | Showing, hiding and positioning the panel |
| `Sources/Ferry/ShelfView.swift` | The SwiftUI shelf: drop zone, pile, file list, footer |
| `Sources/Ferry/FileDragArea.swift` | AppKit drop target and multi-file drag source |
| `Sources/Ferry/FileSelection.swift` | Finder-style selection for the file list |
| `Sources/Ferry/DragMonitor.swift` | Detects file drags in other apps |
| `Sources/Ferry/StatusItemController.swift` | Menu bar icon and menu |
| `scripts/build-app.sh` | Assembles and signs `Ferry.app` |
| `scripts/make-icon.swift` | Draws the app icon (`swift scripts/make-icon.swift`) |

## License

MIT, see [LICENSE](LICENSE).
