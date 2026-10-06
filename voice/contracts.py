"""Provider-neutral contracts for local voice engines."""

from __future__ import annotations

from dataclasses import dataclass
from enum import IntEnum
from pathlib import Path
from typing import Protocol


class SpeechPriority(IntEnum):
    """Priority used later by the speech queue."""

    LOW = 10
    NORMAL = 50
    HIGH = 80
    INTERRUPT = 100


@dataclass(frozen=True)
class SpeechRequest:
    """Immutable request passed from the conversation layer to a TTS backend."""

    text: str
    voice: str | None = None
    priority: SpeechPriority = SpeechPriority.NORMAL

    def __post_init__(self) -> None:
        if not self.text.strip():
            raise ValueError('text must not be empty.')


class TTSBackend(Protocol):
    """Synchronous contract for a local text-to-speech backend."""

    name: str

    def synthesize(
        self,
        request: SpeechRequest,
        output_path: Path,
    ) -> Path:
        """Generate audio and return the resulting file path."""


class STTBackend(Protocol):
    """Contract for speech-to-text engines."""

    name: str

    def transcribe(self, audio_path: Path) -> str:
        """Transcribe one audio file and return normalized text."""


class VADBackend(Protocol):
    """Contract for voice-activity detectors."""

    name: str

    def is_speech(self, audio_frame: bytes, sample_rate: int) -> bool:
        """Return True when the supplied audio frame contains speech."""