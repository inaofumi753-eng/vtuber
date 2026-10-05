"""Application lifecycle states."""

from enum import Enum


class AppState(str, Enum):
    """Minimal lifecycle states for the application."""

    STARTING = "STARTING"
    RUNNING = "RUNNING"
    STOPPING = "STOPPING"
    STOPPED = "STOPPED"
    ERROR = "ERROR"
