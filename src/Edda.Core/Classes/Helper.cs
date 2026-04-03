using Edda.Classes.MapEditorNS.NoteNS;
using Edda.Const;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

public partial class Helper {

    // Math
    private static double threshold = 0.0001;
    // these should be used when parsing numerical strings that do not necessarily come from the user's culture
    // e.g. externally downloaded maps (where the JSON standard uses , for decimal separators)
    public static double DoubleParseInvariant(string s) {
        return double.Parse(s, CultureInfo.InvariantCulture);
    }
    public static bool DoubleApproxGreaterEqual(double x, double y) {
        return x - y >= -threshold;
    }
    public static bool DoubleApproxGreater(double x, double y) {
        return x - y > threshold;
    }
    public static bool DoubleApproxEqual(double x, double y) {
        return Math.Abs(x - y) <= threshold;
    }
    public static bool DoubleRangeCheck(double a, double x, double y) {
        double lower = Math.Min(x, y);
        double higher = Math.Max(x, y);
        return lower <= a && a <= higher;
    }
    public static double DoubleRangeTruncate(double a, double x, double y) {
        return Math.Min(Math.Max(a, x), y);
    }
    public class DoubleApproxEqualComparer : IEqualityComparer<double> {
        public bool Equals(double x, double y) {
            return DoubleApproxEqual(x, y);
        }

        public int GetHashCode(double obj) {
            return double.Round(obj, 3).GetHashCode(); // We need this rounding to make sure all close-enough values will fall into the same bucket.
        }
    }


    public static string TimeFormat(int seconds) {
        int min = seconds / 60;
        int sec = seconds % 60;

        return $"{min}:{sec:D2}";
    }
    public static string TimeFormat(double seconds) {
        return TimeFormat((int)seconds);
    }
    public static double LpNorm(List<int> vector, int p) {
        return LpNorm(vector.ConvertAll(x => (double)x), p);
    }
    public static double LpNorm(List<double> vector, int p) {
        var total = 0.0;
        foreach (var x in vector) {
            total += Math.Pow(Math.Abs(x), p);
        }
        return Math.Pow(total, 1.0 / p);
    }
    public static List<double> LpNormalise(List<int> vector, int p) {
        return LpNormalise(vector.ConvertAll(x => (double)x), p);
    }
    public static List<double> LpNormalise(List<double> vector, int p) {
        var norm = LpNorm(vector, p);
        var normalisedVec = new List<double>();
        foreach (var x in vector) {
            normalisedVec.Add(x / norm);
        }
        return normalisedVec;
    }
    public static double LpDistance(List<double> v, List<double> w, int p) {
        var distVec = new List<double>();
        for (int i = 0; i < v.Count; i++) {
            distVec.Add(v[i] - w[i]);
        }
        return LpNorm(distVec, p);
    }
    public static double GetQuantile(List<double> x, double q) {
        if (x.Count == 0) {
            return 0;
        }
        x.Sort();
        var indx = (x.Count - 1) * q;
        if ((int)indx == indx) {
            return x[(int)indx];
        } else {
            var i = (int)Math.Floor(indx);
            var j = (int)Math.Ceiling(indx);
            return x[i] + (x[j] - x[i]) * q;
        }
    }

    // File I/O
    public static string GetRoamingAppDataDirectory() {
        var appDataPath = Environment.GetEnvironmentVariable("APPDATA");
        if (!string.IsNullOrWhiteSpace(appDataPath) && Path.IsPathRooted(appDataPath)) {
            return appDataPath;
        }

        return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    }

