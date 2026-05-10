using Edda.Const;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Edda.Classes.MapEditorNS.Stats {
    public static class TimelineAggregator {
        public static double Aggregate(List<double> strains, double strideSeconds = 0.5) {
            if (strains.Count == 0) return 0;
            var ordered = strains.OrderBy(x => x).ToList();
            var p95 = Quantile(ordered, 0.95);
            var max = ordered.Last();
            var topCount = Math.Max(1, (int)Math.Ceiling(strains.Count * 0.1));
            var topMean = ordered.TakeLast(topCount).Average();
            var highThreshold = Quantile(ordered, 0.8);
            var sustained = strains.Count(x => x >= highThreshold) * strideSeconds;
            var mean = strains.Average();
            var variance = strains.Sum(x => (x - mean) * (x - mean)) / strains.Count;
            return p95 * DifficultyPrediction.Timeline.P95Weight
                + max * DifficultyPrediction.Timeline.MaxWeight
                + topMean * DifficultyPrediction.Timeline.TopTenPercentMeanWeight
                + sustained * DifficultyPrediction.Timeline.SustainedHighStrainWeight
                + variance * DifficultyPrediction.Timeline.VarianceWeight;
        }

        private static double Quantile(List<double> sorted, double q) {
            if (sorted.Count == 0) return 0;
            var idx = (sorted.Count - 1) * q;
            var lo = (int)Math.Floor(idx);
            var hi = (int)Math.Ceiling(idx);
            if (lo == hi) return sorted[lo];
            var frac = idx - lo;
            return sorted[lo] * (1 - frac) + sorted[hi] * frac;
        }
    }
}
