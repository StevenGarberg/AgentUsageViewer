# AgentUsageViewer

AgentUsageViewer is a lightweight Avalonia overlay for tracking local Claude Code and Codex CLI usage without API keys, hooks, or network calls. It watches the JSONL session logs each tool already writes under your home directory, aggregates token usage for `today`, `7d`, `30d`, and `all`, and shows a compact always-on-top dashboard with drill-downs by model and project.

## Features

- Cross-platform .NET 10 desktop app built with Avalonia 11.
- Always-on-top overlay with tray icon, drag-to-move, and persisted placement.
- Live Claude and Codex usage from local JSONL logs with incremental tail-reading.
- Editable local `pricing.json` for cost estimates.
- Breakdown window with per-model, per-project, and 14-day trend views.
- Settings window for roots, pricing path, refresh debounce, overlay opacity, and cost visibility.
- Parser, incremental-read, and aggregation tests with anonymized sample fixtures.

## Repo Layout

- `src/AgentUsageViewer.Core`: parsing, sources, pricing, aggregation, settings persistence.
- `src/AgentUsageViewer.App`: Avalonia overlay UI and desktop services.
- `src/AgentUsageViewer.Tests`: xUnit coverage for parser/source/reader/aggregator behavior.
- `samples/`: anonymized JSONL fixtures for Claude and Codex.
- `scripts/`: publish helpers.

## Run

```powershell
dotnet build AgentUsageViewer.sln
dotnet test AgentUsageViewer.sln
dotnet run --project src/AgentUsageViewer.App/AgentUsageViewer.App.csproj
```

The app defaults to:

- Claude root: `%USERPROFILE%\.claude\projects`
- Codex root: `%USERPROFILE%\.codex\sessions`
- Settings file: `%APPDATA%\AgentUsageViewer\settings.json`
- Pricing file: `pricing.json` copied next to the app output

## Publish

Windows single-file publish:

```powershell
./scripts/publish-win.ps1
```

macOS publish plus `.app` wrapping:

```bash
dotnet publish src/AgentUsageViewer.App -c Release -r osx-arm64 -p:PublishSingleFile=true -p:SelfContained=true
./scripts/make-app-bundle.sh ./src/AgentUsageViewer.App/bin/Release/net10.0/osx-arm64/publish
```

## Notes

- Unknown models intentionally show cost as `-` instead of inventing a price.
- Codex token totals are treated as cumulative-per-session and collapsed to the max total seen per rollout file.
- The app is read-only against the underlying agent folders.
