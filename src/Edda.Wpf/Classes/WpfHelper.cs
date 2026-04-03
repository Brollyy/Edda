using Edda.Const;
using Microsoft.WindowsAPICodePack.Dialogs;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Cache;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DrawingColor = System.Drawing.Color;
using MediaColor = System.Windows.Media.Color;

public static class WpfHelper {
    const string TestPickerQueueFileEnvironmentVariable = "EDDA_TEST_PICKER_QUEUE_FILE";
    const string TestPickerCancelSentinel = "__EDDA_TEST_PICKER_CANCEL__";

    public static bool IsValidCoverFile(string fileName) {
        using Image image = Image.FromFile(fileName);
        return image.Width == image.Height;
    }

    public static string TrimCoverFile(string fileName) {
        using Image source = Image.FromFile(fileName);
        var size = Math.Min(source.Width, source.Height);
        var cropRect = new Rectangle((source.Width - size) / 2, (source.Height - size) / 2, size, size);
        using Bitmap target = new(size, size);
        using Graphics graphics = Graphics.FromImage(target);
        graphics.DrawImage(source, new Rectangle(0, 0, size, size), cropRect, GraphicsUnit.Pixel);
        string newFileName = Path.GetTempPath() + Path.GetRandomFileName() + Path.GetExtension(fileName);
        target.Save(newFileName, System.Drawing.Imaging.ImageFormat.Jpeg);
        return newFileName;
    }

    public static string ChooseNewMapFolder() {
        return ChooseNewMapFolder(Helper.GetRagnarockMapFolder());
    }

