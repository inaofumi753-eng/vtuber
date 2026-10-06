"""Provider-neutral OBS adapter contracts."""

from __future__ import annotations

from collections.abc import Callable
from typing import Any, Protocol

OBSEventHandler = Callable[[dict[str, Any]], None]

class OBSBackend(Protocol):
    """Boundary for an external OBS Studio controller."""

    name: str

    def connect(self) -> None: ...
    def disconnect(self) -> None: ...
    def request(self, request_type: str, request_data: dict[str, Any] | None = None) -> dict[str, Any]: ...
    def subscribe(self, event_name: str, handler: OBSEventHandler) -> None: ...