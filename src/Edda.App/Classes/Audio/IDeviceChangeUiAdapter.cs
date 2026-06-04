using System;

namespace Edda {
    public interface IDeviceChangeUiAdapter {
        bool PlayingOnDefaultDevice { get; }
        bool DefaultDeviceAvailable { get; }
        string UserPreferredPlaybackDeviceId { get; }
        string PlaybackDeviceId { get; }

        void InvokeOnUiThread(Action action);
        void UpdatePlaybackDevice(string playbackDeviceId, bool useDefaultDevice);
    }
}