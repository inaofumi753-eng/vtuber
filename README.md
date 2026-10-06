# VTuber Bot

Local-first desktop VTuber Bot foundation for Windows.

Target: Ryzen 5 5600G, 16 GB RAM and Windows.

## V0.1 Foundation

V0.1 is a small local desktop foundation built around:

- Python 3.11+
- PySide6
- standard-library logging
- SQLite
- synchronous EventManager
- one-shot TaskScheduler with at most one worker
- ResourceManager for deterministic cleanup
- minimal PySide6 panel
- reproducible pytest tests

At startup the application loads optional configuration, initializes logging and SQLite, registers the core resources, processes one internal event named internal.test, and opens the minimal panel. The event handler writes "Internal test event processed" to the log.

V0.1 intentionally does **not** implement Twitch, avatar rendering, TTS, Telegram, OBS integration, performance monitoring, external APIs, or AI runtime dependencies.

## Requirements

- Windows
- Python 3.11 or newer

## Installation

From the repository root:

~~~text
py -3.11 -m venv .venv
.venv\\Scripts\\activate
python -m pip install -r requirements.txt
python -m pip install -r requirements-dev.txt
~~~

config.toml is optional. When absent, safe defaults are used. To create a local configuration, copy config.example.toml to config.toml and edit only local values. The real config file, database and logs are ignored by Git.

## Run

~~~text
python main.py
~~~

The window should show:

~~~text
VTuber Bot
V0.1 Foundation
Estado: RUNNING
~~~

Close the window normally. The scheduler, SQLite connection and registered resources are cleaned up before exit.

## Tests

~~~text
pytest -q
ruff check .
~~~

The startup smoke test does not require a GUI session or any external service.

## Project documentation

- PROJECT_SPEC.md — central long-term technical specification
- ROADMAP.md — phased development plan
- PROMPTS.md — GPT/Gemini development workflow

## Integrated audit-derived foundation

The audit produced two small, dependency-free foundations that are now part of the main codebase:

- `core/process_manager.py` provides `OwnedProcess` for local STT/TTS/tracking engines. It launches only explicit child commands with `shell=False` and manages only the process instance it owns.
- `voice/contracts.py` defines provider-neutral TTS/STT/VAD contracts plus speech priorities.
- `vtuber/avatar_contracts.py` defines the avatar runtime boundary.
- `twitch/backend_contracts.py` defines the Twitch integration boundary.
- `obs/backend_contracts.py` defines the OBS control boundary.

These contracts do not activate external engines in V0.1; they make later integrations replaceable without changing the core.

## Audit history

- `BITACORA_AUDITORIA.md` — permanent record of audited projects, licenses, architectural findings, integration decisions and future re-audit rules.
