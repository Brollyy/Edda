using Edda.Classes.MapEditorNS.NoteNS;
using Edda.Const;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Edda.Classes.MapEditorNS.Stats {
    public static class LocalStrainCalculator {
        public static double Calculate(List<Note> windowNotes, double bpm, double windowLength) {
            if (windowNotes.Count == 0) return 0;
            var times = windowNotes.Select(n => 60d / bpm * n.beat).OrderBy(t => t).ToList();
            var intervals = times.Zip(times.Skip(1), (a, b) => b - a).Where(i => i > 0).ToList();
            var nps = windowNotes.Count / windowLength;
            var peakLocalNps = intervals.Count == 0 ? nps : 1d / Math.Max(0.001, intervals.Min());
            var intervalVar = intervals.Count > 1 ? Variance(intervals) : 0d;
            var jumps = windowNotes.Zip(windowNotes.Skip(1), (a, b) => Math.Abs(a.col - b.col)).Select(x => (double)x).ToList();
            var jumpMean = jumps.Count > 0 ? jumps.Average() : 0d;
            var jumpVar = jumps.Count > 1 ? Variance(jumps) : 0d;
            var repetition = jumps.Count > 0 ? jumps.Count(x => x == 0) / (double)jumps.Count : 1d;
            var alternation = jumps.Count > 1 ? jumps.Zip(jumps.Skip(1), (a, b) => Math.Abs(a - b) > 0 ? 1d : 0d).Average() : 0d;
            var recovery = intervals.Count > 0 ? intervals.Count(x => x > 0.30) / (double)intervals.Count : 1d;

            var speed = 0.6 * nps + 0.4 * peakLocalNps;
            var stamina = nps * (1.0 - recovery * 0.5);
            var rhythm = intervalVar + alternation;
            var awkward = jumpMean + jumpVar + repetition * 0.3;
            return speed * DifficultyPrediction.Timeline.SpeedWeight
                + stamina * DifficultyPrediction.Timeline.StaminaWeight
                + rhythm * DifficultyPrediction.Timeline.RhythmComplexityWeight
                + awkward * DifficultyPrediction.Timeline.AwkwardnessWeight;
        }

        private static double Variance(List<double> xs) {
            var mean = xs.Average();
            return xs.Sum(x => Math.Pow(x - mean, 2)) / xs.Count;
        }
    }
}
