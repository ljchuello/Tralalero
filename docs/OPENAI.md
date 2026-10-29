# 🔑 OpenAI setup

> Tralalero uses OpenAI's Audio API by default and can also target OpenAI-compatible transcription endpoints. This page explains the key, model, request shape, limits, and failure modes.

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

Tralalero creates one request per detected phrase. Silence is not uploaded. On custom hosts it automatically uses `language` instead of `languages[]` and may omit the prompt and auth; see [compatible endpoints](#-openai-compatible-endpoints).

## 🧠 Model

Use **`gpt-transcribe`**. It is OpenAI's recommended model for transcribing recorded speech in its original language as of October 2026.

> ⚠️ OpenAI lists `gpt-4o-transcribe`, `gpt-4o-mini-transcribe`, and `whisper-1` as deprecated, with shutdown scheduled for **February 26, 2027**. Tralalero does not use them on the OpenAI endpoint. Compatible servers may still expose `whisper-1`; use whatever model name your server documents.

Model documentation: [Speech to text](https://platform.openai.com/docs/guides/speech-to-text)

## 🌐 OpenAI-compatible endpoints

Go to **Settings → 🌐 Endpoint & API key** and set:

- **Base URL** — `https://api.openai.com/v1` by default.
- **Model** — `gpt-transcribe` by default; the field is editable.

Use **Reset to OpenAI** to restore both defaults. Base URL and model are stored in `%APPDATA%\Tralalero\settings.json`; the API key remains in `credentials.bin`.

### 🔀 Automatic compatibility mode

| Setting | OpenAI (`api.openai.com`) | Custom host |
|---|---|---|
| Auth | Bearer key required | Bearer if a key is present; no auth if empty |
| Language field | `languages[]` | `language` |
| Context prompt | Sent | Skipped for compatibility |
| `/models` check | Required | Best-effort; `404/405` is accepted |
| Transport | HTTPS | HTTPS for remote hosts; HTTP only for localhost/private LAN |

This keeps the UI simple while covering the common Whisper-compatible servers.

### 🧪 Example endpoints

| Server | Base URL | Example model | Auth |
|---|---|---|---|
| OpenAI | `https://api.openai.com/v1` | `gpt-transcribe` | Bearer |
| LM Studio | `http://localhost:1234/v1` | `whisper-1` | None |
| LocalAI | `http://localhost:8080/v1` | `whisper-1` | None |
| faster-whisper / Speaches | `http://localhost:8000/v1` | `Systran/faster-whisper-large-v3` | None |
| OpenRouter | `https://openrouter.ai/api/v1` | `openai/whisper-large-v3` | Bearer |
| Groq | `https://api.groq.com/openai/v1` | `whisper-large-v3-turbo` | Bearer |

> ⚠️ These are examples, not guarantees. Model names, auth, response formats, and supported fields vary by server. Check your server's documentation.
>
> OpenRouter uses provider-namespaced STT model slugs such as `openai/whisper-large-v3`. Identifiers like `Systran/faster-whisper-large-v3` belong to local faster-whisper servers and are not valid on OpenRouter.

### 🔐 Key safety

- Changing the endpoint **host** clears the previously stored key before the next save, so an OpenAI key is never silently forwarded to a custom host.
- Remote endpoints must use HTTPS. HTTP is allowed for `localhost`, loopback, and private LAN addresses.
- A keyless local endpoint is allowed; the main status shows `🔓 No key required`.

### 🚫 Not covered yet

Azure OpenAI uses deployment paths, `api-version`, and an `api-key` header; it is not supported by the simple Base URL field. Custom headers and multiple endpoint profiles are also out of scope for this version.

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

A `200` means the key is accepted. `401`/`403` means it is invalid or lacks permission. Custom endpoints may return `404`/`405`; Tralalero treats that as “saved but unverified” instead of blocking the endpoint.

## 🧯 Errors and retries

| Status | Meaning | Tralalero behavior |
|---|---|---|
| `400` | Invalid request/model/language | Shows the API message; no retry |
| `401` | Invalid key | Stops and asks you to check Settings |
| `403` | Key lacks permission | Stops with an actionable message |
| `413` | Segment too large | Shows an error; no retry |
| `429` | Rate limit | Retries with `Retry-After`/backoff, up to 3 attempts |
| `5xx` | Endpoint service issue | Retries, up to 3 attempts |
| Timeout | No response in 60 s | Retries, then reports a timeout |
| Network error | DNS/TLS/offline | Retries, then reports a connection problem |

Partial transcripts are preserved when a failure occurs. The app never silently discards text you can see.

## 🔒 Data handling

- Tralalero sends **audio** to the configured endpoint; it does not send logs, transcripts, or telemetry back to any Tralalero server (there is no such server).
- Tralalero does not store audio.
- OpenAI's handling of API data is governed by [OpenAI's privacy policy](https://openai.com/policies/privacy-policy/) and your organization's agreement.
- A custom endpoint has its own privacy policy; review it before sending sensitive audio.
- If your audio is sensitive, review the relevant data controls and organizational policy before use.

## 🧰 Advanced: custom path and query

The client appends `/audio/transcriptions` unless the Base URL already ends with that path:

```text
http://localhost:1234/v1                 → /v1/audio/transcriptions
http://localhost:1234/v1/audio/transcriptions → unchanged
```

The `/models` check strips the transcription path and uses the API root. If a custom server does not expose `/models`, Tralalero accepts the endpoint but marks the check as unverified.

> ⚠️ A custom proxy can see your audio and key. Use a trusted server and never enter a key you do not want that server to receive.

## 📚 Official links

- [Speech to text guide](https://platform.openai.com/docs/guides/speech-to-text)
- [API reference: create transcription](https://platform.openai.com/docs/api-reference/audio/createTranscription)
- [API keys](https://platform.openai.com/api-keys)
- [Pricing](https://openai.com/api/pricing/)
- [Privacy policy](https://openai.com/policies/privacy-policy/)

---

📖 Next: [Privacy](PRIVACY.md) · [Troubleshooting](TROUBLESHOOTING.md)
