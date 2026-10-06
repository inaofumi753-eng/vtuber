"""Provider-neutral Twitch adapter contracts."""

from __future__ import annotations

from collections.abc import Callable
from typing import Any, Protocol

TwitchEventHandler = Callable[[dict[str, Any]], None]

class TwitchBackend(Protocol):
    """Boundary for Twitch connectivity and event delivery."""

    name: str

    def connect(self) -> None: ...
    def disconnect(self) -> None: ...
    def subscribe(self, event_name: str, handler: TwitchEventHandler) -> None: ...
    def send_message(self, channel: str, message: str) -> None: ...