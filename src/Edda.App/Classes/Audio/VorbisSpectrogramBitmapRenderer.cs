#nullable enable

using Edda.Const;
using Spectrogram;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Edda {
    public static class VorbisSpectrogramBitmapRenderer {
        public static SpectrogramBitmapSet? Render(
            string filePath,
            bool cache,
            SpectrogramType type,
            SpectrogramQuality quality,
            int maxFreq,
            string? colormap,
            bool drawFlipped,
            int numChunks,
            CancellationToken cancellationToken,
            ISpectrogramAudioReader audioReader) {
            if (numChunks <= 0 || string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) {
                return null;
            }

            colormap ??= Colormap.Blues.Name;
            var selectedColormap = Colormap.GetColormap(colormap);
            var backgroundRgb = selectedColormap.GetRGB((byte)0);

            var audioSamples = audioReader.ReadSamples(filePath);
            var bytesPerSample = Math.Max(1, audioSamples.BitsPerSample / 8);
            var numSamples = audioSamples.SourceLengthBytes / bytesPerSample;

            if (numSamples > numChunks * Editor.Spectrogram.MaxSampleSteps * Editor.Spectrogram.StepSize * (int)quality) {
                return null;
            }

            var audioBuffer = audioSamples.Samples;
            cancellationToken.ThrowIfCancellationRequested();

            var audioBufferDouble = Array.ConvertAll(audioBuffer, sample => maxFreq * (double)sample);
            var fftSize = (int)Math.Pow(2, Editor.Spectrogram.FftSizeExp);
            var stepSize = Editor.Spectrogram.StepSize * (int)quality;
            var generator = new SpectrogramGenerator(audioSamples.SampleRate, fftSize: fftSize, stepSize: stepSize, maxFreq: maxFreq) {
                Colormap = selectedColormap
            };
            generator.Add(audioBufferDouble);

            // Padding keeps the displayed chunk edges aligned with playback on long songs.
            var expectedWidth = (int)numSamples / stepSize;
            generator.Add(new double[Math.Max(expectedWidth - generator.Width, 0) * stepSize]);
            cancellationToken.ThrowIfCancellationRequested();

            var ffts = type switch {
                SpectrogramType.MelScale => generator.GetMelFFTs(Editor.Spectrogram.MelBinCount),
                SpectrogramType.MaxScale => ReduceFftsByMax(generator.GetFFTs(), reduction: 4),
                _ => generator.GetFFTs()
            };

            cancellationToken.ThrowIfCancellationRequested();
            return new SpectrogramBitmapSet(
                new SpectrogramRgbColor(backgroundRgb.Item1, backgroundRgb.Item2, backgroundRgb.Item3),
                RenderChunks(ffts, selectedColormap, drawFlipped, numChunks, cancellationToken));
        }

        public static string GetCacheDirectoryPath(string filePath) {
            return Path.Combine(Path.GetDirectoryName(filePath) ?? string.Empty, Program.CachePath);
        }

        static SpectrogramPixelChunk[] RenderChunks(
            IReadOnlyList<double[]> ffts,
            Colormap colormap,
            bool drawFlipped,
            int numChunks,
            CancellationToken cancellationToken) {
            if (ffts.Count == 0) {
                return [];
            }

            var frequencyBins = Math.Max(1, ffts.Max(fft => fft.Length));
            var chunks = new SpectrogramPixelChunk[numChunks];
            for (var chunkIndex = 0; chunkIndex < numChunks; chunkIndex++) {
                cancellationToken.ThrowIfCancellationRequested();
                var startColumn = ffts.Count * chunkIndex / numChunks;
                var endColumn = Math.Max(startColumn + 1, ffts.Count * (chunkIndex + 1) / numChunks);
                var height = endColumn - startColumn;
                var pixels = new byte[frequencyBins * height * 4];

                for (var timeIndex = 0; timeIndex < height; timeIndex++) {
                    var fft = ffts[Math.Min(startColumn + timeIndex, ffts.Count - 1)];
                    var outputRow = height - 1 - timeIndex;
                    for (var bin = 0; bin < frequencyBins; bin++) {
                        var sourceBin = drawFlipped ? bin : frequencyBins - 1 - bin;
                        var value = sourceBin < fft.Length ? fft[sourceBin] : 0;
                        var intensity = (byte)Math.Clamp(value, 0, 255);
                        var (red, green, blue) = colormap.GetRGB(intensity);
                        var offset = ((outputRow * frequencyBins) + bin) * 4;
                        pixels[offset] = blue;
                        pixels[offset + 1] = green;
                        pixels[offset + 2] = red;
                        pixels[offset + 3] = 255;
                    }
                }

                chunks[chunkIndex] = new SpectrogramPixelChunk(frequencyBins, height, pixels);
            }

            return chunks;
        }

        static List<double[]> ReduceFftsByMax(List<double[]> ffts, int reduction) {
            var reduced = new List<double[]>(ffts.Count);
            foreach (var fft in ffts) {
                var reducedFft = new double[fft.Length / reduction];
                for (var i = 0; i < reducedFft.Length; i++) {
                    for (var offset = 0; offset < reduction; offset++) {
                        reducedFft[i] = Math.Max(reducedFft[i], fft[i * reduction + offset]);
                    }
                }
                reduced.Add(reducedFft);
            }

            return reduced;
        }

        public enum SpectrogramType {
            Standard = 0,
            MelScale = 1,
            MaxScale = 2
        }

        public enum SpectrogramQuality {
            Low = 4,
            Medium = 2,
            High = 1
        }
    }

    public sealed class SpectrogramBitmapSet : IDisposable {
        readonly SpectrogramPixelChunk[] chunks;

        public SpectrogramBitmapSet(SpectrogramRgbColor backgroundColor, SpectrogramPixelChunk[] chunks) {
            BackgroundColor = backgroundColor;
            this.chunks = chunks ?? [];
        }

        public SpectrogramRgbColor BackgroundColor { get; }

        public IReadOnlyList<SpectrogramPixelChunk> Chunks => chunks;

        public void Dispose() {
        }
    }

    public readonly record struct SpectrogramRgbColor(byte R, byte G, byte B);

    public sealed record SpectrogramPixelChunk(int Width, int Height, byte[] BgraPixels);
}