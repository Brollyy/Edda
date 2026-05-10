using Edda.Classes.MapEditorNS.NoteNS;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static Edda.Classes.MapEditorNS.Stats.IDifficultyPredictor.Features;

namespace Edda.Classes.MapEditorNS.Stats {
    public class TimelineDifficultyPredictor : IDifficultyPredictor {
        public static readonly TimelineDifficultyPredictor SINGLETON = new();

        public IDifficultyPredictor.Features GetSupportedFeatures() => PreciseFloat | AlwaysPredict | RealTime;

        public float? PredictDifficulty(MapEditor mapEditor, int difficultyIndex) {
            var diff = mapEditor.GetDifficulty(difficultyIndex);
            return PredictDifficulty(diff.notes.ToList(), mapEditor.GlobalBPM, mapEditor.SongDuration);
        }

        public float? PredictDifficulty(IReadOnlyCollection<Note> notes, double globalBpm, double songDuration) {
            if (notes.Count == 0 || globalBpm <= 0) return 0;
            var windows = WindowBuilder.BuildWindows(notes, globalBpm, songDuration);
            var strains = windows.Select(w => LocalStrainCalculator.Calculate(w, globalBpm, 4.0)).ToList();
            var score = TimelineAggregator.Aggregate(strains);
            return (float)Math.Max(0, score);
        }

        public void ExportDebugTimelineCsv(IReadOnlyCollection<Note> notes, double globalBpm, double songDuration, string path) {
            var windows = WindowBuilder.BuildWindows(notes, globalBpm, songDuration);
            using var writer = new StreamWriter(path);
            writer.WriteLine("window_index,strain");
            for (int i = 0; i < windows.Count; i++) {
                var s = LocalStrainCalculator.Calculate(windows[i], globalBpm, 4.0);
                writer.WriteLine($"{i},{s:0.######}");
            }
        }
    }
}
