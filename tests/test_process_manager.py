import sys

import pytest

from core.process_manager import OwnedProcess, ProcessManagerError, ProcessSpec

def _sleep_command(seconds: float) -> tuple[str, ...]:
    return (sys.executable, "-c", f"import time; time.sleep({seconds})")

def test_process_spec_rejects_empty_command() -> None:
    with pytest.raises(ValueError):
        ProcessSpec.from_command([])

def test_owned_process_starts_and_stops() -> None:
    process = OwnedProcess(ProcessSpec.from_command(_sleep_command(10)))
    pid = process.start()
    try:
        assert pid > 0
        assert process.is_running
        assert process.pid == pid
    finally:
        process.stop(timeout=2)
    assert not process.is_running
    assert process.pid is None

def test_owned_process_cannot_start_twice() -> None:
    process = OwnedProcess(ProcessSpec.from_command(_sleep_command(10)))
    process.start()
    try:
        with pytest.raises(ProcessManagerError):
            process.start()
    finally:
        process.stop(timeout=2)

def test_owned_process_wait_reports_exit_code() -> None:
    process = OwnedProcess(ProcessSpec.from_command((sys.executable, "-c", "raise SystemExit(7)")))
    process.start()
    assert process.wait(timeout=3) == 7

def test_owned_process_rejects_missing_cwd() -> None:
    process = OwnedProcess(ProcessSpec.from_command(_sleep_command(1), cwd="Z:\\definitely_missing_vtuber_bot_dir"))
    with pytest.raises(ProcessManagerError):
        process.start()

def test_owned_process_restart() -> None:
    process = OwnedProcess(ProcessSpec.from_command(_sleep_command(10)))
    first = process.start()
    try:
        second = process.restart(stop_timeout=2)
        assert second > 0
        assert second != first
        assert process.is_running
    finally:
        process.stop(timeout=2)