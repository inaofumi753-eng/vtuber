from pathlib import Path

import pytest

from voice.contracts import (
    SpeechPriority,
    SpeechRequest,
    STTBackend,
    TTSBackend,
    VADBackend,
)


def test_speech_request_defaults() -> None:
    request = SpeechRequest('hello')
    assert request.voice is None
    assert request.priority is SpeechPriority.NORMAL


def test_speech_request_rejects_blank_text() -> None:
    with pytest.raises(ValueError):
        SpeechRequest('   ')


def test_priority_order_is_explicit() -> None:
    assert SpeechPriority.LOW < SpeechPriority.NORMAL < SpeechPriority.HIGH < SpeechPriority.INTERRUPT


class FakeTTS:
    name = 'fake-tts'

    def synthesize(self, request: SpeechRequest, output_path: Path) -> Path:
        output_path.write_bytes(request.text.encode())
        return output_path


class FakeSTT:
    name = 'fake-stt'

    def transcribe(self, audio_path: Path) -> str:
        return audio_path.read_text()


class FakeVAD:
    name = 'fake-vad'

    def is_speech(self, audio_frame: bytes, sample_rate: int) -> bool:
        return bool(audio_frame) and sample_rate > 0


def test_backend_protocols_are_usable() -> None:
    tts: TTSBackend = FakeTTS()
    stt: STTBackend = FakeSTT()
    vad: VADBackend = FakeVAD()

    request = SpeechRequest('hello', priority=SpeechPriority.HIGH)
    output = Path('test-output.wav')
    try:
        assert tts.synthesize(request, output) == output
        output.write_text('transcribed')
        assert stt.transcribe(output) == 'transcribed'
        assert vad.is_speech(b'audio', 16000)
    finally:
        output.unlink(missing_ok=True)