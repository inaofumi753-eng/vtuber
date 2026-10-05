from pathlib import Path

from core.app_state import AppState
from core.application import Application


def test_startup_smoke_without_gui(tmp_path: Path) -> None:
    config_path = tmp_path / "config.toml"
    config_path.write_text(
        "[app]\nname = 'Smoke Bot'\n[database]\npath = 'data/smoke.sqlite3'\n",
        encoding="utf-8",
    )

    application = Application(config_path)
    application.initialize()

    assert application.state is AppState.RUNNING
    assert application.internal_test_events_processed == 1
    assert (tmp_path / "data" / "smoke.sqlite3").exists()
    assert (tmp_path / "logs" / "vtuber.log").exists()

    application.shutdown()
    assert application.state is AppState.STOPPED
