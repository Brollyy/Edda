using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using System;

namespace Edda {
    public class DeviceChangeListener : IMMNotificationClient, IDisposable {
        readonly IDeviceChangeUiAdapter uiAdapter;

        public DeviceChangeListener(IDeviceChangeUiAdapter uiAdapter) {
            this.uiAdapter = uiAdapter;
        }

        public void Dispose() {
        }

        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId) {
            if (flow == DataFlow.Render && role == Role.Multimedia && (uiAdapter.PlayingOnDefaultDevice || !uiAdapter.DefaultDeviceAvailable)) {
                uiAdapter.InvokeOnUiThread(() => {
                    uiAdapter.UpdatePlaybackDevice(defaultDeviceId, true);
                });
            }
        }

        public void OnDeviceAdded(string pwstrDeviceId) {
            if (pwstrDeviceId == uiAdapter.UserPreferredPlaybackDeviceId) {
                // User preferred device was readded, so switch to it.
                uiAdapter.InvokeOnUiThread(() => {
                    uiAdapter.UpdatePlaybackDevice(pwstrDeviceId, false);
                });
            }
        }

        public void OnDeviceRemoved(string deviceId) {
            if (uiAdapter.PlaybackDeviceId == deviceId) {
                // We force an update to default device in this case.
                uiAdapter.InvokeOnUiThread(() => {
                    uiAdapter.UpdatePlaybackDevice(null, true);
                });
            }
        }

        public void OnDeviceStateChanged(string deviceId, DeviceState newState) {
            if (uiAdapter.PlaybackDeviceId == deviceId && newState != DeviceState.Active) {
                // If the current device is not active anymore, we force an update to default device.
                uiAdapter.InvokeOnUiThread(() => {
                    uiAdapter.UpdatePlaybackDevice(null, true);
                });
            }
        }

        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) {
            // Not needed.
        }
    }
}