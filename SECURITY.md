# 🔐 Security policy

## Supported versions

| Version | Supported |
|---|---|
| `0.1.x` | ✅ |
| older/pre-release | ❌ |

## Reporting a vulnerability

Please **do not open a public issue** for security problems.

Use a private GitHub security advisory on the repository, or contact the maintainer through the repository's private contact channel. Include:

- affected version,
- Windows version,
- reproduction steps,
- impact,
- any relevant log lines **with secrets and transcripts removed**.

We will acknowledge receipt as soon as possible and coordinate a fix/disclosure.

## Security model

- The API key is encrypted with **Windows DPAPI** (`CurrentUser`) in `%APPDATA%\Tralalero\credentials.bin`.
- Settings never contain the key.
- Changing the transcription endpoint host clears the stored key so it cannot be forwarded to a different server.
- Logs contain metadata only; transcript text and key material must never be logged.
- Audio is memory-only and sent only to the configured OpenAI-compatible endpoint.
- No telemetry, accounts, or Tralalero servers.
- Clipboard content is visible to other desktop apps, as with any Windows app.
- Auto-paste uses `SendInput` into the previously active window; elevated apps may reject it.

## Out of scope

- Malware running as the same Windows user.
- A compromised OpenAI account or API key.
- Screen/clipboard capture by other apps.
- A malicious custom `ApiBaseUrl` configured by someone with access to your settings.
- SmartScreen warnings caused by the absence of code signing (planned).

## Dependencies

See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). Keep NuGet packages updated; run `dotnet list package --vulnerable` before releases.
