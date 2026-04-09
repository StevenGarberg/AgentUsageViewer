# AgentUsageViewer — Implementation Plan

A lightweight, cross-platform (Windows + macOS) desktop overlay built in .NET that
tracks token usage and cost for **Claude Code** and **OpenAI Codex CLI** by parsing
the JSONL session logs each tool already writes to disk. No API keys, no network,
no hooks into the agents themselves — purely a passive reader.

---

## 1. Goals & Constraints

**Must:**
- Run on Windows 11 and macOS as a native .NET app (single codebase).
- Display a small, always-on-top overlay window (compact, draggable, low-opacity friendly).
- Show live usage for Claude Code + Codex CLI (today / 7d / 30d / all-time).
- Cold-start to visible UI in well under a second; idle RAM target < 80 MB.
- Zero network calls. Read-only against the user's home directory.

**Nice to have:**
- Cost estimates from a local pricing table (editable JSON).
- Per-project / per-model breakdown.
- Click-through opacity mode.
- System tray icon to show/hide overlay.

**Non-goals (v1):**
- No historical database — recompute from JSONL on demand. Cache in memory only.
- No auth, no sync, no telemetry.
- No editing or writing to agent state — strictly read-only.

---

## 2. Technology Choices

| Concern              | Choice                         | Why |
|----------------------|--------------------------------|-----|
| Framework            | **.NET 9**                     | Latest LTS-track, fast startup, NativeAOT-friendly. |
| UI                   | **Avalonia UI 11**             | Only mature .NET option that's truly cross-platform desktop (Win/macOS/Linux), supports transparent/borderless/topmost windows, small footprint. WPF is Windows-only; MAUI desktop is awkward for overlays. |
| Packaging (Win)      | Self-contained single-file exe | No installer required for v1. |
| Packaging (macOS)    | `.app` bundle via `dotnet publish -r osx-arm64` (+ `osx-x64`) | Universal-ish; sign ad-hoc for personal use. |
| JSON parsing         | `System.Text.Json` (streaming `Utf8JsonReader`) | Avoid allocating per-line; JSONL files can grow to many MB. |
| File watching        | `FileSystemWatcher`            | Cross-platform in .NET; debounce events. |
| MVVM                 | CommunityToolkit.Mvvm          | Source-generated observable props, tiny. |
| Tray icon            | Avalonia `TrayIcon`            | Built-in, works on both OSes. |

**Rejected:** Electron (heavy, not .NET), WPF (Windows-only), WinUI 3 (Windows-only),
MAUI (poor desktop story for overlays/transparency), Tauri (not .NET).

---

## 3. Data Sources

Both CLIs already record everything we need locally. We just read it.

### 3.1 Claude Code

- **Path:** `~/.claude/projects/<encoded-cwd>/<sessionId>.jsonl`
  - On Windows: `C:\Users\<user>\.claude\projects\...`
  - The `<encoded-cwd>` folder name is the cwd with separators replaced by `-`
    (e.g. `C--Users-Steven-Repos-AgentUsageViewer`). Useful for per-project grouping.
- **Format:** one JSON object per line. Lines we care about have `type == "assistant"`
  and contain a `message.usage` object:

  ```json
  {
    "type": "assistant",
    "timestamp": "2026-04-03T04:36:24.868Z",
    "sessionId": "...",
    "cwd": "C:\\Users\\Steven\\Repos\\FP3D",
    "version": "2.1.87",
    "message": {
      "model": "claude-opus-4-6",
      "usage": {
        "input_tokens": 3,
        "cache_creation_input_tokens": 40467,
        "cache_read_input_tokens": 7863,
        "output_tokens": 2,
        "service_tier": "standard"
      }
    }
  }
  ```

- **Fields to extract per assistant turn:** `timestamp`, `sessionId`, `cwd`, `model`,
  and the four token counts (input, cache_creation, cache_read, output).
- **Dedup key:** `(sessionId, message.id)` if present, otherwise `(sessionId, timestamp)`.
  Important because Claude Code can resume sessions and append.
- **Other line types** (`queue-operation`, `user`, `tool_use`, etc.) — skip cheaply
  by checking the `type` field first before fully parsing.

### 3.2 Codex CLI

- **Path:** `~/.codex/sessions/<YYYY>/<MM>/<DD>/rollout-<timestamp>-<id>.jsonl`
- **Format:** one JSON object per line. The lines we care about:

  ```json
  {
    "timestamp": "2026-03-14T04:00:21.497Z",
    "type": "event_msg",
    "payload": {
      "type": "token_count",
      "info": {
        "total_token_usage": {
          "input_tokens": 10139,
          "cached_input_tokens": 7040,
          "output_tokens": 278,
          "reasoning_output_tokens": 26,
          "total_tokens": 10417
        },
        "last_token_usage": { "...": "..." },
        "model_context_window": 258400
      }
    }
  }
  ```

