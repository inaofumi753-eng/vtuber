from core.resource_manager import ResourceManager


def test_cleanup_runs_in_reverse_order() -> None:
    manager = ResourceManager()
    order: list[str] = []

    manager.register("first", lambda: order.append("first"))
    manager.register("second", lambda: order.append("second"))
    manager.register("third", lambda: order.append("third"))
    manager.cleanup_all()

    assert order == ["third", "second", "first"]


def test_cleanup_is_idempotent_and_continues_after_failure() -> None:
    manager = ResourceManager()
    order: list[str] = []

    def failing_cleanup() -> None:
        order.append("failing")
        raise RuntimeError("expected cleanup failure")

    manager.register("first", lambda: order.append("first"))
    manager.register("failing", failing_cleanup)
    manager.register("last", lambda: order.append("last"))

    manager.cleanup_all()
    manager.cleanup_all()

    assert order == ["last", "failing", "first"]
