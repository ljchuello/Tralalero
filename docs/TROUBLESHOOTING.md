# 🧯 Troubleshooting

> Quick fixes for the things that usually go wrong.

## 🔑 Setup and API key

### The onboarding window doesn't appear

- Check the tray: the main window may be minimized or on another monitor.
- Open **Settings → Test key**; if the key is missing, use **Open setup →** in the banner.
- Look at `%LOCALAPPDATA%\Tralalero\logs` for `Opening setup dialog` and any exception.

### “OpenAI rejected the API key” (401/403)

1. Create a fresh project key at [platform.openai.com/api-keys](https://platform.openai.com/api-keys).
2. Confirm the project has billing/usage enabled.
3. Paste the key again in Settings → **Test key**.
4. Remove whitespace/newlines around the key.

### “Rate limit reached” (429)

- Wait a minute and try again.
- Check your OpenAI project usage/limits.
- Retry behavior is automatic (up to 3 attempts with backoff).

### Custom endpoint returns 404/405

- Some compatible servers do not implement `/models`; Tralalero treats that as **saved but unverified**, not as an auth failure.
- Verify the Base URL ends at the API root, usually `/v1`.
- If the server exposes the transcription endpoint directly, a Base URL ending in `/audio/transcriptions` is also accepted.
- Check the server logs to confirm it implements `POST /audio/transcriptions`.

### Custom endpoint rejects `languages[]` or `prompt`

For non-OpenAI hosts, Tralalero automatically sends `language` (not `languages[]`) and skips the context prompt. If it still fails:

- Check the server's expected language field and model name.
- Some servers require a specific model identifier; use the exact name shown by the server.
- Try a short phrase in a quiet room to rule out audio/VAD issues.

### OpenRouter returns “Invalid multipart/form-data body”

Older builds sent multipart field names unquoted (`name=file`), which OpenRouter rejects. Update Tralalero so the upload uses quoted names (`name="file"`).

Also use an OpenRouter STT slug such as `openai/whisper-large-v3` or `openai/whisper-1`. A local server identifier such as `Systran/faster-whisper-large-v3` is not a valid OpenRouter model.

### Local endpoint works without a key

- In Settings, leave **API key** empty.
- Set the Base URL to `http://localhost:<port>/v1` and the model to the server's exact name.
- Use **Test key**; a keyless custom endpoint should report `✅ Endpoint saved; no key required.`
- If the app previously stored an OpenAI key, changing the host clears it automatically on save.

### “Remote endpoints must use HTTPS”

- Use `https://` for any public host.
- Plain HTTP is accepted only for `localhost`, loopback, and private LAN addresses (for example `192.168.x.x`).
- For a remote development server, put it behind HTTPS or use an SSH tunnel to localhost.

### “Could not reach the transcription endpoint” / timeout

- Check internet access.
- Check VPN/proxy/firewall rules for `api.openai.com` or your custom host.
- Try again; Tralalero retries transient errors.
- For a custom endpoint, confirm it supports multipart uploads and the selected model.
- For localhost, check that the local server is running and listening on the expected port.

## 🎤 Microphone

### “No microphone was found”

1. Open **Settings → Sound** in Windows and check the input device.
2. Open **Settings → Privacy & security → Microphone** and allow desktop apps.
3. Reconnect the microphone and restart Tralalero.
4. Choose the correct device in Tralalero → Settings.

### The app hears nothing / no phrases

- Watch the level meter while speaking.
- Increase **Voice sensitivity** (High for quiet rooms).
- Move closer to the microphone.
- Check the Windows input level and mute button.
- Confirm the selected device is the one you are actually using.

### Phrases are cut off

- Speak with a slightly longer pause (~0.7 s) between phrases.
- Increase sensitivity if the detector is missing quiet speech.
- Avoid very noisy rooms; the detector may treat noise as speech or vice versa.

### Very long phrase / 20-second splits

- Tralalero caps a phrase at 20 seconds. Pause more often for cleaner results.
- Long continuous speech may be split at the cap; this is intentional.

## ⌨️ Hotkey

### `Ctrl+Alt+Space` does nothing

- Another app may own the shortcut. Open Settings and choose a different combination.
- The app logs `Global hotkey '…' could not be registered`.
- Some games/apps capture keys globally; close them or change the hotkey.
- If the main window is open and a modal dialog is active, the hotkey is intentionally ignored.

### The hotkey starts recording but I can't stop it

- Press `Ctrl+Alt+Space` again (toggle).
- If the app is finishing a request, wait for `🟢 Ready`.
- Use the tray **Stop dictation** item or the app button.

## ⌨️ Auto-paste

### Text is copied but not pasted

- Auto-paste uses `Ctrl+V` in the previously active window. Some elevated apps (running as administrator) reject input from a non-elevated app.
- If the target window was closed or changed, Tralalero leaves the text in the clipboard.
- Try a normal app such as Notepad/VS Code first.
- If you need to paste into an elevated app, run Tralalero at the same elevation **only if you trust it**; this is not required for normal use.

### Pasting goes to the wrong window

- Start recording from the target app (hotkey), not from Tralalero's button.
- Don't switch windows while the final phrase is being transcribed.
- Tralalero captures the foreground window at start and refreshes it at stop when possible.

### Clipboard is busy

- Another app is holding the clipboard. Try **Copy** again.
- Tralalero retries clipboard writes a few times.

## 🪟 Window and tray

### Closing the window didn't exit

That is intentional when **Close to tray** is on. Use tray → **❌ Exit**.

### The app is running but I can't find the window

- Double-click the tray icon.
- The window may be on a secondary monitor; use `Win+Shift+Left/Right` or the taskbar.
- Check Task Manager for `Tralalero.exe`.

### Two instances / hotkey conflict

Tralalero uses a per-session mutex. If a second launch occurs, it tries to bring the existing window forward. If it can't find it, close the other instance from Task Manager.

## 🧾 Logs and settings

Logs: `%LOCALAPPDATA%\Tralalero\logs`  
Settings: `%APPDATA%\Tralalero\settings.json`  
Encrypted key: `%APPDATA%\Tralalero\credentials.bin`

### Reset everything

1. Exit Tralalero from the tray.
2. Delete `%APPDATA%\Tralalero\settings.json` and `credentials.bin`.
3. Optionally delete `%LOCALAPPDATA%\Tralalero\logs`.
4. Start Tralalero and complete onboarding again.

### “The stored API key could not be decrypted”

The DPAPI blob belongs to another Windows user or the profile changed. Remove the key and paste it again. If you copied the app data folder from another PC, this is expected.

### Corrupt `settings.json`

Tralalero moves it to `settings.json.corrupt-<timestamp>` and uses defaults. Restore values manually or delete the corrupt file.

## 🖥️ Display / UI

### Emojis show as boxes or monochrome

Windows renders emojis through the installed Segoe UI Emoji font. Some remote-desktop or stripped Windows images lack color emoji. Tralalero still works; emojis in docs remain colorful.

### Blurry UI / wrong scaling

Tralalero is PerMonitorV2 DPI-aware. If a remote session or old app overrides DPI, sign out/in or adjust Windows display scaling. Report the scaling combination if it persists.

## 🛡️ SmartScreen / antivirus

The release executable is unsigned. SmartScreen may show “Windows protected your PC”; choose **More info → Run anyway** only if you downloaded from a trusted source or built it yourself. Code signing is planned.

## 🧠 Performance

- Phrase requests are sequential; a slow connection increases latency after each pause.
- Keep each phrase short for snappier text.
- The app is lightweight; if CPU is high, check for another audio app or a very noisy input triggering constant phrases.
- A 10-minute recording is capped by default; change it in Settings if needed.

## 📬 Still stuck?

Open an issue with:

- Windows version,
- Tralalero version,
- the relevant log lines (redact any personal data),
- whether the mic/key works in another app,
- steps to reproduce.

**Never paste your API key or private transcript into an issue.**
