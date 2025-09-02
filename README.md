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
  - Resizable columns: Type, Tag, Message
  - Clickable stack links open code at file:line
  - Code tooltip on hover: anchored near the link, single-instance, no flicker, DPI-aware, with highlighted line
  - Clear options persisted: Clear on Play, Clear on Build, Clear on Recompile
  - Snapshot logs across domain reloads when “Clear on Recompile” is off

## Installation

- As a local package: Unity Package Manager > Add package from disk… select `Packages/StyleLog/package.json`
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
- Click a stack link to open it; hover to see an inline code preview with the target line highlighted
- Use the toolbar dropdown to toggle: Clear on Play, Clear on Build, Clear on Recompile
- When “Clear on Recompile” is off, logs are snapshotted and restored across script recompiles

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

## TODOs

- Capture normal Unity logs (Debug.Log/Warning/Error) into Styled Console
- Tag selector to view only specific tags
- Simple UI for build-time debugging (easy to toggle/use)