- **Important:** `total_token_usage` is **cumulative for the session**, not delta.
  Strategy: per session file, take the **max `total_tokens`** (or the last
  `token_count` event) as that session's total. Do **not** sum across `token_count`
  events within a single file or you'll multi-count.
- **Model / cwd:** capture from the first `turn_context` line in the file
  (`payload.model`, `payload.cwd`).
- **Session id:** parse from filename (`019cea80-...`) or take from `turn_id` prefix.

### 3.3 Pricing

Ship a small editable file `pricing.json`:

```json
{
  "claude-opus-4-6":   { "input": 15.0, "cache_write": 18.75, "cache_read": 1.50, "output": 75.0 },
  "claude-sonnet-4-6": { "input": 3.0,  "cache_write": 3.75,  "cache_read": 0.30, "output": 15.0 },
  "claude-haiku-4-5":  { "input": 1.0,  "cache_write": 1.25,  "cache_read": 0.10, "output": 5.0  },
  "gpt-5.4":           { "input": 1.25, "cached_input": 0.125, "output": 10.0 }
}
```

Prices are per **1M tokens**. Unknown models → cost shown as `—` rather than guessed.
User can edit this file; the app reloads on file change.

---

## 4. Architecture

```
AgentUsageViewer.sln
├─ src/
│  ├─ AgentUsageViewer.Core/          # netstandard2.1 / net9.0, no UI deps
│  │   ├─ Models/        UsageRecord, SessionSummary, Totals, ModelKey
│  │   ├─ Parsers/       ClaudeJsonlParser, CodexJsonlParser   (streaming)
│  │   ├─ Sources/       ClaudeUsageSource, CodexUsageSource   (dir scan + watch)
│  │   ├─ Pricing/       PricingTable, CostCalculator
│  │   └─ Aggregation/   UsageAggregator (today/7d/30d/all, by model, by project)
│  ├─ AgentUsageViewer.App/           # Avalonia desktop app
│  │   ├─ Views/         OverlayWindow.axaml, SettingsWindow.axaml
│  │   ├─ ViewModels/    OverlayViewModel, SettingsViewModel
│  │   ├─ Services/      TrayService, HotkeyService (optional)
│  │   └─ App.axaml, Program.cs
│  └─ AgentUsageViewer.Tests/         # xUnit, parser fixtures from real jsonl samples
└─ samples/                           # tiny anonymized jsonl fixtures for tests
```

### 4.1 Core flow

1. On startup, each `*UsageSource` enumerates its root directory once.
2. For each `.jsonl` file, parser streams lines, yields `UsageRecord`s.
3. Records are stored in an in-memory list per source, keyed for dedup.
4. `FileSystemWatcher` (recursive) on each root → on `Changed`/`Created`,
   debounce 500 ms, then incrementally read **only the new bytes** of changed files
   (track per-file `(length, lastWriteUtc)`); re-scan from scratch if a file shrank.
5. `UsageAggregator` recomputes rollups from the in-memory record list. Cheap —
   we're talking thousands of records, not millions.
6. ViewModel exposes `ObservableProperty` totals; UI binds.

### 4.2 Incremental read detail

```
struct FileCursor { long Length; DateTime LastWriteUtc; }

ReadNew(file):
    cursor = cursors[file]   // default 0
    fi = new FileInfo(file)
    if fi.Length < cursor.Length: cursor = default   // truncated/rotated
    open Read+ShareReadWrite
    seek cursor.Length
    stream remaining lines → parser
    cursors[file] = { fi.Length, fi.LastWriteUtcNow }
```

Critical: **open with `FileShare.ReadWrite | FileShare.Delete`** so we never lock
files the agents are actively writing.

### 4.3 Threading

- All file IO and parsing on a background `Channel<FileChangeEvent>` consumer.
- Aggregation runs on the background thread; only the final `Totals` snapshot is
  marshalled to the UI thread via `Dispatcher.UIThread.Post`.
- No locks on the hot UI path.

---

## 5. UI / Overlay Design

### 5.1 Window

- `Window` with: `SystemDecorations="None"`, `TransparencyLevelHint="AcrylicBlur,Transparent"`,
  `Topmost="True"`, `ShowInTaskbar="False"`, `CanResize="False"`.
- Default size ~ 280×120. Anchored top-right of primary screen on first run; position
  persisted to `settings.json` after drag.
- Drag: handle `PointerPressed` on the root border → `BeginMoveDrag`.
- Right-click → context menu: Settings, Reset position, Quit.
- Keyboard: `Esc` hides to tray.

### 5.2 Layout (compact mode)

