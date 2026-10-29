# 📓 Changelog

All notable changes to Tralalero are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Planned

- 🎚️ Optional neural VAD backend.
- 🖥️ Live waveform in the overlay.
- 🔁 Local Whisper provider behind `ITranscriber`.
- 🌍 Spanish UI localization.
- 📦 Installer/MSIX and code signing.
- 📝 Custom vocabulary/keywords.

## [0.1.0] — 2026-10-29

### Added

- 🎙️ Near-live phrase dictation for Windows with `Ctrl+Alt+Space`.
- 🧠 OpenAI `gpt-transcribe` integration through `/v1/audio/transcriptions`.
- 🎚️ Energy-based VAD with pre-roll, hangover, min/max phrase sizes, and sensitivity.
- 📝 Live transcript, copy, save as UTF-8, clear, and editable text.
- ⌨️ Auto-paste into the previously active window with clipboard fallback.
- 🪟 Tray icon, tray menu, recording overlay, close-to-tray, and start with Windows.
- 👋 First-run onboarding with privacy consent and API key validation.
- ⚙️ Settings for model, language, microphone, sensitivity, hotkey, duration, and behavior.
- 🔐 API key encrypted with Windows DPAPI.
- 🧾 Daily logs with secret redaction and no transcript content.
- 🧪 29 unit tests covering VAD, aggregation, HTTP, settings, DPAPI, hotkeys, WAV, logs, and session orchestration.
- 📚 English documentation with architecture diagrams, usage, OpenAI setup, privacy, development, and troubleshooting.
- ⚙️ GitHub Actions CI and tagged release workflow.
- 📦 Self-contained `win-x64` publish script.

### Security

- API keys are never written to settings JSON or logs.
- Audio is not persisted by the app.
- No telemetry or Tralalero backend.

### Known limitations

- Phrase-based near-live, not word-by-word streaming.
- Requires internet and an OpenAI API key; not offline.
- Unsigned executable (SmartScreen may warn).
- Windows only.
- Auto-paste may be blocked for elevated target apps.
