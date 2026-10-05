from pathlib import Path

from database.sqlite import SCHEMA_VERSION, SQLiteDatabase


def test_database_creates_schema_and_persists(tmp_path: Path) -> None:
    database_path = tmp_path / "data" / "vtuber.sqlite3"
    database = SQLiteDatabase(database_path)
    database.open()
    database.initialize_schema()

    assert database_path.exists()
    assert database.connection is not None
    row = database.connection.execute(
        "SELECT value FROM schema_meta WHERE key = 'schema_version'"
    ).fetchone()
    assert row == (str(SCHEMA_VERSION),)

    database.execute(
        "INSERT OR REPLACE INTO schema_meta (key, value) VALUES (?, ?)",
        ("test_value", "persisted"),
    )
    database.close()
    database.close()

    reopened = SQLiteDatabase(database_path)
    reopened.open()
    value = reopened.connection.execute(
        "SELECT value FROM schema_meta WHERE key = 'test_value'"
    ).fetchone()
    assert value == ("persisted",)
    reopened.close()
    assert reopened.connection is None
