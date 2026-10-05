from pathlib import Path

import pytest

from core.app_state import AppState
from core.application import Application, ApplicationError
from database.sqlite import SQLiteDatabase


def test_application_lifecycle_and_internal_event(tmp_path: Path) -> None:
    config_path = tmp_path / "config.toml"
    config_path.write_text(
        "[database]\npath = 'data/app.sqlite3'\n[logging]\nlevel = 'INFO'\n",
        encoding="utf-8",
    )
    application = Application(config_path)

    assert application.state is AppState.STOPPED
    application.initialize()

    assert application.state is AppState.RUNNING
    assert application.internal_test_events_processed == 1
    assert application.database.path == (tmp_path / "data" / "app.sqlite3").resolve()

    log_text = (tmp_path / "logs" / "vtuber.log").read_text(encoding="utf-8")
    assert log_text.count("Internal test event processed") == 1

    application.shutdown()
    assert application.state is AppState.STOPPED
    application.shutdown()


def test_application_enters_error_on_critical_initialization_failure(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    config_path = tmp_path / "config.toml"
    config_path.write_text(
        "[database]\npath = 'data/app.sqlite3'\n",
        encoding="utf-8",
    )

    def fail_open(self: SQLiteDatabase) -> None:
        raise RuntimeError("database failure")

    monkeypatch.setattr(SQLiteDatabase, "open", fail_open)

    application = Application(config_path)
    with pytest.raises(ApplicationError):
        application.initialize()

    assert application.state is AppState.ERROR
    application.shutdown()
    assert application.state is AppState.STOPPED