```
┌──────────────────────────────┐
│ ◐ Claude   1.24M tok  $12.40 │
│ ◑ Codex      318k tok  $0.92 │
│ ── Today ──── 7d ──── 30d ── │
└──────────────────────────────┘
```

Click a row → expands to a second window/panel with:
- Per-model breakdown (tokens + $)
- Per-project (cwd) breakdown
- Sparkline of last 14 days

### 5.3 Tray

- Always-present `TrayIcon`. Left-click toggles overlay visibility. Menu mirrors
  the right-click menu.

---

## 6. Settings

Stored at `~/.config/AgentUsageViewer/settings.json` (mac/linux) or
`%APPDATA%\AgentUsageViewer\settings.json` (Windows). Resolved via
`Environment.SpecialFolder.ApplicationData`.

```json
{
  "claudeRoot": "C:\\Users\\Steven\\.claude\\projects",
  "codexRoot":  "C:\\Users\\Steven\\.codex\\sessions",
  "pricingPath": "pricing.json",
  "window": { "x": 1620, "y": 40, "opacity": 0.92 },
  "refreshDebounceMs": 500,
  "showCost": true,
  "ranges": ["today", "7d", "30d"]
}
```

If the roots don't exist, the source is silently disabled and the UI shows `—`
for that agent.

---

## 7. Build & Distribution

```
# Windows
dotnet publish src/AgentUsageViewer.App -c Release -r win-x64 \
    -p:PublishSingleFile=true -p:SelfContained=true -p:PublishTrimmed=true

# macOS (Apple Silicon)
dotnet publish src/AgentUsageViewer.App -c Release -r osx-arm64 \
    -p:PublishSingleFile=true -p:SelfContained=true
# Wrap in .app bundle (small bash script in /scripts/make-app-bundle.sh)
```

Trimming Avalonia requires `<TrimMode>partial</TrimMode>` and a roots file —
worth the binary-size win (target < 30 MB on Windows). NativeAOT is **not** v1
goal; revisit once Avalonia 11.x AOT story is fully smooth.

---

## 8. Testing Strategy

- **Parser tests** (xUnit) using anonymized real-world fixtures committed under
  `samples/`. Cover:
  - Claude assistant message with all four token fields
  - Claude lines that aren't assistant messages (must be skipped)
  - Resumed Claude session (duplicate `message.id`)
  - Codex `token_count` cumulative behavior — assert we take max, not sum
  - Codex file with multiple `turn_context` entries → first one wins for model
  - Truncated final line (writer mid-flush) — must not throw
- **Incremental read test:** write a temp file, read, append, read again, assert
  only new records emitted.
- **Aggregator test:** synthetic records crossing day boundaries in various TZs;
  assert "today" uses local time.

---

## 9. Implementation Order (suggested PRs)

1. **Scaffold:** solution, Core + App + Tests projects, Avalonia hello-world overlay window.
2. **Claude parser + tests** with fixtures from `samples/claude/`.
3. **Codex parser + tests** with fixtures from `samples/codex/`. Verify cumulative-vs-delta handling.
4. **Sources + FileSystemWatcher + incremental cursors.**
5. **Aggregator + pricing/cost calculator.**
6. **OverlayViewModel + binding to live totals.**
7. **Tray icon + show/hide + settings persistence.**
8. **Expanded breakdown panel (per-model, per-project, sparkline).**
9. **Packaging scripts for Windows + macOS, README with run instructions.**
10. **(Optional) Global hotkey, click-through mode, dark/light theme follow-OS.**

Each PR should be independently mergeable and shippable.

---

## 10. Open Questions for the Implementer

1. Confirm Codex `total_token_usage` semantics in the version you have installed —
   this plan assumes cumulative-per-session. If it turns out to be per-turn delta,
   switch the aggregator to sum instead of max. The test fixture should pin this.
2. Pricing for `gpt-5.4` and current Claude 4.6 family — fill in real numbers from
   the official pricing pages before shipping; placeholders above are illustrative.
3. macOS: ad-hoc codesign is fine for personal use, but Gatekeeper will warn on
   first launch. Decide whether v1 ships notarized or with a documented
   right-click-Open workaround.
4. Should "cost" be hidden by default for screen-sharing safety? (Setting exists;
   default value TBD.)

---

## 11. Reference: Known Local Paths (Windows, this machine)

- Claude projects root: `C:\Users\Steven\.claude\projects\`
- A current session file: `C:\Users\Steven\.claude\projects\C--Users-Steven-Repos-AgentUsageViewer\<sessionId>.jsonl`
- Codex sessions root: `C:\Users\Steven\.codex\sessions\`
- Codex session file pattern: `<root>\YYYY\MM\DD\rollout-<iso-ts>-<uuid>.jsonl`

On macOS the equivalents are `~/.claude/projects/` and `~/.codex/sessions/`.
