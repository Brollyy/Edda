#pragma warning disable CA1416

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Edda.Const;
using NAudio.Vorbis;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingColor = System.Drawing.Color;
using DrawingGraphics = System.Drawing.Graphics;
using DrawingPen = System.Drawing.Pen;
using DrawingRectangle = System.Drawing.Rectangle;
using InterpolationMode = System.Drawing.Drawing2D.InterpolationMode;
using PixelFormat = System.Drawing.Imaging.PixelFormat;

namespace Edda.Avalonia.Windows;

public sealed partial class MainWindow {
    global::Avalonia.Controls.Image mainWaveformImage = null!;
    readonly List<global::Avalonia.Controls.Image> spectrogramChunkImages = [];
    global::Avalonia.Controls.Image navWaveformImage = null!;

    CancellationTokenSource mainWaveformRenderCancellationTokenSource = new();
    CancellationTokenSource spectrogramRenderCancellationTokenSource = new();
    CancellationTokenSource navWaveformRenderCancellationTokenSource = new();

    Bitmap? renderedMainWaveformBitmap;
    readonly List<Bitmap> renderedSpectrogramChunkBitmaps = [];
    Bitmap? renderedNavWaveformBitmap;
    SolidColorBrush? renderedSpectrogramBackgroundBrush;
    SpectrogramChunkLayout? spectrogramChunkLayout;

    string? mainWaveformRenderKey;
    string? spectrogramRenderKey;
    string? navWaveformRenderKey;

    void EnsureMainWaveformImage() {
        if (mainWaveformCanvas == null) {
            return;
        }

        if (mainWaveformImage == null) {
            mainWaveformImage = new global::Avalonia.Controls.Image {
                Stretch = global::Avalonia.Media.Stretch.Fill,
                IsHitTestVisible = false
            };
            RenderOptions.SetBitmapInterpolationMode(mainWaveformImage, BitmapInterpolationMode.None);
        }

        if (!mainWaveformCanvas.Children.Contains(mainWaveformImage)) {
            mainWaveformCanvas.Children.Add(mainWaveformImage);
        }
    }

