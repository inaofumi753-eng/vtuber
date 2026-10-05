"""Minimal one-shot task scheduler."""

from __future__ import annotations

import heapq
import itertools
import logging
from threading import Condition, Thread
import time
from typing import Callable


class TaskScheduler:
    """A single lazy worker for one-shot delayed callbacks."""

    def __init__(self, logger: logging.Logger | None = None) -> None:
        self._logger = logger or logging.getLogger("vtuber")
        self._condition = Condition()
        self._tasks: list[tuple[float, int, int, Callable[[], None]]] = []
        self._scheduled: set[int] = set()
        self._cancelled: set[int] = set()
        self._sequence = itertools.count()
        self._task_ids = itertools.count(1)
        self._worker: Thread | None = None
        self._shutdown = False

    def schedule_once(self, delay: float, callback: Callable[[], None]) -> int:
        """Schedule callback once after delay seconds and return its task id."""
        if delay < 0:
            raise ValueError("delay must be non-negative.")
        if not callable(callback):
            raise TypeError("callback must be callable.")

        with self._condition:
            if self._shutdown:
                raise RuntimeError("TaskScheduler has been shut down.")
            task_id = next(self._task_ids)
            sequence = next(self._sequence)
            deadline = time.monotonic() + delay
            heapq.heappush(
                self._tasks,
                (deadline, sequence, task_id, callback),
            )
            self._scheduled.add(task_id)
            if self._worker is None:
                self._worker = Thread(
                    target=self._worker_loop,
                    name="vtuber-task-scheduler",
                    daemon=False,
                )
                self._worker.start()
            self._condition.notify_all()
            return task_id

    def cancel(self, task_id: int) -> bool:
        """Cancel a pending task. Return True only when it was pending."""
        with self._condition:
            if task_id not in self._scheduled:
                return False
            self._scheduled.remove(task_id)
            self._cancelled.add(task_id)
            self._condition.notify_all()
            return True

    def shutdown(self) -> None:
        """Stop the worker and discard pending tasks."""
        with self._condition:
            worker = self._worker
            if not self._shutdown:
                self._shutdown = True
                self._tasks.clear()
                self._scheduled.clear()
                self._cancelled.clear()
                self._condition.notify_all()

        if worker is not None and worker.is_alive():
            worker.join()
        with self._condition:
            self._worker = None

    def _worker_loop(self) -> None:
        while True:
            with self._condition:
                if self._shutdown:
                    return

                while not self._tasks:
                    self._condition.wait()
                    if self._shutdown:
                        return

                deadline, _sequence, task_id, callback = self._tasks[0]
                if task_id in self._cancelled:
                    heapq.heappop(self._tasks)
                    self._cancelled.discard(task_id)
                    continue

                remaining = deadline - time.monotonic()
                if remaining > 0:
                    self._condition.wait(timeout=remaining)
                    continue

                heapq.heappop(self._tasks)
                self._scheduled.discard(task_id)

            try:
                callback()
            except Exception:
                self._logger.exception("Scheduled task %s failed", task_id)
