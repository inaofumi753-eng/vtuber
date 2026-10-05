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

## V0.2 Performance Core
- CPU/RAM monitoring
- GPU monitoring where viable
- avatar FPS measurement
- Ahorro/Equilibrado/Calidad
- adaptive FPS and internal metrics

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
Critical features stable, tests green, Gemini Inspector review completed, critical/high issues resolved, documentation current, simple configuration, useful logs, recovery and acceptable resource usage on the target PC.

## Release rule
Do not advance merely because something works manually. It must be implemented, tested, stable, documented and free of known critical regressions.

Priority: stability -> performance -> recovery -> maintainability -> features -> visual effects.
