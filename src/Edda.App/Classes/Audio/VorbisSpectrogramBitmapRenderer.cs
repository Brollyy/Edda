#nullable enable

using Edda.Const;
using NAudio.Vorbis;
using Spectrogram;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using DrawingColor = System.Drawing.Color;

namespace Edda {
    [SupportedOSPlatform("windows")]
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
            CancellationToken cancellationToken) {
            if (numChunks <= 0 || string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) {
                return null;
            }

            colormap ??= Colormap.Blues.Name;
            var backgroundColor = Colormap.GetColormap(colormap).GetColor(0);
            var cacheSearchPattern = string.Format(Editor.Spectrogram.CachedBmpFilenameFormat, type, quality, maxFreq, colormap);

            if (TryLoadCachedChunks(filePath, cache, cacheSearchPattern, drawFlipped, numChunks, cancellationToken, out var cachedBitmaps)) {
                return new SpectrogramBitmapSet(backgroundColor, cachedBitmaps);
            }

            using var reader = new VorbisWaveReader(filePath);
            var bytesPerSample = Math.Max(1, reader.WaveFormat.BitsPerSample / 8);
            var numSamples = reader.Length / bytesPerSample;

            if (numSamples > numChunks * Editor.Spectrogram.MaxSampleSteps * Editor.Spectrogram.StepSize * (int)quality) {
                return null;
            }

            var audioBuffer = new float[numSamples];
            reader.Read(audioBuffer, 0, (int)numSamples);
            cancellationToken.ThrowIfCancellationRequested();

            var audioBufferDouble = Array.ConvertAll(audioBuffer, sample => maxFreq * (double)sample);
            var fftSize = (int)Math.Pow(2, Editor.Spectrogram.FftSizeExp);
            var stepSize = Editor.Spectrogram.StepSize * (int)quality;
            var generator = new SpectrogramGenerator(reader.WaveFormat.SampleRate, fftSize: fftSize, stepSize: stepSize, maxFreq: maxFreq) {
                Colormap = Colormap.GetColormap(colormap)
            };
            generator.Add(audioBufferDouble);

            // Padding keeps the displayed chunk edges aligned with playback on long songs.
            var expectedWidth = (int)numSamples / stepSize;
            generator.Add(new double[Math.Max(expectedWidth - generator.Width, 0) * stepSize]);
            cancellationToken.ThrowIfCancellationRequested();

            using var spectrogramBitmap = type switch {
                SpectrogramType.MelScale => generator.GetBitmapMel(melBinCount: Editor.Spectrogram.MelBinCount),
                SpectrogramType.MaxScale => generator.GetBitmapMax(),
                _ => generator.GetBitmap()
            };

            var splitBitmaps = SplitBitmapHorizontally(spectrogramBitmap, numChunks);
            try {
                cancellationToken.ThrowIfCancellationRequested();

                SaveChunksToCache(filePath, cache, cacheSearchPattern, splitBitmaps);

                var transformedBitmaps = new Bitmap[splitBitmaps.Length];
                for (var i = 0; i < splitBitmaps.Length; i++) {
                    cancellationToken.ThrowIfCancellationRequested();
                    transformedBitmaps[i] = TransformBitmap(splitBitmaps[i], drawFlipped);
                }

                return new SpectrogramBitmapSet(backgroundColor, transformedBitmaps);
            } finally {
                foreach (var bitmap in splitBitmaps) {
                    bitmap.Dispose();
                }
            }
        }

        public static string GetCacheDirectoryPath(string filePath) {
            return Path.Combine(Path.GetDirectoryName(filePath) ?? string.Empty, Program.CachePath);
        }

        static bool TryLoadCachedChunks(
            string filePath,
            bool cache,
            string cacheSearchPattern,
            bool drawFlipped,
            int numChunks,
            CancellationToken cancellationToken,
            out Bitmap[] bitmaps) {
            bitmaps = [];
            var cacheDirectoryPath = GetCacheDirectoryPath(filePath);
            if (!cache || !Directory.Exists(cacheDirectoryPath)) {
                return false;
            }

            var chunkFiles = Directory
                .GetFiles(cacheDirectoryPath, cacheSearchPattern)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (chunkFiles.Length != numChunks) {
                foreach (var chunkFile in chunkFiles) {
                    File.Delete(chunkFile);
                }

                return false;
            }

            var loadedBitmaps = new Bitmap[chunkFiles.Length];
            try {
                for (var i = 0; i < chunkFiles.Length; i++) {
                    cancellationToken.ThrowIfCancellationRequested();
                    using var bitmap = (Bitmap)Bitmap.FromFile(chunkFiles[i]);
                    loadedBitmaps[i] = TransformBitmap(bitmap, drawFlipped);
                }

                bitmaps = loadedBitmaps;
                return true;
            } catch {
                foreach (var bitmap in loadedBitmaps.Where(bitmap => bitmap != null)) {
                    bitmap.Dispose();
                }

                throw;
            }
        }

        static void SaveChunksToCache(string filePath, bool cache, string cacheSearchPattern, Bitmap[] splitBitmaps) {
            if (!cache) {
                return;
            }

            var cacheDirectoryPath = GetCacheDirectoryPath(filePath);
            Directory.CreateDirectory(cacheDirectoryPath);

            for (var i = 0; i < splitBitmaps.Length; i++) {
                var cachedPath = Path.Combine(cacheDirectoryPath, cacheSearchPattern.Replace("*", $"{i:000}"));
                try {
                    splitBitmaps[i].Save(cachedPath, ImageFormat.Png);
                } catch (ExternalException ex) {
                    Trace.WriteLine($"WARNING: Exception when saving spectrogram BMP: ({ex})");
                    File.Delete(cachedPath);
                    return;
                }
            }
        }

        static Bitmap[] SplitBitmapHorizontally(Bitmap source, int numChunks) {
            if (numChunks == 1) {
                return [(Bitmap)source.Clone()];
            }

            var splitBitmaps = new Bitmap[numChunks];
            for (var i = 0; i < numChunks; i++) {
                var startPixel = source.Width * i / numChunks;
                var endPixel = source.Width * (i + 1) / numChunks;
                var bitmap = new Bitmap(endPixel - startPixel, source.Height);
                using var graphics = Graphics.FromImage(bitmap);
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.DrawImage(source, 0, 0, new Rectangle(startPixel, 0, bitmap.Width, bitmap.Height), GraphicsUnit.Pixel);
                splitBitmaps[i] = bitmap;
            }

            return splitBitmaps;
        }

        static Bitmap TransformBitmap(Bitmap bitmap, bool drawFlipped) {
            var transformed = (Bitmap)bitmap.Clone();
            transformed.RotateFlip(drawFlipped
                ? RotateFlipType.Rotate270FlipX
                : RotateFlipType.Rotate270FlipNone);
            return transformed;
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

    [SupportedOSPlatform("windows")]
    public sealed class SpectrogramBitmapSet : IDisposable {
        readonly Bitmap[] bitmaps;

        public SpectrogramBitmapSet(DrawingColor backgroundColor, Bitmap[] bitmaps) {
            BackgroundColor = backgroundColor;
            this.bitmaps = bitmaps ?? [];
        }

        public DrawingColor BackgroundColor { get; }

        public IReadOnlyList<Bitmap> Bitmaps => bitmaps;

        public void Dispose() {
            foreach (var bitmap in bitmaps) {
                bitmap.Dispose();
            }
        }
    }
}
