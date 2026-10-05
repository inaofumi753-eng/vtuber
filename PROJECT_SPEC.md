# PROJECT SPEC — VTuber Bot

Central specification for the local-first Windows VTuber Bot. Target: Ryzen 5 5600G, 16 GB RAM, Windows and OBS Studio.

## Core constraints
- No AI APIs in the final product.
- GPT/Gemini are development tools only.
- SQLite is the default local database.
- No secrets in source control.
- No closing user programs or changing critical Windows settings without explicit authorization.
- External integrations must be isolated behind adapters.
- Changes must be small, testable and reversible.

## Planned systems
- Twitch commands, automatic messages, points, rankings, giveaways, moderation and minigames.
- Local users, profiles, statistics and history.
- PNG/2.5D avatar with blinking, eyes, breathing, expressions, mouth and basic lip sync.
- Local TTS, audio queue, emotional profiles and prerecorded reactions.
- PySide6 desktop control panel.
- Telegram Web as an interface, not the system brain.
- Transparent avatar window captured by OBS.
- CPU/RAM/GPU monitoring and adaptive Ahorro/Equilibrado/Calidad profiles.

## Architecture
core / twitch / commands / giveaways / points / rankings / moderation / minigames / automation / vtuber / voice / telegram / obs / performance / database / panel / tests / main.py.

The system is event-oriented: adapter -> EventManager -> feature handler -> database/avatar/voice/panel -> logs and metrics.

## Database
SQLite will contain users, points, rankings, inventory, statistics, giveaways, game state, configuration and event history. Database access is isolated from business logic.

## Testing
Unit tests, event integration tests, SQLite persistence tests, queue tests, recovery tests and startup smoke tests.

## Development flow
GPT Cerebro designs -> GPT Obrero implements -> tests -> Gemini Inspector reviews -> Cerebro decides -> Obrero corrects -> tests -> release.

## Open decisions
Twitch method under the no-API constraint, local TTS engine, final 2.5D renderer, Telegram integration method, optional OBS control, avatar asset format and packaging strategy.
