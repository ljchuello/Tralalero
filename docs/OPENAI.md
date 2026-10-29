# 🔑 OpenAI setup

> Tralalero uses OpenAI's Audio API for transcription. This page explains the key, model, request shape, limits, and failure modes.

## 🌐 What Tralalero calls

```http
POST https://api.openai.com/v1/audio/transcriptions
Authorization: Bearer <your-api-key>
Content-Type: multipart/form-data
```

| Field | Value |
|---|---|
| `file` | `segment.wav` — 16 kHz, mono, 16-bit PCM |
| `model` | `gpt-transcribe` |
| `languages[]` | `es` by default; omitted when set to **Auto-detect** |
| `prompt` | Last ~240 characters of the transcript, for continuity |
| `response_format` | `json` (optional; the API defaults to JSON) |

Tralalero creates one request per detected phrase. Silence is not uploaded.

## 🧠 Model

Use **`gpt-transcribe`**. It is OpenAI's recommended model for transcribing recorded speech in its original language as of October 2026.

> ⚠️ OpenAI lists `gpt-4o-transcribe`, `gpt-4o-mini-transcribe`, and `whisper-1` as deprecated, with shutdown scheduled for **February 26, 2027**. Tralalero does not use them.

Model documentation: [Speech to text](https://platform.openai.com/docs/guides/speech-to-text)

## 🔐 Create and store a key

1. Go to [platform.openai.com/api-keys](https://platform.openai.com/api-keys).
2. Create a **project** key.
3. Ensure the project has billing/usage enabled.
4. Paste it into Tralalero's setup or Settings.

Tralalero:

- validates the key with `GET /v1/models`,
- stores it encrypted with **Windows DPAPI (CurrentUser)**,
- never writes it to `settings.json`,
- never logs it or places it on the clipboard,
- can remove it from Settings at any time.

If you use multiple OpenAI projects, use a dedicated project key for Tralalero so usage is easy to track and revoke.

## 💳 Cost

OpenAI bills transcription per minute of audio. Prices and model availability can change; always check the official [pricing page](https://openai.com/api/pricing/).

Tralalero reduces cost by:

- not sending silence,
- using one request per phrase,
- capping recordings at 10 minutes by default,
- retrying only transient failures.

> 💡 A 10-minute dictation is not automatically one 10-minute upload; it is several short phrase requests. The app shows the number of phrases in the session status.

## 📏 Limits

- Maximum file size: **25 MB per request**.
- Tralalero's 20-second maximum phrase is well below this limit.
- Supported input formats include `mp3`, `mp4`, `mpeg`, `mpga`, `m4a`, `wav`, and `webm`; Tralalero sends WAV.
- Rate limits depend on your OpenAI project tier.
- Audio is sent over HTTPS with the platform certificate validation.

## 🧪 Validate the key without spending credits

Tralalero uses:

```http
GET https://api.openai.com/v1/models
Authorization: Bearer <key>
```

A `200` means the key is accepted. `401`/`403` means it is invalid or lacks permission.

## 🧯 Errors and retries

| Status | Meaning | Tralalero behavior |
|---|---|---|
| `400` | Invalid request/model/language | Shows the API message; no retry |
| `401` | Invalid key | Stops and asks you to check Settings |
| `403` | Key lacks permission | Stops with an actionable message |
| `413` | Segment too large | Shows an error; no retry |
| `429` | Rate limit | Retries with `Retry-After`/backoff, up to 3 attempts |
| `5xx` | OpenAI service issue | Retries, up to 3 attempts |
| Timeout | No response in 60 s | Retries, then reports a timeout |
| Network error | DNS/TLS/offline | Retries, then reports a connection problem |

Partial transcripts are preserved when a failure occurs. The app never silently discards text you can see.

## 🔒 Data handling

- Tralalero sends **audio** to OpenAI; it does not send logs, transcripts, or telemetry back to any Tralalero server (there is no such server).
- Tralalero does not store audio.
- OpenAI's handling of API data is governed by [OpenAI's privacy policy](https://openai.com/policies/privacy-policy/) and your organization's agreement.
- If your audio is sensitive, review your OpenAI data controls and organizational policy before use.

## 🧰 Advanced: API-compatible base URL

`AppSettings.ApiBaseUrl` defaults to `https://api.openai.com/v1`. The Settings UI does not expose it, but developers can change it in `%APPDATA%\Tralalero\settings.json` for a compatible proxy/test server.

The override must support:

- `POST {baseUrl}/audio/transcriptions`,
- multipart form data,
- bearer authentication,
- the `gpt-transcribe` model (or a compatible transcription model).

Use this responsibly: a proxy can see your audio and key. Tralalero does not validate custom endpoints beyond the normal HTTP response.

## 📚 Official links

- [Speech to text guide](https://platform.openai.com/docs/guides/speech-to-text)
- [API reference: create transcription](https://platform.openai.com/docs/api-reference/audio/createTranscription)
- [API keys](https://platform.openai.com/api-keys)
- [Pricing](https://openai.com/api/pricing/)
- [Privacy policy](https://openai.com/policies/privacy-policy/)

---

📖 Next: [Privacy](PRIVACY.md) · [Troubleshooting](TROUBLESHOOTING.md)
