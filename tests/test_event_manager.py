import logging

from core.event_manager import Event, EventManager


def test_event_reaches_registered_handler() -> None:
    manager = EventManager(logging.getLogger("test-event"))
    received: list[object] = []

    manager.subscribe("test", lambda event: received.append(event.payload))
    manager.emit(Event("test", {"value": 42}))

    assert received == [{"value": 42}]


def test_unsubscribe_stops_handler() -> None:
    manager = EventManager()
    received: list[int] = []

    def handler(event: Event) -> None:
        received.append(1)

    manager.subscribe("test", handler)
    manager.unsubscribe("test", handler)
    manager.emit(Event("test"))

    assert received == []


def test_handler_failure_does_not_stop_other_handlers() -> None:
    manager = EventManager()
    received: list[str] = []

    def failing_handler(event: Event) -> None:
        received.append("failed")
        raise RuntimeError("expected failure")

    def good_handler(event: Event) -> None:
        received.append("good")

    manager.subscribe("test", failing_handler)
    manager.subscribe("test", good_handler)
    manager.emit(Event("test"))

    assert received == ["failed", "good"]