    void EnsureSpectrogramChunkImages(int numChunks) {
        if (spectrogramCanvas == null) {
            return;
        }

        while (spectrogramChunkImages.Count < numChunks) {
            var image = new global::Avalonia.Controls.Image {
                Stretch = global::Avalonia.Media.Stretch.Fill,
                IsHitTestVisible = false
            };
            RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.None);
            spectrogramChunkImages.Add(image);
        }
    }

    void EnsureNavWaveformImage() {
        if (navWaveformBackdrop == null) {
            return;
        }

        if (navWaveformImage == null) {
            navWaveformImage = new global::Avalonia.Controls.Image {
                Stretch = global::Avalonia.Media.Stretch.Fill,
                IsHitTestVisible = false
            };
            RenderOptions.SetBitmapInterpolationMode(navWaveformImage, BitmapInterpolationMode.None);
        }

        if (!navWaveformBackdrop.Children.Contains(navWaveformImage)) {
            navWaveformBackdrop.Children.Add(navWaveformImage);
        }
    }

    void InvalidateEditorAudioVisuals(bool clearVisuals = false) {
        CancelRender(ref mainWaveformRenderCancellationTokenSource);
        CancelRender(ref spectrogramRenderCancellationTokenSource);
        CancelRender(ref navWaveformRenderCancellationTokenSource);

        mainWaveformRenderKey = null;
        spectrogramRenderKey = null;
        navWaveformRenderKey = null;

        ReplaceBitmap(ref renderedMainWaveformBitmap, null, mainWaveformImage, clearVisuals);
        ClearSpectrogramBitmaps(clearVisuals);
        ReplaceBitmap(ref renderedNavWaveformBitmap, null, navWaveformImage, clearVisuals);
    }

    void ClearSpectrogramBitmaps(bool clearVisuals) {
        spectrogramChunkLayout = null;
        foreach (var bitmap in renderedSpectrogramChunkBitmaps) {
            bitmap.Dispose();
        }
        renderedSpectrogramChunkBitmaps.Clear();
        renderedSpectrogramBackgroundBrush = null;

        foreach (var image in spectrogramChunkImages) {
            image.Source = null;
        }

        if (clearVisuals && spectrogramCanvas != null) {
            spectrogramCanvas.Children.Clear();
            spectrogramCanvas.Background = null;
        }
    }

    void UpdateSpectrogramLayout(double width, double totalHeight, double beatContentHeight, double topPadding, int numChunks) {
        spectrogramChunkLayout = new SpectrogramChunkLayout(width, totalHeight, beatContentHeight, topPadding, numChunks);
        LayoutSpectrogramChunks();
    }

    void LayoutSpectrogramChunks() {
        if (spectrogramCanvas == null || spectrogramChunkLayout == null) {
            return;
        }

        var layout = spectrogramChunkLayout;
        spectrogramCanvas.Width = layout.Width;
        spectrogramCanvas.Height = layout.TotalHeight;
        spectrogramCanvas.Children.Clear();
        spectrogramCanvas.Background = renderedSpectrogramBackgroundBrush ?? new SolidColorBrush(global::Avalonia.Media.Color.Parse("#030611"));

        EnsureSpectrogramChunkImages(layout.NumChunks);

        var y = layout.TopPadding;
        if (renderedSpectrogramChunkBitmaps.Count == layout.NumChunks) {
            var totalBitmapHeight = renderedSpectrogramChunkBitmaps.Sum(bitmap => (double)bitmap.PixelSize.Height);
            for (var displayIndex = 0; displayIndex < layout.NumChunks; displayIndex++) {
                var bitmap = renderedSpectrogramChunkBitmaps[layout.NumChunks - 1 - displayIndex];
                var image = spectrogramChunkImages[displayIndex];
                image.Width = layout.Width;
                image.Height = totalBitmapHeight <= 0
                    ? layout.BeatContentHeight / layout.NumChunks
                    : layout.BeatContentHeight * bitmap.PixelSize.Height / totalBitmapHeight;
                image.Source = bitmap;
                Canvas.SetLeft(image, 0);
                Canvas.SetTop(image, y);
                spectrogramCanvas.Children.Add(image);
                y += image.Height;
            }

            return;
        }

        var placeholderHeight = layout.BeatContentHeight / Math.Max(1, layout.NumChunks);
        for (var i = 0; i < layout.NumChunks; i++) {
            var image = spectrogramChunkImages[i];
            image.Width = layout.Width;
            image.Height = placeholderHeight;
            image.Source = null;
            Canvas.SetLeft(image, 0);
            Canvas.SetTop(image, y);
            spectrogramCanvas.Children.Add(image);
            y += placeholderHeight;
        }
    }

    void ScheduleMainWaveformRender(double width, double height) {
        EnsureMainWaveformImage();
        if (mainWaveformImage == null) {
            return;
        }

        mainWaveformImage.Width = width;
        mainWaveformImage.Height = height;

        var songPath = CurrentSongPath;
        if (string.IsNullOrWhiteSpace(songPath) || !File.Exists(songPath)) {
            ReplaceBitmap(ref renderedMainWaveformBitmap, null, mainWaveformImage);
            return;
        }

        var color = ResolveBrush(userSettings.GetValueForKey(UserSettingsKey.NavWaveformColor), Editor.Waveform.ColourWPF, opacity: 0.35).Color;
        var renderKey = $"{songPath}|{width:0}|{height:0}|{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
        if (string.Equals(renderKey, mainWaveformRenderKey, StringComparison.Ordinal) && renderedMainWaveformBitmap != null) {
            return;
        }

        mainWaveformRenderKey = renderKey;
        var token = ResetRenderToken(ref mainWaveformRenderCancellationTokenSource);
        _ = RenderAndApplyBitmapAsync(
            () => RenderWaveformBitmap(songPath, width, height, color, token),
            token,
            bitmap => ReplaceBitmap(ref renderedMainWaveformBitmap, bitmap, mainWaveformImage));
    }

    void ScheduleSpectrogramRender(int numChunks) {
        var songPath = CurrentSongPath;
        if (string.IsNullOrWhiteSpace(songPath) || !File.Exists(songPath) || spectrogramCanvas == null) {
            ClearSpectrogramBitmaps(clearVisuals: false);
            LayoutSpectrogramChunks();
            return;
        }

        var cache = GetSettingBool(UserSettingsKey.SpectrogramCache, DefaultUserSettings.SpectrogramCache);
        var colormap = userSettings.GetValueForKey(UserSettingsKey.SpectrogramColormap) ?? DefaultUserSettings.SpectrogramColormap;
        var flipped = GetSettingBool(UserSettingsKey.SpectrogramFlipped, DefaultUserSettings.SpectrogramFlipped);
        var frequency = int.TryParse(userSettings.GetValueForKey(UserSettingsKey.SpectrogramFrequency), out var parsedFrequency)
            ? parsedFrequency
            : DefaultUserSettings.SpectrogramFrequency;

        var spectrogramType = Edda.VorbisSpectrogramBitmapRenderer.SpectrogramType.Standard;
        var spectrogramQuality = Edda.VorbisSpectrogramBitmapRenderer.SpectrogramQuality.Medium;
        _ = Enum.TryParse(userSettings.GetValueForKey(UserSettingsKey.SpectrogramType) ?? DefaultUserSettings.SpectrogramType, ignoreCase: true, out spectrogramType);
        _ = Enum.TryParse(userSettings.GetValueForKey(UserSettingsKey.SpectrogramQuality) ?? DefaultUserSettings.SpectrogramQuality, ignoreCase: true, out spectrogramQuality);

        var renderKey = $"{songPath}|{cache}|{spectrogramType}|{spectrogramQuality}|{frequency}|{colormap}|{flipped}|{numChunks}";
        if (string.Equals(renderKey, spectrogramRenderKey, StringComparison.Ordinal) &&
            renderedSpectrogramChunkBitmaps.Count == numChunks) {
            LayoutSpectrogramChunks();
            return;
        }

        spectrogramRenderKey = renderKey;
        var token = ResetRenderToken(ref spectrogramRenderCancellationTokenSource);
        _ = RenderAndApplySpectrogramAsync(
            () => RenderSpectrogramChunks(songPath, cache, spectrogramType, spectrogramQuality, frequency, colormap, flipped, numChunks, token),
            token);
    }

    void ScheduleNavWaveformRender(double width, double height) {
        EnsureNavWaveformImage();
        if (navWaveformImage == null) {
            return;
        }

        navWaveformImage.Width = width;
        navWaveformImage.Height = height;

        var songPath = CurrentSongPath;
        if (string.IsNullOrWhiteSpace(songPath) || !File.Exists(songPath)) {
            ReplaceBitmap(ref renderedNavWaveformBitmap, null, navWaveformImage);
            return;
        }

        var color = ResolveBrush(userSettings.GetValueForKey(UserSettingsKey.NavWaveformColor), Editor.Waveform.ColourWPF, opacity: 0.95).Color;
        var renderKey = $"{songPath}|{width:0}|{height:0}|{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
        if (string.Equals(renderKey, navWaveformRenderKey, StringComparison.Ordinal) && renderedNavWaveformBitmap != null) {
            return;
        }

        navWaveformRenderKey = renderKey;
        var token = ResetRenderToken(ref navWaveformRenderCancellationTokenSource);
        _ = RenderAndApplyBitmapAsync(
            () => RenderWaveformBitmap(songPath, width, height, color, token),
            token,
            bitmap => ReplaceBitmap(ref renderedNavWaveformBitmap, bitmap, navWaveformImage));
    }

    async Task RenderAndApplyBitmapAsync(Func<Bitmap?> render, CancellationToken token, Action<Bitmap?> applyBitmap) {
        Bitmap? bitmap = null;
        try {
            bitmap = await Task.Run(render, token);
        } catch (OperationCanceledException) {
            return;
        } catch {
            bitmap?.Dispose();
            return;
        }

        if (token.IsCancellationRequested) {
            bitmap?.Dispose();
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() => {
            if (token.IsCancellationRequested) {
                bitmap?.Dispose();
                return;
            }

            applyBitmap(bitmap);
        });
    }

    async Task RenderAndApplySpectrogramAsync(Func<AvaloniaSpectrogramRenderResult?> render, CancellationToken token) {
        AvaloniaSpectrogramRenderResult? result = null;
        try {
            result = await Task.Run(render, token);
        } catch (OperationCanceledException) {
            return;
        } catch {
            result?.Dispose();
            return;
        }

        if (token.IsCancellationRequested) {
            result?.Dispose();
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() => {
            if (token.IsCancellationRequested) {
                result?.Dispose();
                return;
            }

            ApplySpectrogramRenderResult(result);
        });
    }

    void ApplySpectrogramRenderResult(AvaloniaSpectrogramRenderResult? result) {
        foreach (var bitmap in renderedSpectrogramChunkBitmaps) {
            bitmap.Dispose();
        }
        renderedSpectrogramChunkBitmaps.Clear();

        if (result == null) {
            renderedSpectrogramBackgroundBrush = null;
            LayoutSpectrogramChunks();
            return;
        }

        renderedSpectrogramBackgroundBrush = new SolidColorBrush(result.BackgroundColor);
        renderedSpectrogramChunkBitmaps.AddRange(result.DetachChunkBitmaps());
        result.Dispose();
        LayoutSpectrogramChunks();
    }

    static void ReplaceBitmap(ref Bitmap? currentBitmap, Bitmap? nextBitmap, global::Avalonia.Controls.Image? target, bool clearVisual = false) {
        var previousBitmap = currentBitmap;
        currentBitmap = nextBitmap;
        if (target != null) {
            target.Source = clearVisual ? null : nextBitmap;
        }
        previousBitmap?.Dispose();
    }

    static CancellationToken ResetRenderToken(ref CancellationTokenSource source) {
        var previous = source;
        source = new CancellationTokenSource();
        try {
            previous.Cancel();
        } catch {
            // Ignored: we're replacing the token source regardless.
        }
        previous.Dispose();
        return source.Token;
    }

    static void CancelRender(ref CancellationTokenSource source) {
        try {
            source.Cancel();
        } catch {
            // Ignore cancellation races while the window is closing.
        }
        source.Dispose();
        source = new CancellationTokenSource();
    }

    static Bitmap? RenderWaveformBitmap(string filePath, double width, double height, global::Avalonia.Media.Color color, CancellationToken token, bool alignToRight = false) {
        var (bitmapWidth, bitmapHeight) = ScaleBitmapDimensions(width, height);
        if (bitmapWidth <= 0 || bitmapHeight <= 0) {
            return null;
        }

        using var reader = new VorbisWaveReader(filePath);
        using var bitmap = new DrawingBitmap(bitmapWidth, bitmapHeight, PixelFormat.Format32bppPArgb);
        using var graphics = DrawingGraphics.FromImage(bitmap);
        using var pen = new DrawingPen(DrawingColor.FromArgb(color.A, color.R, color.G, color.B), (float)Editor.Waveform.ThicknessWPF);

        graphics.Clear(DrawingColor.Transparent);

        var channels = Math.Max(1, reader.WaveFormat.Channels);
        var bytesPerSample = Math.Max(1, reader.WaveFormat.BitsPerSample / 8 * channels);
        var sampleCount = Math.Max(1, reader.Length / bytesPerSample);
        var samplesPerPixel = Math.Max(1, (int)(sampleCount / bitmapHeight) * channels);
        var expectedSamplesPerPixel = sampleCount / (double)bitmapHeight * channels;
        long totalSamples = 0;
        double totalExpectedSamples = 0;
        var buffer = new float[samplesPerPixel + channels];

        for (var pixel = 0; pixel < bitmapHeight; pixel++) {
            token.ThrowIfCancellationRequested();

            var samplesRead = reader.Read(buffer, 0, samplesPerPixel);
            if (samplesRead == 0) {
                break;
            }

            totalSamples += samplesPerPixel;
            totalExpectedSamples += expectedSamplesPerPixel;
            if (totalExpectedSamples - totalSamples > channels) {
                totalSamples += channels;
                reader.Read(buffer, samplesPerPixel, channels);
                samplesRead += channels;
            }

            var samples = buffer.Take(samplesRead).ToArray();
            Array.Sort(samples);
            var lowIndex = (int)((samples.Length - 1) * (1 - Editor.Waveform.SampleMaxPercentile));
            var highIndex = (int)((samples.Length - 1) * Editor.Waveform.SampleMaxPercentile);
            var lowPercent = (samples[lowIndex] + 1f) / 2f;
            var highPercent = (samples[highIndex] + 1f) / 2f;

            var lowValue = bitmapWidth * lowPercent;
            var highValue = bitmapWidth * highPercent;
            if (alignToRight) {
                var span = Math.Max(1, highValue - lowValue);
                lowValue = Math.Max(0, bitmapWidth - span - 4);
                highValue = Math.Min(bitmapWidth, bitmapWidth - 4);
            }

            var y = bitmapHeight - pixel;
            graphics.DrawLine(pen, lowValue, y, highValue, y);
        }

        return ConvertDrawingBitmap(bitmap);
    }

    static AvaloniaSpectrogramRenderResult? RenderSpectrogramChunks(
        string filePath,
        bool cache,
        Edda.VorbisSpectrogramBitmapRenderer.SpectrogramType type,
        Edda.VorbisSpectrogramBitmapRenderer.SpectrogramQuality quality,
        int maxFreq,
        string colormap,
        bool flipped,
        int numChunks,
        CancellationToken token) {
        using var bitmapSet = Edda.VorbisSpectrogramBitmapRenderer.Render(
            filePath,
            cache,
            type,
            quality,
            maxFreq,
            colormap,
            flipped,
            numChunks,
            token);

        if (bitmapSet == null) {
            return null;
        }

        var chunkBitmaps = new Bitmap[bitmapSet.Bitmaps.Count];
        for (var i = 0; i < bitmapSet.Bitmaps.Count; i++) {
            token.ThrowIfCancellationRequested();
            chunkBitmaps[i] = ConvertDrawingBitmap(bitmapSet.Bitmaps[i])!;
        }

        return new AvaloniaSpectrogramRenderResult(
            global::Avalonia.Media.Color.FromArgb(bitmapSet.BackgroundColor.A, bitmapSet.BackgroundColor.R, bitmapSet.BackgroundColor.G, bitmapSet.BackgroundColor.B),
            chunkBitmaps);
    }

    static Bitmap? ConvertDrawingBitmap(DrawingBitmap drawingBitmap) {
        using var stream = new MemoryStream();
        drawingBitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
        stream.Position = 0;
        return new Bitmap(stream);
    }

    static (int width, int height) ScaleBitmapDimensions(double width, double height) {
        var targetWidth = Math.Max(1, (int)Math.Round(width));
        var targetHeight = Math.Max(1, (int)Math.Round(height));
        var largestDimension = Math.Max(targetWidth, targetHeight);
        if (largestDimension <= Editor.Waveform.MaxDimension) {
            return (targetWidth, targetHeight);
        }

        var scale = Editor.Waveform.MaxDimension / (double)largestDimension;
        return (
            Math.Max(1, (int)Math.Round(targetWidth * scale)),
            Math.Max(1, (int)Math.Round(targetHeight * scale)));
    }

    sealed class SpectrogramChunkLayout {
        public SpectrogramChunkLayout(double width, double totalHeight, double beatContentHeight, double topPadding, int numChunks) {
            Width = width;
            TotalHeight = totalHeight;
            BeatContentHeight = beatContentHeight;
            TopPadding = topPadding;
            NumChunks = numChunks;
        }

        public double Width { get; }
        public double TotalHeight { get; }
        public double BeatContentHeight { get; }
        public double TopPadding { get; }
        public int NumChunks { get; }
    }

    sealed class AvaloniaSpectrogramRenderResult : IDisposable {
        Bitmap[] chunkBitmaps;

        public AvaloniaSpectrogramRenderResult(global::Avalonia.Media.Color backgroundColor, Bitmap[] chunkBitmaps) {
            BackgroundColor = backgroundColor;
            this.chunkBitmaps = chunkBitmaps;
        }

        public global::Avalonia.Media.Color BackgroundColor { get; }

        public Bitmap[] DetachChunkBitmaps() {
            var detached = chunkBitmaps;
            chunkBitmaps = [];
            return detached;
        }

        public void Dispose() {
            foreach (var bitmap in chunkBitmaps) {
                bitmap.Dispose();
            }
            chunkBitmaps = [];
        }
    }
}

#pragma warning restore CA1416
