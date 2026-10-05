from threading import Event as ThreadEvent

from core.task_scheduler import TaskScheduler


def test_scheduler_worker_is_lazy_and_task_runs() -> None:
    scheduler = TaskScheduler()
    completed = ThreadEvent()

    assert scheduler._worker is None
    scheduler.schedule_once(0.01, completed.set)
    assert scheduler._worker is not None
    assert completed.wait(1.0)

    scheduler.shutdown()
    assert scheduler._worker is None


def test_scheduler_cancels_pending_task() -> None:
    scheduler = TaskScheduler()
    completed = ThreadEvent()

    task_id = scheduler.schedule_once(0.2, completed.set)
    assert scheduler.cancel(task_id) is True
    assert scheduler.cancel(task_id) is False
    assert not completed.wait(0.35)

    scheduler.shutdown()


def test_scheduler_shutdown_is_idempotent() -> None:
    scheduler = TaskScheduler()
    scheduler.shutdown()
    scheduler.shutdown()

    assert scheduler._worker is None
