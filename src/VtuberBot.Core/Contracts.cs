namespace VtuberBot.Core;

public enum SpeechPriority
{
    Low = 10,
    Normal = 50,
    High = 80,
    Interrupt = 100
}

public sealed record SpeechRequest
{
    public SpeechRequest(string text, string? voice = null, SpeechPriority priority = SpeechPriority.Normal)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text must not be empty.", nameof(text));

        Text = text;
        Voice = voice;
        Priority = priority;
    }

    public string Text { get; }
    public string? Voice { get; }
    public SpeechPriority Priority { get; }
}

public interface ITtsBackend
{
    string Name { get; }
    string Synthesize(SpeechRequest request, string path);
}

public interface ISttBackend
{
    string Name { get; }
    string Transcribe(string path);
}

public interface IVadBackend
{
    string Name { get; }
    bool IsSpeech(byte[] frame, int rate);
}

public interface IAvatarBackend
{
    string Name { get; }
    void Connect();
    void Disconnect();
    void SetParameter(string parameter, float value, float weight = 1);
    void TriggerHotkey(string id);
    void SetExpression(string id, bool enabled = true);
}

public interface ITwitchBackend
{
    string Name { get; }
    void Connect();
    void Disconnect();
    void Subscribe(string eventName, Action<IReadOnlyDictionary<string, object?>> handler);
    void SendMessage(string channel, string message);
}

public interface IObsBackend
{
    string Name { get; }
    void Connect();
    void Disconnect();
    IReadOnlyDictionary<string, object?> Request(string type, IReadOnlyDictionary<string, object?>? data = null);
    void Subscribe(string eventName, Action<IReadOnlyDictionary<string, object?>> handler);
}
