using System;
using System.Windows;

namespace Edda {
    public class WpfDeviceChangeUiAdapter : IDeviceChangeUiAdapter {
        readonly MainWindow mainWindow;

        public WpfDeviceChangeUiAdapter(MainWindow mainWindow) {
            this.mainWindow = mainWindow;
        }

        public bool PlayingOnDefaultDevice => mainWindow.playingOnDefaultDevice;

        public bool DefaultDeviceAvailable => mainWindow.defaultDeviceAvailable;

        public string UserPreferredPlaybackDeviceId => mainWindow.userPreferredPlaybackDeviceID;

        public string PlaybackDeviceId => mainWindow.playbackDeviceID;

        public void InvokeOnUiThread(Action action) {
            Application.Current.Dispatcher.BeginInvoke(action);
        }

        public void UpdatePlaybackDevice(string playbackDeviceId, bool useDefaultDevice) {
            mainWindow.UpdatePlaybackDevice(playbackDeviceId, useDefaultDevice);
        }
    }
}