    public static string SanitiseSongFileName(string fileName) {
        //return fileName.Replace(" ", "-");
        return BeatmapDefaults.SongFilename;
    }
    public static string SanitiseCoverFileName(string fileName) {
        // We'd like to use cover.* instead of the actual filename, as RagnaCustoms doesn't allow too long filenames there.
        string coverExtension = Path.GetExtension(fileName);
        if (coverExtension == ".jfif") {
            // .jfif extension is not directly supported in RagnaRock, but the file can be easily converted into supported .jpeg by just renaming it.
            coverExtension = ".jpeg";
        }
        return BeatmapDefaults.CoverFilename + coverExtension;
    }
    public static string DefaultRagnarockMapPath() {
        string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string ragPath = Path.Combine(docPath, "Ragnarock");
        string ragSongPath = Path.Combine(ragPath, "CustomSongs");
        return Directory.Exists(ragSongPath) ? ragSongPath : null;
    }
    public static string ValidFilenameFrom(string filename) {
        string output = filename;
        foreach (char c in Path.GetInvalidFileNameChars()) {
            output = output.Replace(c, '_');
        }
        return output;
    }
    public static string ValidMapFolderNameFrom(string filename) {
        string output = "";
        foreach (char c in filename) {
            if ('a' <= c && c <= 'z' || 'A' <= c && c <= 'Z') {
                output += c;
            }
        }
        return output;
    }
    public static void FileDeleteIfExists(string path) {
        if (File.Exists(path)) {
            File.Delete(path);
        }
    }
    public static string GetRagnarockMapFolder() {
        var userSettings = new UserSettingsManager(Program.SettingsFile);
        return int.TryParse(userSettings.GetValueForKey(Edda.Const.UserSettingsKey.MapSaveLocationIndex), out var index) && index > 0
            ? Path.Combine(userSettings.GetValueForKey(Edda.Const.UserSettingsKey.MapSaveLocationPath), Program.GameInstallRelativeMapFolder)
            : Helper.DefaultRagnarockMapPath();
    }

    // Processes
    public static void ThreadedPrint(string message) {
        new System.Threading.Thread(new System.Threading.ThreadStart(delegate {
            Trace.WriteLine(message);
        })).Start();
    }
    public static int FFmpeg(string dir, string arg) {
        // uses the bundled version of ffmpeg with ONLY libvorbis support
        var path = Path.Combine(AppContext.BaseDirectory, Program.ResourcesPath, "ffmpeg.exe");
        if (!File.Exists(path)) {
            // fallback for layouts where Resources are copied to the app root
            path = Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe");
        }
        if (!File.Exists(path)) {
            throw new FileNotFoundException($"Couldn't find ffmpeg executable in {AppContext.BaseDirectory}");
        }

        var startInfo = new ProcessStartInfo(path, arg);
        startInfo.WorkingDirectory = dir;
        var p = Process.Start(startInfo);
        p.WaitForExit();
        int exitCode = p.ExitCode;
        p.Close();

        return exitCode;
    }
    public static void CmdCopyFile(string src, string dst) {
        var p = Process.Start("cmd.exe", "/C copy \"" + src + "\" \"" + dst + "\"");
        p.StartInfo.RedirectStandardOutput = true;
        p.Start();
        Console.WriteLine(p.StandardOutput.ReadToEnd());
        // p.WaitForExit();
    }
    public static void CmdCopyFiles(List<string> src, string dst) {
        var cmd = "/C ";
        foreach (var str in src) {
            cmd += "copy \"" + str + "\" \"" + dst + "\" & ";
        }
        var p = Process.Start("cmd.exe", cmd);
        //p.StartInfo.RedirectStandardOutput = true;
        p.Start();
        //Console.WriteLine(p.StandardOutput.ReadToEnd());
        p.WaitForExit();
    }

    // Network
    public static void OpenWebUrl(string url) {
        Process proc = new Process();
        proc.StartInfo.UseShellExecute = true;
        proc.StartInfo.FileName = url;
        proc.Start();
    }

    // Misc
    // TODO figure out a better way than this
    public static string UidGenerator(Note n) {
        return $"Note({Math.Round(n.beat, 4)},{n.col})";
    }
    [GeneratedRegex(@"Note\((.*?),(\d)\)", RegexOptions.Compiled)]
    private static partial Regex UidRegex();
    public static Note NoteFromUid(string uid) {
        var match = UidRegex().Match(uid);
        if (match.Success) {
            return new(double.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value));
        } else {
            return null;
        }
    }
    public static string NameGenerator(Note n) {
        return "N" + n.GetHashCode().ToString().Replace("-", "_"); // '-' is an invalid character for identifiers
    }
}
