using System;

public interface IAudioCuePlayer : IDisposable {
    bool isEnabled { get; set; }
    bool isPanned { get; set; }
    bool Play();
    bool Play(int channel);
    void ChangeVolume(double vol);
}