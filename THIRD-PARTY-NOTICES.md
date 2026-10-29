# 📦 Third-party notices

Tralalero is MIT-licensed. It depends on the following software and services.

## Runtime dependencies

| Component | Version | License | Link |
|---|---:|---|---|
| .NET / WinForms | 10 | MIT | https://github.com/dotnet/runtime |
| NAudio | 3.3.0 | MIT | https://github.com/naudio/NAudio |
| System.Security.Cryptography.ProtectedData | .NET 10 | MIT | https://github.com/dotnet/runtime |
| Microsoft.Win32.Registry | .NET 10 | MIT | https://github.com/dotnet/runtime |

## Test dependencies

| Component | Version | License | Link |
|---|---:|---|---|
| xUnit | 2.9.3 | Apache-2.0 | https://github.com/xunit/xunit |
| xunit.runner.visualstudio | 3.1.4 | Apache-2.0 | https://github.com/xunit/visualstudio.xunit |
| Microsoft.NET.Test.Sdk | 17.14.1 | MIT | https://github.com/microsoft/vstest |
| coverlet.collector | 6.0.4 | MIT | https://github.com/coverlet-coverage/coverlet |

## Services

| Service | Role | Terms |
|---|---|---|
| OpenAI Audio API | Cloud transcription | https://openai.com/policies/terms-of-use/ |
| OpenAI API data | Audio processing | https://openai.com/policies/privacy-policy/ |

Tralalero does not bundle OpenAI models or SDKs. It calls the OpenAI REST API over HTTPS using `HttpClient`.

## Assets

- The Tralalero icon and logo were generated for this project from simple geometric shapes. No third-party icon assets are bundled.

## Notes

- Product names, logos, and trademarks are the property of their respective owners.
- Tralalero is an independent project and is not affiliated with OpenAI.
- If you redistribute a build, keep this file and the MIT license with it.
