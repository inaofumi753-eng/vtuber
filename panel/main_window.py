"""Minimal V0.1 PySide6 main window."""

from __future__ import annotations

from PySide6.QtCore import Qt
from PySide6.QtWidgets import QLabel, QMainWindow, QVBoxLayout, QWidget

from core.application import Application


class MainWindow(QMainWindow):
    """Present the current foundation status without core business logic."""

    def __init__(self, application: Application) -> None:
        super().__init__()
        self._application = application
        self.setWindowTitle(application.config.name)
        self.setMinimumSize(360, 160)

        central = QWidget(self)
        layout = QVBoxLayout(central)

        title = QLabel("VTuber Bot")
        title.setAlignment(Qt.AlignCenter)
        title.setStyleSheet("font-size: 22px; font-weight: bold;")

        version = QLabel("V0.1 Foundation")
        version.setAlignment(Qt.AlignCenter)

        status = QLabel(f"Estado: {application.state.value}")
        status.setAlignment(Qt.AlignCenter)

        layout.addWidget(title)
        layout.addWidget(version)
        layout.addWidget(status)
        self.setCentralWidget(central)
