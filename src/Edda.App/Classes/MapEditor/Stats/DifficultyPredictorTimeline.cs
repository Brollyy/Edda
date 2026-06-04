using Edda.Classes.MapEditorNS.NoteNS;
using System;
using System.Collections.Generic;
using System.Linq;
using Edda.Classes.MapEditorNS.Stats.Timeline;
using static Edda.Classes.MapEditorNS.Stats.IDifficultyPredictor.Features;

namespace Edda.Classes.MapEditorNS.Stats {
    public class DifficultyPredictorTimeline : IDifficultyPredictor {
        public static readonly DifficultyPredictorTimeline SINGLETON = new();

        public IDifficultyPredictor.Features GetSupportedFeatures() => PreciseFloat | AlwaysPredict | RealTime;

        public float? PredictDifficulty(MapEditor mapEditor, int difficultyIndex) {
            var diff = mapEditor.GetDifficulty(difficultyIndex);
            if (diff == null) return 0;
            return PredictDifficulty(diff.notes.ToList(), mapEditor.GlobalBPM);
        }

        private float? PredictDifficulty(IReadOnlyCollection<Note> notes, double globalBpm) {
            if (notes.Count == 0 || globalBpm <= 0) return 0;
            var score = TimelineAggregator.Aggregate(ExtractTimelineStatistics(notes, globalBpm));
            return (float)Math.Max(0, score);
        }

        public TimelineStatistics ExtractTimelineStatistics(IReadOnlyCollection<Note> notes, double globalBpm) {
            if (notes.Count == 0 || globalBpm <= 0) return new TimelineStatistics();
            var windows = WindowBuilder.BuildWindows(notes, globalBpm);
            var strains = windows.Select(w => LocalStrainCalculator.Calculate(w, globalBpm, 4.0)).ToList();
            return TimelineAggregator.ExtractStatistics(strains, notes, globalBpm);
        }
    }
}
