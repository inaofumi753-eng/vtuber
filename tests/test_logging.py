from pathlib import Path

from core.logging_config import configure_logging, shutdown_logging


def test_logging_creates_file_and_writes_message(tmp_path: Path) -> None:
    logger = configure_logging("INFO", tmp_path)
    logger.info("test log message")

    for handler in logger.handlers:
        handler.flush()

    log_path = tmp_path / "vtuber.log"
    assert log_path.exists()
    assert "test log message" in log_path.read_text(encoding="utf-8")

    shutdown_logging()


def test_logging_is_idempotent(tmp_path: Path) -> None:
    logger = configure_logging("INFO", tmp_path)
    configure_logging("DEBUG", tmp_path)

    managed_handlers = [
        handler for handler in logger.handlers
        if getattr(handler, "_vtuber_bot_handler", False)
    ]
    assert len(managed_handlers) == 2
    assert logger.level == 10

    shutdown_logging()
