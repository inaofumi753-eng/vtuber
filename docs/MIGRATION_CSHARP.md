# C# / .NET 10 / WPF Migration

V0.1 is being migrated from Python/PySide6 to C#/.NET 10/WPF.

Core is independent of WPF. The WPF project is only the composition root and minimal diagnostic UI. SQLite remains direct through Microsoft.Data.Sqlite and TOML remains the external configuration format through Tomlyn.

Python remains temporarily as the functional reference. No Twitch, STT, TTS, VAD, LLM, avatar runtime, OBS integration, Telegram or V0.2 functionality is activated.

Dependencies:
- Microsoft.Data.Sqlite 10.0.12 — MIT.
- Tomlyn 2.10.1 — BSD-2-Clause.
- xunit.v3 4.0.1 — Apache-2.0.
- Microsoft.NET.Test.Sdk 18.10.1 — MIT.

Windows CI is mandatory for WPF build validation. A successful build does not by itself prove interactive GUI runtime behavior.