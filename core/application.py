"""Application orchestration for V0.1 Foundation."""

from __future__ import annotations

import logging
from pathlib import Path

from database.sqlite import SQLiteDatabase

from .app_state import AppState
from .config import AppConfig, ConfigError
from .event_manager import Event, EventManager
from .logging_config import configure_logging, shutdown_logging
from .resource_manager import ResourceManager
from .task_scheduler import TaskScheduler


INTERNAL_TEST_EVENT = "internal.test"


class ApplicationError(RuntimeError):
    """Raised when critical application initialization fails."""


class Application:
    """Coordinate the V0.1 components without owning GUI logic."""

    def __init__(self, config_path: str | Path | None = None) -> None:
        self._config_path = config_path
        self._state = AppState.STOPPED
        self._config: AppConfig | None = None
        self._database: SQLiteDatabase | None = None
        self._event_manager: EventManager | None = None
        self._task_scheduler: TaskScheduler | None = None
        self._resource_manager: ResourceManager | None = None
        self._logger = logging.getLogger("vtuber")
        self._internal_test_events_processed = 0

    @property
    def state(self) -> AppState:
        return self._state

    @property
    def config(self) -> AppConfig:
        if self._config is None:
            raise RuntimeError("Application has not been initialized.")
        return self._config

    @property
    def database(self) -> SQLiteDatabase:
        if self._database is None:
            raise RuntimeError("Application database is not initialized.")
        return self._database

    @property
    def event_manager(self) -> EventManager:
        if self._event_manager is None:
            raise RuntimeError("EventManager is not initialized.")
        return self._event_manager

    @property
    def task_scheduler(self) -> TaskScheduler:
        if self._task_scheduler is None:
            raise RuntimeError("TaskScheduler is not initialized.")
        return self._task_scheduler

    @property
    def resource_manager(self) -> ResourceManager:
        if self._resource_manager is None:
            raise RuntimeError("ResourceManager is not initialized.")
        return self._resource_manager

    @property
    def internal_test_events_processed(self) -> int:
        return self._internal_test_events_processed

    def initialize(self) -> None:
        if self._state is not AppState.STOPPED:
            raise RuntimeError(f"Cannot initialize from state {self._state.value}.")

        self._state = AppState.STARTING
        self._resource_manager = ResourceManager()

        try:
            self._config = AppConfig.load(self._config_path)
            self._logger = configure_logging(
                self._config.log_level,
                self._config.log_directory,
            )
            self._resource_manager.register("logging", shutdown_logging)

            self._database = SQLiteDatabase(self._config.database_path)
            self._database.open()
            self._database.initialize_schema()
            self._resource_manager.register("database", self._database.close)

            self._event_manager = EventManager(self._logger)
            self._task_scheduler = TaskScheduler(self._logger)
            self._resource_manager.register(
                "task_scheduler", self._task_scheduler.shutdown
            )

            self._event_manager.subscribe(
                INTERNAL_TEST_EVENT,
                self._handle_internal_test_event,
            )
            self._event_manager.emit(Event(INTERNAL_TEST_EVENT))
            self._state = AppState.RUNNING
            self._logger.info("Application state: %s", self._state.value)
        except Exception as exc:
            if self._logger.handlers:
                self._logger.exception("Application initialization failed")
            self._state = AppState.ERROR
            if self._resource_manager is not None:
                self._resource_manager.cleanup_all()
            if isinstance(exc, ConfigError):
                raise ApplicationError(str(exc)) from exc
            raise ApplicationError("Application initialization failed.") from exc

    def shutdown(self) -> None:
        if self._state is AppState.STOPPED:
            return

        self._state = AppState.STOPPING
        resource_manager = self._resource_manager
        try:
            if resource_manager is not None:
                resource_manager.cleanup_all()
        finally:
            self._state = AppState.STOPPED
            self._database = None
            self._event_manager = None
            self._task_scheduler = None
            self._resource_manager = None
            self._config = None

    def _handle_internal_test_event(self, event: Event) -> None:
        self._internal_test_events_processed += 1
        self._logger.info("Internal test event processed")
