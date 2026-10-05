"""Small synchronous event dispatcher for V0.1 Foundation."""

from __future__ import annotations

from dataclasses import dataclass
import logging
from typing import Any, Callable


@dataclass(frozen=True)
class Event:
    """An internal event with a name and optional payload."""

    name: str
    payload: Any = None

    def __post_init__(self) -> None:
        if not isinstance(self.name, str) or not self.name.strip():
            raise ValueError("Event name must be a non-empty string.")


EventHandler = Callable[[Event], None]


class EventManager:
    """Synchronous event dispatcher that isolates handler failures."""

    def __init__(self, logger: logging.Logger | None = None) -> None:
        self._handlers: dict[str, list[EventHandler]] = {}
        self._logger = logger or logging.getLogger("vtuber")

    def subscribe(self, event_name: str, handler: EventHandler) -> None:
        if not isinstance(event_name, str) or not event_name.strip():
            raise ValueError("event_name must be a non-empty string.")
        if not callable(handler):
            raise TypeError("handler must be callable.")
        handlers = self._handlers.setdefault(event_name, [])
        if handler not in handlers:
            handlers.append(handler)

    def unsubscribe(self, event_name: str, handler: EventHandler) -> None:
        handlers = self._handlers.get(event_name)
        if not handlers:
            return
        if handler in handlers:
            handlers.remove(handler)
        if not handlers:
            self._handlers.pop(event_name, None)

    def emit(self, event: Event) -> None:
        if not isinstance(event, Event):
            raise TypeError("emit() expects an Event instance.")
        for handler in tuple(self._handlers.get(event.name, ())):
            try:
                handler(event)
            except Exception:
                self._logger.exception(
                    "Event handler failed for %s: %r", event.name, handler
                )
