"""Resource cleanup management for V0.1 Foundation."""

from __future__ import annotations

import logging
from typing import Callable


Cleanup = Callable[[], None]


class ResourceManager:
    """Register cleanups and execute them safely in reverse order."""

    def __init__(self, logger: logging.Logger | None = None) -> None:
        self._logger = logger or logging.getLogger("vtuber")
        self._resources: dict[str, Cleanup] = {}
        self._cleaned = False

    def register(self, name: str, cleanup: Cleanup) -> None:
        if self._cleaned:
            raise RuntimeError("Cannot register resources after cleanup.")
        if not isinstance(name, str) or not name.strip():
            raise ValueError("Resource name must be a non-empty string.")
        if not callable(cleanup):
            raise TypeError("cleanup must be callable.")
        self._resources[name] = cleanup

    def cleanup_all(self) -> None:
        if self._cleaned:
            return
        self._cleaned = True

        for name, cleanup in reversed(tuple(self._resources.items())):
            try:
                cleanup()
            except Exception:
                self._logger.exception("Cleanup failed for resource %s", name)
        self._resources.clear()
