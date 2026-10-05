"""Configuration loading for V0.1 Foundation."""

from __future__ import annotations

from dataclasses import dataclass
import logging
from pathlib import Path
import tomllib
from typing import Any


class ConfigError(ValueError):
    """Raised when the application configuration is invalid."""


_PROJECT_ROOT = Path(__file__).resolve().parents[1]
_DEFAULT_DATABASE = Path("data") / "vtuber.sqlite3"
_LOG_LEVELS = {
    "CRITICAL": logging.CRITICAL,
    "ERROR": logging.ERROR,
    "WARNING": logging.WARNING,
    "INFO": logging.INFO,
    "DEBUG": logging.DEBUG,
}


@dataclass(frozen=True)
class AppConfig:
    """Validated V0.1 configuration and deterministic derived paths."""

    name: str
    database_path: Path
    log_level: int
    base_dir: Path
    config_path: Path | None

    @property
    def log_directory(self) -> Path:
        """Return the directory used for application logs."""
        return self.base_dir / "logs"

    @classmethod
    def load(cls, config_path: str | Path | None = None) -> AppConfig:
        """Load config.toml or safe defaults when the file is absent.

        Relative paths are resolved from the directory containing config.toml.
        With no config file, the repository root is used as the deterministic
        base directory.
        """
        if config_path is None:
            path = _PROJECT_ROOT / "config.toml"
            base_dir = _PROJECT_ROOT
        else:
            path = Path(config_path).expanduser().resolve()
            base_dir = path.parent

        data: dict[str, Any] = {}
        if path.exists():
            try:
                with path.open("rb") as file:
                    parsed = tomllib.load(file)
            except tomllib.TOMLDecodeError as exc:
                raise ConfigError(f"Invalid TOML in {path}: {exc}") from exc
            if not isinstance(parsed, dict):
                raise ConfigError("Configuration root must be a TOML table.")
            data = parsed

        app_section = _get_table(data, "app")
        database_section = _get_table(data, "database")
        logging_section = _get_table(data, "logging")

        name = app_section.get("name", "VTuber Bot")
        if not isinstance(name, str) or not name.strip():
            raise ConfigError("[app].name must be a non-empty string.")

        database_value = database_section.get("path", str(_DEFAULT_DATABASE))
        if not isinstance(database_value, str) or not database_value.strip():
            raise ConfigError("[database].path must be a non-empty string.")
        database_path = Path(database_value).expanduser()
        if not database_path.is_absolute():
            database_path = base_dir / database_path
        database_path = database_path.resolve()

        level_value = logging_section.get("level", "INFO")
        log_level = _parse_log_level(level_value)

        return cls(
            name=name.strip(),
            database_path=database_path,
            log_level=log_level,
            base_dir=base_dir,
            config_path=path if path.exists() else None,
        )


def _get_table(data: dict[str, Any], name: str) -> dict[str, Any]:
    value = data.get(name, {})
    if not isinstance(value, dict):
        raise ConfigError(f"[{name}] must be a TOML table.")
    return value


def _parse_log_level(value: Any) -> int:
    if not isinstance(value, str):
        raise ConfigError("[logging].level must be a logging level name.")
    level = _LOG_LEVELS.get(value.strip().upper())
    if level is None:
        valid = ", ".join(_LOG_LEVELS)
        raise ConfigError(f"Invalid logging level {value!r}. Use one of: {valid}.")
    return level
