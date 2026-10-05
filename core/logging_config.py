"""Standard-library logging configuration for V0.1 Foundation."""

from __future__ import annotations

from pathlib import Path
import logging
from logging.handlers import RotatingFileHandler


_HANDLER_MARKER = "_vtuber_bot_handler"
_FORMAT = "%(asctime)s | %(levelname)s | %(name)s | %(message)s"
_MAX_BYTES = 1_048_576
_BACKUP_COUNT = 2


def configure_logging(
    level: int | str,
    log_directory: str | Path,
    logger_name: str = "vtuber",
) -> logging.Logger:
    """Configure one console handler and one rotating file handler.

    Only handlers created by this function are managed. External handlers,
    such as pytest capture handlers, are left untouched.
    """
    log_dir = Path(log_directory).expanduser().resolve()
    log_dir.mkdir(parents=True, exist_ok=True)
    log_path = log_dir / "vtuber.log"
    numeric_level = _normalize_level(level)
    logger = logging.getLogger(logger_name)

    _remove_managed_handlers(logger)

    formatter = logging.Formatter(_FORMAT)

    console_handler = logging.StreamHandler()
    console_handler.setLevel(numeric_level)
    console_handler.setFormatter(formatter)
    setattr(console_handler, _HANDLER_MARKER, True)
    setattr(console_handler, "_vtuber_handler_kind", "console")

    file_handler = RotatingFileHandler(
        log_path,
        maxBytes=_MAX_BYTES,
        backupCount=_BACKUP_COUNT,
        encoding="utf-8",
    )
    file_handler.setLevel(numeric_level)
    file_handler.setFormatter(formatter)
    setattr(file_handler, _HANDLER_MARKER, True)
    setattr(file_handler, "_vtuber_handler_kind", "file")

    logger.setLevel(numeric_level)
    logger.propagate = False
    logger.addHandler(console_handler)
    logger.addHandler(file_handler)
    return logger


def shutdown_logging(logger_name: str = "vtuber") -> None:
    """Close and remove handlers managed by this module."""
    logger = logging.getLogger(logger_name)
    _remove_managed_handlers(logger)


def _remove_managed_handlers(logger: logging.Logger) -> None:
    for handler in list(logger.handlers):
        if getattr(handler, _HANDLER_MARKER, False):
            logger.removeHandler(handler)
            handler.close()


def _normalize_level(level: int | str) -> int:
    if isinstance(level, int):
        return level
    if isinstance(level, str):
        name = level.strip().upper()
        numeric = logging.getLevelName(name)
        if isinstance(numeric, int):
            return numeric
    raise ValueError(f"Invalid logging level: {level!r}")