    public static string ChooseNewMapFolder(string initialDirectory) {
        var selectedFolder = PickFolder("Select an empty folder to store your map", initialDirectory);
        if (selectedFolder == null) {
            return null;
        }

        var folderName = new FileInfo(selectedFolder).Name;
        if (!Regex.IsMatch(folderName, @"^[a-zA-Z]+$")) {
            MessageBox.Show("The folder name cannot contain spaces or non-alphabetic characters.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return null;
        }

        if (Directory.GetFiles(selectedFolder).Length > 0 &&
            MessageBoxResult.No == MessageBox.Show("The specified folder is not empty. Continue anyway?", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning)) {
            return null;
        }

        return selectedFolder;
    }

    public static string ChooseOpenMapFolder() {
        return ChooseOpenMapFolder(Helper.GetRagnarockMapFolder());
    }

    public static string ChooseOpenMapFolder(string initialDirectory) {
        return PickFolder("Select your map's containing folder", initialDirectory);
    }

    public static string PickExportFolder(string initialDirectory) {
        return PickFolder("Select a folder to export the map to", initialDirectory);
    }

    public static string PickGameInstallFolder(string initialDirectory) {
        return PickFolder("Select the folder that Ragnarock is installed in", initialDirectory);
    }

    public static string PickSongFile() {
        return PickOpenFile("Select a song to map", "OGG Vorbis (*.ogg)|*.ogg", ".ogg");
    }

    public static string PickImportSimfile() {
        return PickOpenFile("Select a simfile to import", "StepMania simfile|*.sm;*.ssc");
    }

    public static string PickCoverFile() {
        return PickOpenFile("Select a song to map", "JPEG Files|*.jpg;*.jpeg;*.jfif");
    }

    static string PickFolder(string title, string initialDirectory) {
        if (TryDequeueTestPickerSelection(out var queuedSelection)) {
            return queuedSelection;
        }

        var dialog = new CommonOpenFileDialog {
            Title = title,
            IsFolderPicker = true,
            InitialDirectory = initialDirectory
        };
        return dialog.ShowDialog() == CommonFileDialogResult.Ok ? dialog.FileName : null;
    }

    static string PickOpenFile(string title, string filter, string defaultExt = null) {
        if (TryDequeueTestPickerSelection(out var queuedSelection)) {
            return queuedSelection;
        }

        var dialog = new Microsoft.Win32.OpenFileDialog {
            Title = title,
            Filter = filter
        };
        if (!string.IsNullOrWhiteSpace(defaultExt)) {
            dialog.DefaultExt = defaultExt;
        }
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public static bool CheckForUpdates() {
        double NumerifyVersionString(string version) {
            string numerify = "";
            int counter = 0;
            foreach (var character in version) {
                if (counter == 3) {
                    numerify += '.';
                    counter++;
                }
                if (character >= '0' && character <= '9') {
                    numerify += character;
                    counter++;
                }
            }
            while (counter < 3) {
                numerify += '0';
                counter++;
            }
            return Helper.DoubleParseInvariant(numerify);
        }

        static bool IsBeta(string version) {
            return version.Contains('b') || version.Contains('B');
        }

        using HttpClient client = new();
        client.DefaultRequestHeaders.Add("User-Agent", "Edda-" + Program.DisplayVersionString);
        string response = client.GetStringAsync(Program.ReleasesAPI).Result;

        var releases = JArray.Parse(response);
        var index = 0;
        if (!IsBeta(Program.VersionString)) {
            while (IsBeta((string)releases[index]["tag_name"])) {
                index++;
            }
        }

        var newestRelease = releases[index];
        string newestVersion = (string)newestRelease["tag_name"];
        string currentVersion = "v" + Program.VersionString;
        if (NumerifyVersionString(newestVersion) <= NumerifyVersionString(currentVersion)) {
            return false;
        }

        MessageBox.Show(
            $"A new release of Edda is available.\n\nNewest version: {newestVersion}\nCurrent version: {currentVersion}",
            "New release available",
            MessageBoxButton.OK,
            MessageBoxImage.Information
        );
        return true;
    }

    public static Window GetFirstWindow<T>() where T : Window {
        return Application.Current.Windows.OfType<T>().FirstOrDefault();
    }

    public static BitmapImage BitmapGenerator(Uri uri, bool ignoreCache = false) {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        if (ignoreCache) {
            bitmap.CacheOption = BitmapCacheOption.None;
            bitmap.UriCachePolicy = new RequestCachePolicy(RequestCacheLevel.BypassCache);
        }
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        if (ignoreCache) {
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
        }
        bitmap.UriSource = uri;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    public static Uri UriForResource(string file) {
        return new($"pack://application:,,,/resources/{file}");
    }

    public static BitmapImage BitmapGenerator(string resourceFile) {
        return BitmapGenerator(UriForResource(resourceFile));
    }

    public static BitmapImage BitmapImageForBeat(double beat, bool isHighlighted = false) {
        const int denominator = Editor.GridDivisionMax * 6;
        int fraction = (int)Math.Round(beat * denominator) % denominator;
        string rune = fraction switch {
            0 => "1",
            denominator * 1 / 4 => "14",
            denominator * 1 / 3 => "13",
            denominator * 5 / 6 => "13",
            denominator * 1 / 2 => "12",
            denominator * 2 / 3 => "23",
            denominator * 1 / 6 => "23",
            denominator * 3 / 4 => "34",
            _ => "X"
        };
        return BitmapGenerator($"rune{rune}{(isHighlighted ? "highlight" : "")}.png");
    }

    public static MediaColor ColorFromString(string colorValue, string fallbackColor) {
        var parsed = System.Windows.Media.ColorConverter.ConvertFromString(string.IsNullOrWhiteSpace(colorValue) ? fallbackColor : colorValue);
        return parsed is MediaColor color ? color : (MediaColor)System.Windows.Media.ColorConverter.ConvertFromString(fallbackColor);
    }

    public static SolidColorBrush BrushFromString(string colorValue, string fallbackColor) {
        return new(ColorFromString(colorValue, fallbackColor));
    }

    public static MediaColor MediaColorFromDrawingColor(DrawingColor color) {
        return MediaColor.FromArgb(color.A, color.R, color.G, color.B);
    }

    public static SolidColorBrush BrushFromDrawingColor(DrawingColor color) {
        return new(MediaColorFromDrawingColor(color));
    }

    public static void DeleteDirectory(string targetDir) {
        string[] files = Directory.GetFiles(targetDir);
        string[] dirs = Directory.GetDirectories(targetDir);

        foreach (string file in files) {
            File.SetAttributes(file, FileAttributes.Normal);
            File.Delete(file);
        }

        foreach (string dir in dirs) {
            DeleteDirectory(dir);
        }

        try {
            Directory.Delete(targetDir, false);
        } catch (IOException ex) {
            Trace.WriteLine(ex);
            ShowOneDriveWarning();
        }
    }

    public static void ShowOneDriveWarning() {
        MessageBox.Show(
            "There's been an issue with managing map files, likely due to OneDrive or similar application blocking Edda from accessing the files.\n\nPlease consider disabling OneDrive on the folder where the map is stored, to avoid losing work.",
            "Warning",
            MessageBoxButton.OK,
            MessageBoxImage.Warning
        );
    }

    static bool TryDequeueTestPickerSelection(out string selection) {
        selection = null;

        var queueFilePath = Environment.GetEnvironmentVariable(TestPickerQueueFileEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(queueFilePath) || !File.Exists(queueFilePath)) {
            return false;
        }

        for (var attempt = 0; attempt < 10; attempt++) {
            try {
                var lines = File.ReadAllLines(queueFilePath)
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .ToList();
                if (lines.Count == 0) {
                    return false;
                }

                selection = lines[0];
                File.WriteAllLines(queueFilePath, lines.Skip(1));
                if (string.Equals(selection, TestPickerCancelSentinel, StringComparison.Ordinal)) {
                    selection = null;
                }
                return true;
            } catch (IOException) {
                Thread.Sleep(20);
            }
        }

        throw new IOException($"Unable to read queued picker selections from '{queueFilePath}'.");
    }
}
