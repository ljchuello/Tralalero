# 🛠️ Development

> Build, test, debug, and release Tralalero.

## ✅ Prerequisites

- Windows 10/11 x64.
- [.NET 10 SDK](https://dotnet.microsoft.com/download) (`10.0.401` is pinned in `global.json`; `rollForward: latestFeature` accepts newer 10.x SDKs).
- PowerShell 7+ recommended for scripts; Windows PowerShell 5.1 works for icon generation.
- Optional: Visual Studio 2026 / VS Code with C# Dev Kit.
- An OpenAI API key only for manual end-to-end testing.

## 🚀 Run

```powershell
dotnet restore .\Tralalero.slnx
dotnet build .\Tralalero.slnx -c Debug
dotnet run --project .\Tralalero\Tralalero.csproj
```

The app is a `WinExe`; `dotnet run` starts the WinForms UI.

## 🧪 Test

```powershell
dotnet test .\Tralalero.slnx -c Debug
```

The suite does **not** call OpenAI. Fake audio and fake HTTP keep tests deterministic and free.

```powershell
# Optional coverage
dotnet test .\Tralalero.slnx -c Debug --collect:"XPlat Code Coverage"
```

### Test map

| Area | File |
|---|---|
| VAD segmentation | `VoiceActivitySegmenterTests.cs` |
| Transcript merging | `TranscriptAggregatorTests.cs` |
| OpenAI request/retry/errors | `OpenAiClientTests.cs` |
| Session orchestration | `DictationSessionTests.cs` |
| Settings round-trip/corruption | `SettingsServiceTests.cs` |
| DPAPI key storage | `ApiKeyStoreTests.cs` |
| Hotkey parsing | `HotkeyDefinitionTests.cs` |
| WAV header | `WavWriterTests.cs` |
| Log redaction | `AppLoggerTests.cs` |

## 🎨 Format & style

- `.editorconfig` defines C# style; `Directory.Build.props` enables nullable and treats warnings as errors.
- File-scoped namespaces are preferred.
- Async methods end in `Async`; use `CancellationToken` where cancellation is meaningful.
- No `async void` except UI event handlers (which must catch their own exceptions).
- Public core types carry XML documentation (the build generates an XML doc file).
- UI strings live in `App/UiText.cs` for consistency; docs/UI use emojis, identifiers do not.

```powershell
dotnet format .\Tralalero.slnx
dotnet format .\Tralalero.slnx --verify-no-changes
```

## 📁 Where things live

| Path | Purpose |
|---|---|
| `Tralalero/App/` | Forms, tray, overlay, theme, UI strings. |
| `Tralalero/Core/Audio/` | NAudio recorder, VAD, WAV writer. |
| `Tralalero/Core/OpenAI/` | HTTP client, model constants, exceptions. |
| `Tralalero/Core/Transcription/` | Session, queue, aggregator, provider seam. |
| `Tralalero/Core/Security/` | DPAPI key storage. |
| `Tralalero/Core/Settings/` | Settings model/service. |
| `Tralalero/Core/Interop/` | Hotkey, paste, startup, single instance. |
| `Tralalero/Core/Infrastructure/` | Paths and logging. |
| `Tralalero.Tests/` | xUnit tests. |
| `docs/` | Architecture, usage, OpenAI, privacy, troubleshooting. |
| `scripts/` | Publish and icon-generation scripts. |

## 🔐 Runtime data for debugging

| Data | Path |
|---|---|
| Settings | `%APPDATA%\Tralalero\settings.json` |
| Encrypted key | `%APPDATA%\Tralalero\credentials.bin` |
| Logs | `%LOCALAPPDATA%\Tralalero\logs` |

To simulate first run, close the app and delete `settings.json` and `credentials.bin`. **Do not commit either file.**

## 🧪 Manual test checklist

Use a real microphone and a real API key:

- [ ] First run: consent, invalid key rejected, valid key accepted.
- [ ] Dictate 3 short phrases into Notepad; text appears after pauses.
- [ ] Stop with the hotkey; final phrase is included; text is pasted into Notepad.
- [ ] Start from the app button; text stays in Tralalero and is copied.
- [ ] Toggle auto-paste off; verify clipboard-only behavior.
- [ ] Change language to Auto-detect; dictate a short phrase.
- [ ] Change VAD sensitivity; test in a quiet/noisy room.
- [ ] Change the hotkey; verify the old one stops working.
- [ ] Switch to tray; double-click to restore; use tray Start/Stop.
- [ ] Close to tray; exit from tray.
- [ ] Disconnect the microphone; start dictation; confirm a friendly error.
- [ ] Use an invalid key; confirm the app preserves the partial transcript.
- [ ] Disable internet; confirm timeout/connection messaging.
- [ ] Trigger 429 (if possible); confirm retries and eventual message.
- [ ] Check logs contain no key or transcript text.
- [ ] Verify the app is DPI-aware on a high-DPI display.

## 🧩 Adding a provider

1. Implement `ITranscriber` in `Core/Transcription/`.
2. Accept `AudioSegment`, `TranscriptionOptions`, prompt context, and `CancellationToken`.
3. Return plain text.
4. Wire it in `MainForm.StartDictationAsync` (or a small factory).
5. Add tests with a fake provider/HTTP handler.
6. Update `docs/ARCHITECTURE.md` and privacy docs.

A local Whisper provider is intentionally not included in v0.1; the seam exists so it can be added without rewriting the UI.

## 📦 Publish

```powershell
pwsh .\scripts\publish.ps1 -Version 0.1.0
```

The script:

- publishes `win-x64` **self-contained** (no .NET install needed),
- keeps native dependencies beside the executable (`PublishSingleFile=false`) for reliability,
- copies README, license, privacy guide, usage guide, and third-party notices,
- creates `artifacts\Tralalero-0.1.0-win-x64.zip`.

> 🧳 **Why not single-file?** Native audio dependencies and icon assets behave more predictably in a normal publish folder. The ZIP is still one download for users.

## 🤖 CI

`.github/workflows/ci.yml` runs on `windows-latest`:

1. restore,
2. `dotnet format --verify-no-changes`,
3. build in Release (warnings are errors),
4. tests.

`.github/workflows/release.yml` runs on `v*` tags and attaches the publish ZIP to a GitHub release.

## 🛡️ Security invariants

- Never commit API keys, `credentials.bin`, `settings.json`, logs, or audio.
- Never log transcript text.
- Never put the API key in UI error messages or clipboard.
- Keep `ApiKeyStore` on DPAPI `CurrentUser`.
- Treat `ApiBaseUrl` overrides as trusted configuration.
- Use HTTPS for any real endpoint.

## 🧯 Debugging tips

- Start with `%LOCALAPPDATA%\Tralalero\logs`; every startup logs a line.
- If onboarding doesn't appear, check `Main form shown` / `Opening setup dialog` entries.
- If hotkeys do nothing, check for `Global hotkey '…' could not be registered` and change the hotkey.
- If the app is invisible, check the tray and `Get-Process Tralalero | Select MainWindowHandle`; the window may be on a secondary monitor.
- For UI layout issues, run at 100% and 150% DPI; the app is PerMonitorV2.
- For API issues, validate the key with `Test key` before blaming audio.

## 📋 Release checklist

- [ ] Update `CHANGELOG.md`.
- [ ] Bump `<Version>` in `Directory.Build.props`.
- [ ] `dotnet format --verify-no-changes`.
- [ ] `dotnet test -c Release`.
- [ ] Run the manual checklist.
- [ ] `pwsh .\scripts\publish.ps1 -Version x.y.z`.
- [ ] Verify the ZIP starts on a clean Windows machine.
- [ ] Tag `vx.y.z` and let the release workflow attach the ZIP.
- [ ] Add a screenshot/GIF to the release notes.

---

📖 Next: [Architecture](ARCHITECTURE.md) · [Usage](USAGE.md) · [Contributing](../CONTRIBUTING.md)
