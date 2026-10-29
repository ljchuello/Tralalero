# 🤝 Contributing to Tralalero

¡Gracias por querer mejorar Tralalero! Thanks for helping make local-ish dictation better.

## 🌱 Before you start

- Read [README](README.md) and [Architecture](docs/ARCHITECTURE.md).
- Check existing issues/PRs to avoid duplicate work.
- For security issues, follow [SECURITY.md](SECURITY.md) instead of opening a public issue.

## 🛠️ Set up

```powershell
git clone <your-fork>
cd Tralalero
dotnet restore .\Tralalero.slnx
dotnet build .\Tralalero.slnx -c Debug
dotnet test .\Tralalero.slnx -c Debug
```

You need Windows and the .NET 10 SDK. A real API key is only needed for manual end-to-end checks.

## 🌿 Branch & commits

- Branch from `main`/`master`: `feat/live-overlay`, `fix/clipboard-retry`, `docs/privacy`.
- Use [Conventional Commits](https://www.conventionalcommits.org/): `feat:`, `fix:`, `docs:`, `test:`, `refactor:`, `chore:`.
- Emojis are welcome in the commit description (e.g. `feat: 🎙️ add live overlay`), not required.
- Keep commits focused and reviewable.

## 🧪 Pull request checklist

- [ ] `dotnet format .\Tralalero.slnx --verify-no-changes`
- [ ] `dotnet build .\Tralalero.slnx -c Release`
- [ ] `dotnet test .\Tralalero.slnx -c Release`
- [ ] New behavior has tests where practical.
- [ ] Docs updated (README/usage/privacy/architecture as relevant).
- [ ] No API keys, transcripts, logs, audio, or personal data committed.
- [ ] UI strings use the existing emoji style and live in `App/UiText.cs` where shared.
- [ ] Logs still avoid transcript content.

## 🎨 Code style

- C# with nullable enabled; warnings are errors.
- File-scoped namespaces.
- Async methods end with `Async`; pass `CancellationToken` where cancellation matters.
- Avoid `async void` except UI handlers, and catch exceptions there.
- Dispose `HttpClient`/audio resources deterministically.
- Keep `Core` testable without WinForms where possible.
- Prefer small, explicit classes over frameworks.

## 😄 Emoji policy

- ✅ Emojis in docs, UI labels, status messages, tray menus, and log levels.
- ❌ Not in identifiers, file names, namespaces, JSON keys, URLs, or API fields.
- Keep them meaningful; a wall of emojis is not documentation.

## 🔐 Security rules

- Never log transcript text or API keys.
- Never store the API key outside `ApiKeyStore`/DPAPI.
- Never commit `credentials.bin`, `settings.json`, logs, or audio.
- Treat a custom `ApiBaseUrl` as trusted configuration.
- Prefer HTTPS; do not add telemetry.

## 📚 Documentation

- User-facing docs are in English under `docs/`.
- Add a short “why” to architecture decisions, not just a “what”.
- Update screenshots if the UI changes noticeably (keep them in `docs/assets/`).
- Link official OpenAI docs rather than copying volatile pricing details.

## 🧭 Good first issues

- Add a microphone test button to Settings.
- Add a live waveform to the overlay.
- Add Spanish UI localization.
- Improve VAD calibration with a rolling noise floor.
- Add a `--diagnostics` command-line mode.
- Add a local Whisper provider behind `ITranscriber`.

## 💬 Questions

Open a discussion/issue with context and logs (redacted). Be kind; this is a small project maintained in spare time.

---

Thank you for contributing. 🎙️✨
