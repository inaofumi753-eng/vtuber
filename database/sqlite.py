"""Minimal SQLite layer using only the Python standard library."""

from __future__ import annotations

from pathlib import Path
import sqlite3
from typing import Any, Iterable


SCHEMA_VERSION = 1


class SQLiteDatabase:
    """Own one SQLite connection and the V0.1 technical schema."""

    def __init__(self, path: str | Path) -> None:
        self.path = Path(path).expanduser().resolve()
        self._connection: sqlite3.Connection | None = None

    @property
    def connection(self) -> sqlite3.Connection | None:
        """Return the owned connection, or None when closed."""
        return self._connection

    def open(self) -> None:
        if self._connection is not None:
            return
        self.path.parent.mkdir(parents=True, exist_ok=True)
        self._connection = sqlite3.connect(self.path)
        self._connection.execute("PRAGMA foreign_keys = ON")

    def initialize_schema(self) -> None:
        connection = self._require_connection()
        connection.execute(
            """
            CREATE TABLE IF NOT EXISTS schema_meta (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            )
            """
        )
        connection.execute(
            "INSERT OR REPLACE INTO schema_meta (key, value) VALUES (?, ?)",
            ("schema_version", str(SCHEMA_VERSION)),
        )
        connection.commit()

    def execute(
        self,
        sql: str,
        parameters: Iterable[Any] = (),
    ) -> sqlite3.Cursor:
        """Execute SQL and commit the transaction."""
        connection = self._require_connection()
        cursor = connection.execute(sql, tuple(parameters))
        connection.commit()
        return cursor

    def close(self) -> None:
        if self._connection is None:
            return
        self._connection.close()
        self._connection = None

    def _require_connection(self) -> sqlite3.Connection:
        if self._connection is None:
            raise RuntimeError("SQLite database is not open.")
        return self._connection
