# 🔐 Privacy

> Tralalero is a thin local client around OpenAI transcription. This page explains exactly what leaves your PC, what stays local, and what you can delete.

## 🗺️ Data flow

```text
🎤 Microphone
   ↓ (in memory)
AudioRecorder → VAD → WAV phrase
   ↓ (HTTPS)
☁️ OpenAI /v1/audio/transcriptions
   ↓ (JSON text)
TranscriptAggregator → UI → 📋 clipboard / 💾 user save
```

## 📊 What is processed where

| Data | Location | Retention |
|---|---|---|
| 🎙️ Microphone audio | RAM on your PC, then OpenAI | Tralalero stores no audio files. OpenAI retention is governed by your OpenAI agreement. |
| 📝 Transcript | RAM in Tralalero | Until you clear it, close the app, or save it. |
| 🔑 API key | `%APPDATA%\Tralalero\credentials.bin` | Encrypted with Windows DPAPI for your Windows user. |
| ⚙️ Settings | `%APPDATA%\Tralalero\settings.json` | Until you delete the file. No API key is stored here. |
| 🧾 Logs | `%LOCALAPPDATA%\Tralalero\logs` | Daily files; old files are pruned after 14 days. |
| 📋 Clipboard | Windows clipboard | Until another app replaces it. |
| 📊 Telemetry | None | Tralalero has no analytics or tracking service. |

## 🎙️ Audio

- Tralalero captures at 16 kHz mono.
- Audio is held in memory while recording.
- Only detected phrases are sent; silence is not uploaded.
- Tralalero does not write WAV files to disk.
- Network requests use HTTPS to `api.openai.com` (or the advanced `ApiBaseUrl` override).
- If you save a transcript, only text is written, not audio.

> ⚠️ **Sensitive audio:** if you dictate confidential information, remember that the audio is processed by OpenAI. Review your organization's policies and OpenAI data controls before use.

## 🔑 API key

- Stored with **Windows DPAPI** using `DataProtectionScope.CurrentUser`.
- The encrypted blob is in `%APPDATA%\Tralalero\credentials.bin`.
- DPAPI ties decryption to your Windows user profile; another Windows user cannot read it.
- The key is never written to `settings.json`, logs, crash reports, or the clipboard.
- Log redaction also masks strings that look like keys.
- Removing the key from Settings deletes the encrypted file.

## 🧾 Logs

Tralalero logs operational metadata:

- app start/stop,
- selected microphone,
- model and language,
- session duration and phrase count,
- HTTP status/retry messages,
- exceptions.

It does **not** log:

- transcript text,
- API key material,
- audio bytes.

Logs live in `%LOCALAPPDATA%\Tralalero\logs` and are pruned after 14 days. You can open the folder from Settings.

## 📋 Clipboard and auto-paste

- The final transcript is copied to the Windows clipboard when auto-copy/auto-paste is enabled or when you press **Copy**.
- Auto-paste sends `Ctrl+V` to the window that was active when recording started.
- Other applications can read the clipboard; this is normal Windows behavior.
- Tralalero does not restore the previous clipboard content after pasting.

## 🌐 Network

Tralalero makes network requests only to the configured OpenAI-compatible transcription base URL:

- `GET /v1/models` for key validation,
- `POST /v1/audio/transcriptions` for phrases.

There is no Tralalero account, sync service, analytics endpoint, or update checker.

## 🧹 Deleting your data

| Data | How to delete |
|---|---|
| API key | Settings → **Remove key**, or delete `%APPDATA%\Tralalero\credentials.bin`. |
| Settings | Delete `%APPDATA%\Tralalero\settings.json`. |
| Logs | Delete `%LOCALAPPDATA%\Tralalero\logs`. |
| Transcript | Clear it in the app; it is not persisted unless you save a file. |
| Saved transcripts | Delete the `.txt` files you created. |
| Windows startup entry | Settings → disable **Start with Windows**, or remove `Tralalero` from `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`. |

## 🧠 Threat model

Tralalero protects against:

- ✅ casual access to the API key on disk (DPAPI + no plaintext),
- ✅ accidental transcript logging,
- ✅ silent background recording (the overlay/tray shows listening state; the app never records without a start action),
- ✅ clipboard leakage from logs (transcripts are not logged).

Tralalero does **not** protect against:

- ❌ malware running as your Windows user (it can read the key while the app runs, or capture audio),
- ❌ a compromised OpenAI account/API key,
- ❌ screen recording or clipboard monitoring by other apps,
- ❌ a malicious custom `ApiBaseUrl` proxy configured by someone with access to your settings.

Use a dedicated OpenAI project key, rotate it if exposed, and keep Windows updated.

## 📄 OpenAI's role

OpenAI is the transcription processor. Its handling of API data is governed by:

- [OpenAI privacy policy](https://openai.com/policies/privacy-policy/)
- your OpenAI organization/project settings and agreement.

Tralalero is an independent project and is not affiliated with OpenAI.

---

📖 Next: [Security policy](../SECURITY.md) · [OpenAI setup](OPENAI.md)
