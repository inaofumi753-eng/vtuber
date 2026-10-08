# VTuber Bot

Local-first Windows VTuber Bot foundation.

## V0.1 C# migration

The approved migration target is **C# / .NET 10 / WPF / SQLite / Windows-first**.

The Python/PySide6 V0.1 remains temporarily as the functional reference. It has not been removed and is not used as a second runtime.

The C# foundation contains configuration, logging, SQLite, lifecycle, EventManager, one-shot TaskScheduler, ResourceManager, owned process management, provider-neutral Voice/Avatar/Twitch/OBS contracts and a minimal WPF control panel. V0.2.0 adds the local Command Core with `help`, `status` and `ping`, plus command execution and lifecycle controls in WPF.

V0.2.0 is limited to the local Control Plane + Command Core. Twitch, STT, TTS, VAD, LLM, avatar runtime, OBS integration, Telegram and V0.3 functionality are not activated. V0.2.1 adds local performance and health monitoring.

## V0.2.0 status

V0.1, V0.2.0 and V0.2.1 are closed and integrated in `main`. V0.2.2 SQLite Concurrency Hardening is currently under implementation.

## Build

```text
dotnet restore VtuberBot.sln
dotnet build VtuberBot.sln --configuration Release
dotnet test VtuberBot.sln --configuration Release
```

Run the WPF application:

```text
dotnet run --project src/VtuberBot.App --configuration Release
```

Windows CI performs restore, Release build and tests on `windows-latest`.

## Configuration

`config.toml` remains optional. Without it, safe defaults are used. Relative database paths are resolved from the configuration file directory when a configuration file is supplied.

## Python reference

Python remains until a separate validation/cleanup task removes the legacy implementation.