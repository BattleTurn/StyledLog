# StyledDebug (StyleLog)

StyledDebug is a styled logging toolkit for Unity with a custom Styled Console in the Editor: tag-based styles, rich text segments, clickable file:line links, and an inline code preview tooltip.

## Features

- Runtime logging API (BattleTurn.StyledLog.StyledDebug)
  - Tag-based styles via StyledLogManager and StyleSetting assets
  - Compose messages from StyledText segments (hex color, bold/underline/strikethrough, optional font)
  - Emits onEmit(tag, richText, logType, stack) for custom sinks

- Styled Console (Editor window)
  - Menu: Tools > StyledDebug > Styled Console
  - Filters (Log/Warning/Error), Collapse, Auto-scroll, Search
  - Table-style log view: resizable columns (Icon, Type, Tag, Message) with clean vertical/horizontal dividers
  - Selectable message area: users can select and copy text in the message pane (supports drag selection)
  - Instant message update: message area updates immediately when a log is selected, with selection highlight reset
  - Improved compiler message trimming: compiler errors/warnings show only the actual message text (prefix removed)
  - Tag filter dropdown in header: “Everything” toggle and per-tag flags
  - Tag filter dropdown in header: “Everything” toggle and per-tag flags
    - “Everything” ON: shows all tags (including new ones); OFF: shows none until you pick tags
    - Your selections persist per project and reload when opening the dropdown
  - Clickable stack links open code at file:line
  - Code tooltip on hover: anchored near the link, single-instance, no flicker, DPI-aware, with highlighted line
  - Full message pane above the stack trace (rich text) with its own scrollbar
  - Draggable splitter between message and stack traces; both sections keep a minimum height so nothing is hidden
  - Clear options persisted: Clear on Play, Clear on Build, Clear on Recompile
  - Snapshot logs across domain reloads when “Clear on Recompile” is off
  - Captures Unity Editor logs automatically and shows them in Styled Console; Exceptions are normalized to Error

## Installation

- As a local package: Unity Package Manager > Add package from disk… select `Packages/colorful-log/package.json`
- Or add from git if you host it as a UPM repo (path must point to the package folder)

## Setup

1) Create StyleSetting assets for your tags (Create > StyledDebug > Style)
   - Configure: Tag, Enabled, Hex Color, Text Style, and optional Font/TMP Font

2) Create a StyledLogManager asset in a Resources folder, assign your StyleSetting entries
   - Name the asset “StyledLogManager” so it loads automatically: `Resources/StyledLogManager`

## Quick start

```csharp
using BattleTurn.StyledLog;
using UnityEngine;

// Simple message with tag style
StyledDebug.Log("Combat", "Critical hit!");

// Styled segments (hex + style)
var heal = new StyledText("+25 HP", "#57FF57", TextStyle.Bold);
var left = new StyledText(" (left: 75)", "#CCCCCC");
StyledDebug.Log("Stats", heal, left);

// Warnings and errors
StyledDebug.LogWarning("Net", new StyledText("Packet delayed", "#FFCC00"));
StyledDebug.LogError("UI", new StyledText("Null ref", "#FF5555", TextStyle.Bold));
```

Styled Console subscribes to `StyledDebug.onEmit` automatically. Open it via Tools > StyledDebug > Styled Console.

## StyledText cheat sheet

```csharp
// text only (inherits tag style color/font if present)
new StyledText("Hello");

// text with hex color + style flags (Bold, Underline, Strikethrough)
new StyledText("Hi", "#00D1FF", TextStyle.Bold | TextStyle.Underline);

// text from a StyleSetting (uses its hexColor/style/font/TMP font)
StyleSetting s = /* from StyledLogManager["Tag"] */ null;
new StyledText("From setting", s);
```

## Styled Console usage

