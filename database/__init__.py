"""SQLite persistence layer for VTuber Bot V0.1."""

from .sqlite import SCHEMA_VERSION, SQLiteDatabase

__all__ = ["SCHEMA_VERSION", "SQLiteDatabase"]
