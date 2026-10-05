"""Thin Windows desktop entry point for VTuber Bot V0.1."""

from __future__ import annotations

import sys

from PySide6.QtWidgets import QApplication

from core.application import Application, ApplicationError
from panel.main_window import MainWindow


def main() -> int:
    application = Application()

    try:
        application.initialize()
    except ApplicationError as exc:
        application.shutdown()
        print(f"VTuber Bot could not start: {exc}", file=sys.stderr)
        return 1

    try:
        qt_application = QApplication(sys.argv)
        window = MainWindow(application)
        qt_application.aboutToQuit.connect(application.shutdown)
        window.show()
        return qt_application.exec()
    except Exception as exc:
        print(f"VTuber Bot GUI failed: {exc}", file=sys.stderr)
        return 1
    finally:
        application.shutdown()


if __name__ == "__main__":
    raise SystemExit(main())
