using Edda.Const;
using System;
using System.Globalization;
using System.IO;

namespace Edda.Settings;

public static class SettingsBootstrapper {
    public static void EnsureDefaults(UserSettingsManager userSettings) {
        EnsureValue(userSettings, UserSettingsKey.EnableSpectrogram, DefaultUserSettings.EnableSpectrogram);
        EnsureValue(userSettings, UserSettingsKey.DefaultMapper, DefaultUserSettings.DefaultMapper);
        EnsureParsableDouble(userSettings, UserSettingsKey.DefaultNoteSpeed, DefaultUserSettings.DefaultNoteSpeed);
        EnsureParsableDouble(userSettings, UserSettingsKey.DefaultGridSpacing, DefaultUserSettings.DefaultGridSpacing);
        EnsureValue(userSettings, UserSettingsKey.NotePasteBehavior, DefaultUserSettings.NotePasteBehavior);
        EnsureParsableInt(userSettings, UserSettingsKey.EditorAudioLatency, DefaultUserSettings.AudioLatency);
        EnsureValue(userSettings, UserSettingsKey.PanDrumSounds, DefaultUserSettings.PanDrumSounds);
        EnsureParsableDouble(userSettings, UserSettingsKey.DefaultSongVolume, DefaultUserSettings.DefaultSongVolume);
        EnsureParsableDouble(userSettings, UserSettingsKey.DefaultNoteVolume, DefaultUserSettings.DefaultNoteVolume);
        EnsureValue(userSettings, UserSettingsKey.DrumSampleFile, DefaultUserSettings.DrumSampleFile);
        EnsureValue(userSettings, UserSettingsKey.SpectrogramCache, DefaultUserSettings.SpectrogramCache);
        EnsureValue(userSettings, UserSettingsKey.SpectrogramType, DefaultUserSettings.SpectrogramType);
        EnsureValue(userSettings, UserSettingsKey.SpectrogramQuality, DefaultUserSettings.SpectrogramQuality);
        EnsureParsableInt(userSettings, UserSettingsKey.SpectrogramFrequency, DefaultUserSettings.SpectrogramFrequency);
        EnsureValue(userSettings, UserSettingsKey.SpectrogramColormap, DefaultUserSettings.SpectrogramColormap);
        EnsureValue(userSettings, UserSettingsKey.SpectrogramFlipped, DefaultUserSettings.SpectrogramFlipped);
        EnsureValue(userSettings, UserSettingsKey.SpectrogramChunking, DefaultUserSettings.SpectrogramChunking);
        EnsureValue(userSettings, UserSettingsKey.EnableDiscordRPC, DefaultUserSettings.EnableDiscordRPC);
        EnsureValue(userSettings, UserSettingsKey.EnableAutosave, DefaultUserSettings.EnableAutosave);
        EnsureValue(userSettings, UserSettingsKey.CheckForUpdates, DefaultUserSettings.CheckForUpdates);
        EnsureMapSaveLocation(userSettings);
        EnsureValue(userSettings, UserSettingsKey.DifficultyPredictorAlgorithm, DefaultUserSettings.DifficultyPredictorAlgorithm);
        EnsureValue(userSettings, UserSettingsKey.DifficultyPredictorShowPrecise, DefaultUserSettings.DifficultyPredictorShowPrecise);
        EnsureValue(userSettings, UserSettingsKey.DifficultyPredictorShowInMapStats, DefaultUserSettings.DifficultyPredictorShowInMapStats);
        EnsureValue(userSettings, UserSettingsKey.EnableNavWaveform, DefaultUserSettings.EnableNavWaveform);
        EnsureValue(userSettings, UserSettingsKey.EnableNavBookmarks, DefaultUserSettings.EnableNavBookmarks);
        EnsureValue(userSettings, UserSettingsKey.EnableNavBPMChanges, DefaultUserSettings.EnableNavBPMChanges);
        EnsureValue(userSettings, UserSettingsKey.EnableNavNotes, DefaultUserSettings.EnableNavNotes);
        userSettings.Write();
    }

    static void EnsureValue(UserSettingsManager userSettings, string key, string defaultValue) {
        if (userSettings.GetValueForKey(key) == null) {
            userSettings.SetValueForKey(key, defaultValue);
        }
    }

    static void EnsureValue(UserSettingsManager userSettings, string key, bool defaultValue) {
        if (userSettings.GetValueForKey(key) == null) {
            userSettings.SetValueForKey(key, defaultValue);
        }
    }

    static void EnsureValue(UserSettingsManager userSettings, string key, int defaultValue) {
        if (userSettings.GetValueForKey(key) == null) {
            userSettings.SetValueForKey(key, defaultValue.ToString(CultureInfo.InvariantCulture));
        }
    }

    static void EnsureParsableDouble(UserSettingsManager userSettings, string key, double defaultValue) {
        var currentValue = userSettings.GetValueForKey(key);
        if (currentValue == null || !double.TryParse(currentValue, CultureInfo.InvariantCulture, out _)) {
            userSettings.SetValueForKey(key, defaultValue);
        }
    }

    static void EnsureParsableInt(UserSettingsManager userSettings, string key, int defaultValue) {
        var currentValue = userSettings.GetValueForKey(key);
        if (currentValue == null || !int.TryParse(currentValue, CultureInfo.InvariantCulture, out _)) {
            userSettings.SetValueForKey(key, defaultValue.ToString(CultureInfo.InvariantCulture));
        }
    }

    static void EnsureMapSaveLocation(UserSettingsManager userSettings) {
        var indexText = userSettings.GetValueForKey(UserSettingsKey.MapSaveLocationIndex);
        var path = userSettings.GetValueForKey(UserSettingsKey.MapSaveLocationPath);
        var invalidSelection =
            !int.TryParse(indexText, CultureInfo.InvariantCulture, out var index) ||
            index < 0 ||
            index > 1 ||
            (index == 1 && string.IsNullOrWhiteSpace(path)) ||
            (index == 1 && !Directory.Exists(path));

        if (invalidSelection) {
            userSettings.SetValueForKey(UserSettingsKey.MapSaveLocationIndex, DefaultUserSettings.MapSaveLocationIndex.ToString(CultureInfo.InvariantCulture));
            userSettings.SetValueForKey(UserSettingsKey.MapSaveLocationPath, DefaultUserSettings.MapSaveLocationPath);
        }
    }
}