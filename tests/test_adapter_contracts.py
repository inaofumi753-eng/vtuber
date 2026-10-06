from typing import cast

from obs.backend_contracts import OBSBackend
from twitch.backend_contracts import TwitchBackend
from vtuber.avatar_contracts import AvatarBackend


class FakeAvatar:
    name = 'fake-avatar'
    def connect(self) -> None: pass
    def disconnect(self) -> None: pass
    def set_parameter(self, parameter: str, value: float, weight: float = 1.0) -> None: pass
    def trigger_hotkey(self, hotkey_id: str) -> None: pass
    def set_expression(self, expression_id: str, enabled: bool = True) -> None: pass

class FakeTwitch:
    name = 'fake-twitch'
    def connect(self) -> None: pass
    def disconnect(self) -> None: pass
    def subscribe(self, event_name: str, handler) -> None: pass
    def send_message(self, channel: str, message: str) -> None: pass

class FakeOBS:
    name = 'fake-obs'
    def connect(self) -> None: pass
    def disconnect(self) -> None: pass
    def request(self, request_type: str, request_data=None) -> dict: return {'requestType': request_type}
    def subscribe(self, event_name: str, handler) -> None: pass

def test_adapter_contracts_accept_matching_implementations() -> None:
    avatar = cast(AvatarBackend, FakeAvatar())
    twitch = cast(TwitchBackend, FakeTwitch())
    obs = cast(OBSBackend, FakeOBS())
    avatar.connect(); avatar.set_parameter('ParamMouthOpenY', 0.5); avatar.trigger_hotkey('hello'); avatar.set_expression('smile')
    twitch.connect(); twitch.subscribe('chat.message', lambda event: None); twitch.send_message('#channel', 'hello')
    obs.connect(); result = obs.request('GetSceneList'); assert result['requestType'] == 'GetSceneList'; obs.subscribe('SceneItemEnableStateChanged', lambda event: None)
    avatar.disconnect(); twitch.disconnect(); obs.disconnect()