from pathlib import Path

import pytest

from core.config import AppConfig, ConfigError


def test_defaults_without_config(tmp_path: Path) -> None:
    config = AppConfig.load(tmp_path / "missing.toml")

    assert config.name == "VTuber Bot"
    assert config.log_level > 0
    assert config.database_path == (tmp_path / "data" / "vtuber.sqlite3").resolve()
    assert config.config_path is None


def test_valid_config_and_future_sections(tmp_path: Path) -> None:
    config_path = tmp_path / "config.toml"
    config_path.write_text(
        """
[app]
name = "Test Bot"

[database]
path = "custom/data.sqlite3"

[logging]
level = "DEBUG"

[twitch]
enabled = false

[avatar]
target_fps = 30
""".strip()
        + "\n",
        encoding="utf-8",
    )

    config = AppConfig.load(config_path)

    assert config.name == "Test Bot"
    assert config.database_path == (tmp_path / "custom" / "data.sqlite3").resolve()
    assert config.log_level == 10


def test_invalid_toml_is_rejected(tmp_path: Path) -> None:
    config_path = tmp_path / "config.toml"
    config_path.write_text("[app\nname = 'broken'", encoding="utf-8")

    with pytest.raises(ConfigError, match="Invalid TOML"):
        AppConfig.load(config_path)


@pytest.mark.parametrize(
    "content, message",
    [
        ("[app]\nname = ''\n", "app"),
        ("[logging]\nlevel = 'NOPE'\n", "logging"),
        ("[database]\npath = ''\n", "database"),
        ("[logging]\nlevel = 10\n", "logging"),
    ],
)
def test_invalid_critical_values_are_rejected(
    tmp_path: Path,
    content: str,
    message: str,
) -> None:
    config_path = tmp_path / "config.toml"
    config_path.write_text(content, encoding="utf-8")

    with pytest.raises(ConfigError, match=message):
        AppConfig.load(config_path)
