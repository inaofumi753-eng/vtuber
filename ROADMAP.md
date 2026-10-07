# ROADMAP — VTuber Bot

Development order: core -> data -> panel -> avatar -> Twitch -> community -> voice -> integrations -> hardening -> release.

## V0.1 Foundation
- module skeleton
- main.py
- configuration and logging
- Event Manager, Task Scheduler, Resource Manager
- SQLite
- minimal panel
- smoke tests

Exit: app starts, opens local DB, shows panel and processes an internal test event.

## V0.2.0 Control Plane + Command Core
- local CommandRouter
- help, status and ping
- functional WPF control panel
- local command execution
- basic lifecycle controls
- visible command results and activity
- command/application integration tests

Exit: local application opens, shows the real state, executes help/status/ping, reports controlled failures and can start/stop/restart its lifecycle without external providers.

## V0.2.1 Performance & Health
- CPU/RAM monitoring
- GPU monitoring where viable
- avatar FPS measurement when an avatar exists
- Ahorro/Equilibrado/Calidad
- adaptive FPS and internal metrics

Exit: resource and health information is observable without unnecessary polling or permanent workers.

## V0.2.2 SQLite Concurrency Hardening
- explicit serialization strategy for concurrent database access
- schema migration groundwork
- concurrency and recovery tests

Exit: the database access model is safe for the first genuinely concurrent workload.

## V0.2.3 Stabilization Gate
- full regression
- repeated start/stop cycles
- long-running smoke
- resource review
- documentation update

Exit: V0.2 is stable, documented and ready for the V0.3 avatar phase.

## V0.3 Avatar MVP
- PNG layer renderer
- blinking, eyes, breathing
- basic expressions
- mouth open/closed
- simple animation
- transparent window and OBS capture

## V0.4 Twitch Core
- Twitch adapter
- supported event/message reception
- command router
- replies and automatic messages
- reconnect/error handling

The no-API restriction may limit official Twitch capabilities; document limitations before locking the adapter.

## V0.5 Community Systems
Users, points, rankings, profiles, statistics, inventory, history and persistence.

## V0.6 Games & Giveaways
Giveaways, minigames, rewards, cooldowns, anti-spam limits and game history.

## V0.7 Moderation & Automation
Moderation rules, filters, cooldowns, automatic messages, configurable actions and action history.

## V0.8 Voice
Local TTS, audio queue/priorities, prerecorded sounds, emotional profiles, mouth synchronization and queue recovery.

## V0.9 Telegram & Advanced Panel
Telegram Web interface, event history, statistics, avatar/voice controls and diagnostics.

## V0.10 Hardening
Recovery, reconnection, configuration validation, secret protection, expanded tests, load testing, memory/loop/process review and long-running sessions.

## V1.0 Release
Critical features stable, tests green, GPT Cerebro final review completed, critical/high issues resolved, documentation current, simple configuration, useful logs, recovery and acceptable resource usage on the target PC.

## Release rule
Do not advance merely because something works manually. It must be implemented, tested, stable, documented and free of known critical regressions.

Priority: stability -> performance -> recovery -> maintainability -> features -> visual effects.
