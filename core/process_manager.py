"""Safe lifecycle management for child processes owned by the Bot."""

from __future__ import annotations

from dataclasses import dataclass
import logging
from pathlib import Path
import os
import subprocess
import threading
from collections.abc import Mapping, Sequence

class ProcessManagerError(RuntimeError):
    """Raised for invalid or failed owned-process operations."""

@dataclass(frozen=True)
class ProcessSpec:
    """Immutable specification for one child process."""

    command: tuple[str, ...]
    cwd: Path | None = None
    env: Mapping[str, str] | None = None

    @classmethod
    def from_command(cls, command: Sequence[str], *, cwd: str | Path | None = None, env: Mapping[str, str] | None = None) -> 'ProcessSpec':
        items = tuple(str(item) for item in command)
        if not items:
            raise ValueError("command must contain at least one item.")
        if not items[0].strip():
            raise ValueError("command executable must not be empty.")
        resolved_cwd = Path(cwd) if cwd is not None else None
        resolved_env = dict(env) if env is not None else None
        return cls(items, resolved_cwd, resolved_env)

class OwnedProcess:
    """Own exactly one subprocess and never manage arbitrary external PIDs."""

    def __init__(self, spec: ProcessSpec, logger: logging.Logger | None = None) -> None:
        self._spec = spec
        self._logger = logger or logging.getLogger("vtuber")
        self._process: subprocess.Popen[bytes] | None = None
        self._lock = threading.RLock()

    @property
    def pid(self) -> int | None:
        with self._lock:
            return self._process.pid if self._process is not None else None

    @property
    def return_code(self) -> int | None:
        with self._lock:
            return self._process.poll() if self._process is not None else None

    @property
    def is_running(self) -> bool:
        return self.return_code is None and self.pid is not None

    def start(self) -> int:
        """Start the owned child process and return its PID."""
        with self._lock:
            if self._process is not None and self._process.poll() is None:
                raise ProcessManagerError("Process is already running.")

            cwd = str(self._spec.cwd) if self._spec.cwd is not None else None
            if self._spec.cwd is not None and not self._spec.cwd.is_dir():
                raise ProcessManagerError(f'Working directory does not exist: {self._spec.cwd}')

            env = os.environ.copy()
            if self._spec.env is not None:
                env.update(self._spec.env)

            creationflags = 0
            if os.name == 'nt':
                creationflags = getattr(subprocess, 'CREATE_NEW_PROCESS_GROUP', 0)

            try:
                self._process = subprocess.Popen(
                    self._spec.command,
                    cwd=cwd,
                    env=env,
                    shell=False,
                    stdin=subprocess.DEVNULL,
                    stdout=subprocess.DEVNULL,
                    stderr=subprocess.DEVNULL,
                    creationflags=creationflags,
                )
            except OSError as exc:
                self._process = None
                raise ProcessManagerError(f'Failed to start process: {self._spec.command[0]}') from exc

            self._logger.info('Owned process started: pid=%s executable=%s', self._process.pid, self._spec.command[0])
            return self._process.pid

    def wait(self, timeout: float | None = None) -> int | None:
        """Wait for the owned process and return its exit code."""
        with self._lock:
            process = self._process
        if process is None:
            return None
        try:
            return process.wait(timeout=timeout)
        except subprocess.TimeoutExpired:
            return None

    def stop(self, timeout: float = 5.0) -> int | None:
        """Stop only the child process owned by this object."""
        if timeout < 0:
            raise ValueError("timeout must be non-negative.")
        with self._lock:
            process = self._process
            if process is None:
                return None
            if process.poll() is not None:
                return_code = process.returncode
                self._process = None
                return return_code
            process.terminate()
        try:
            return_code = process.wait(timeout=timeout)
        except subprocess.TimeoutExpired:
            self._logger.warning('Owned process did not terminate in %.2fs; killing pid=%s', timeout, process.pid)
            process.kill()
            return_code = process.wait()
        with self._lock:
            if self._process is process:
                self._process = None
        self._logger.info('Owned process stopped: pid=%s return_code=%s', process.pid, return_code)
        return return_code

    def restart(self, stop_timeout: float = 5.0) -> int:
        """Stop the owned process if needed, then start it again."""
        self.stop(timeout=stop_timeout)
        return self.start()