- Filter by type, toggle Collapse/Auto-scroll, drag splitters to resize columns and the stack pane
- Use the Tag dropdown in the header to pick which tags are visible
  - “Everything” shows all tags; toggling individual tags creates an explicit selection set
  - Your tag selections are remembered per project
- Click a stack link to open it; hover to see an inline code preview with the target line highlighted
  - Read the full rich-text message in the pane above the stack trace; scroll if it’s long
  - Select and copy text from the message area (click and drag to select)
  - When you select a new log, the message area updates instantly and clears any previous selection highlight
- Use the toolbar dropdown to toggle: Clear on Play, Clear on Build, Clear on Recompile
- When “Clear on Recompile” is off, logs are snapshotted and restored across script recompiles

### Unity logs

Styled Console subscribes to Unity’s `Application.logMessageReceived` in the Editor and mirrors those entries. Messages originating from StyledDebug are not duplicated. Unity `Exception` entries are displayed as `Error` for consistent filtering.

### Compiler diagnostics (C# script / assembly errors)

When the C# compiler produces messages, they are surfaced in Styled Console under the special tag `Compiler`:

- Custom compiler icon (assembly/code themed) with a tiny severity badge (error / warning / info) overlaid bottom‑right.
- Live sync: existing compiler messages are re-synced whenever the window enables (no need to press a button).
  - Message trimming: the leading `Path/File.cs(line,col): severity:` prefix is collapsed in the upper message panel so you focus on the actual text; the original path and line remain accessible through the stack frame / inline link.
  - Compiler messages now always show only the actual error/warning text (no unwanted type prefix)
- Double‑click the row or the highlighted link to open the file at the reported line.

You can also emit simulated compiler messages (see Tester window section) to visually test styles without forcing a recompile.

### Inline file link detection in message body

If a log message itself contains the first occurrence of a relative project path like `Assets/.../SomeFile.cs(123)` (or `Packages/...`), and there is no stack trace, Styled Console parses just that first path occurrence and turns it into a clickable link (green underline). Double‑click (or single click if you customize) opens the file at the detected line. Long tokens are soft‑wrapped to prevent horizontal scroll overflow.

### Tester window & benchmarking

Open via: `Tools > StyledDebug > Tester`.

Provides a focused environment to:

- Preview a `StyleSetting` (resolved color, font(s), style flags) and its generated rich text.
- Log / Warning / Error directly with the chosen style.
- Run a micro benchmark ("Benchmark x1000") to measure mean construction cost (time, GC delta, output length) for a given styled message.
- Emit simulated compiler diagnostics (Info / Warning / Error) using the canonical pattern `Assets/Tests/CompilerTest.cs(line,col): severity: message` to verify:
  - Compiler tag filtering
  - Icon + severity badge overlay
  - Message trimming behavior
  - Inline path link detection

Example (benchmark usage in code):

```csharp
var r = StyledDebugBenchmark.RunMany("Net", 1000, new StyledText("Ping ok", someStyleSetting));
Debug.Log($"avg: {r.milliseconds:F3} ms, bytes={r.bytes}, outLen={r.outputLength}");
```

## Extending (optional)

Listen to logs for custom sinks (file, UI, network):

```csharp
using BattleTurn.StyledLog;
using UnityEngine;

[UnityEditor.InitializeOnLoad]
public static class StyledSink
{
    static StyledSink()
    {
        StyledDebug.onEmit -= OnEmit;
        StyledDebug.onEmit += OnEmit;
    }

    private static void OnEmit(string tag, string rich, LogType type, string stack)
    {
        // Write to a file, send to server, mirror in a TMP UI, etc.
    }
}
```

## Notes

- Styled Console strips `<font>` for IMGUI but keeps fonts for rich sinks (onEmit)
- If tooltip placement looks off on unusual DPI setups, recompile/reopen the window
 - Tag visibility selections are stored in EditorPrefs per project; snapshots of logs (when enabled) are stored in SessionState

## TODOs

- Simple UI for build-time debugging (easy to toggle/use)