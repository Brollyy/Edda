using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.Threading;

namespace Edda.Testing;

public sealed class AudioOutputProbe : IDisposable {
    readonly MMDeviceEnumerator enumerator;
    readonly MMDevice device;
    readonly WasapiLoopbackCapture capture;
    readonly object sync = new();
    double peakAmplitude;
    bool disposed;

    AudioOutputProbe(MMDeviceEnumerator enumerator, MMDevice device, WasapiLoopbackCapture capture) {
        this.enumerator = enumerator;
        this.device = device;
        this.capture = capture;
        capture.DataAvailable += OnDataAvailable;
        capture.StartRecording();
    }

    public static AudioOutputProbe Create() {
        var enumerator = new MMDeviceEnumerator();
        var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        var capture = new WasapiLoopbackCapture(device);
        return new AudioOutputProbe(enumerator, device, capture);
    }

    public void ResetPeak() {
        lock (sync) {
            peakAmplitude = 0;
        }
    }

    public double MeasurePeak(TimeSpan duration) {
        ResetPeak();
        Thread.Sleep(duration);
        return GetPeakAmplitude();
    }

    public bool WaitForPeakAbove(double threshold, TimeSpan timeout) {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline) {
            if (GetPeakAmplitude() >= threshold) {
                return true;
            }

            Thread.Sleep(25);
        }

        return GetPeakAmplitude() >= threshold;
    }

    public double GetPeakAmplitude() {
        lock (sync) {
            return peakAmplitude;
        }
    }

    void OnDataAvailable(object? sender, WaveInEventArgs e) {
        var observedPeak = ComputePeak(e.Buffer, e.BytesRecorded, capture.WaveFormat);
        lock (sync) {
            if (observedPeak > peakAmplitude) {
                peakAmplitude = observedPeak;
            }
        }
    }

    static double ComputePeak(byte[] buffer, int bytesRecorded, WaveFormat waveFormat) {
        if (bytesRecorded <= 0) {
            return 0;
        }

        var peak = 0d;
        if (waveFormat.Encoding == WaveFormatEncoding.IeeeFloat && waveFormat.BitsPerSample == 32) {
            for (var offset = 0; offset + 4 <= bytesRecorded; offset += 4) {
                peak = Math.Max(peak, Math.Abs(BitConverter.ToSingle(buffer, offset)));
            }

            return peak;
        }

        if (waveFormat.BitsPerSample == 16) {
            for (var offset = 0; offset + 2 <= bytesRecorded; offset += 2) {
                peak = Math.Max(peak, Math.Abs(BitConverter.ToInt16(buffer, offset) / 32768d));
            }

            return peak;
        }

        if (waveFormat.BitsPerSample == 32) {
            for (var offset = 0; offset + 4 <= bytesRecorded; offset += 4) {
                peak = Math.Max(peak, Math.Abs(BitConverter.ToInt32(buffer, offset) / (double)int.MaxValue));
            }
        }

        return peak;
    }

    public void Dispose() {
        if (disposed) {
            return;
        }

        disposed = true;
        capture.DataAvailable -= OnDataAvailable;
        try {
            capture.StopRecording();
        } catch {
            // Best effort cleanup for test-only capture.
        }

        capture.Dispose();
        device.Dispose();
        enumerator.Dispose();
    }
}
