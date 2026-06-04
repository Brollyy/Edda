using Edda.Classes.MapEditorNS.NoteNS;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Edda.Classes.MapEditorNS.Stats.Timeline {
    public static class TimelineAggregator {
        public static double Aggregate(TimelineStatistics statistics) {
            var raw = DifficultyWeights.Intercept
                + statistics.P95 * DifficultyWeights.P95
                + statistics.Variance * DifficultyWeights.Variance
                + statistics.RepetitionPressure * DifficultyWeights.RepetitionPressure
                + statistics.RecoveryFeasibility * DifficultyWeights.RecoveryFeasibility
                + statistics.RhythmComplexityResidual * DifficultyWeights.RhythmComplexityResidual
                + statistics.HandStrainPeak * DifficultyWeights.HandStrainPeak
                + statistics.SustainedAwkwardPressure * DifficultyWeights.SustainedAwkwardPressure
                + statistics.HandInstabilitySustain * DifficultyWeights.HandInstabilitySustain
                + statistics.TechnicalDensityAmplification * DifficultyWeights.TechnicalDensityAmplification
                + statistics.SustainedVariancePressure * DifficultyWeights.SustainedVariancePressure
                + statistics.RepetitionSustain * DifficultyWeights.RepetitionSustain;
            return ApplyFinalAdjustment(statistics, raw);
        }

        public static TimelineStatistics ExtractStatistics(List<double> strains, IReadOnlyCollection<Note> notes, double bpm, double strideSeconds = 0.5) {
            if (strains.Count == 0) return new TimelineStatistics();
            var normalizedStrains = NormalizeLocalStrains(strains);
            var ordered = normalizedStrains.OrderBy(x => x).ToList();
            var p95 = Quantile(ordered, 0.95);
            var highThreshold = p95 * 0.75;
            var sustained = p95 > 1e-9
                ? normalizedStrains.Count(x => x >= highThreshold) * strideSeconds
                : 0.0;
            var mean = normalizedStrains.Average();
            var rawVariance = normalizedStrains.Sum(x => (x - mean) * (x - mean)) / normalizedStrains.Count;
            var variance = Math.Log(1.0 + rawVariance);
            var execution = ExtractExecutionFeatures(notes, bpm, normalizedStrains, p95, strideSeconds);
            return new TimelineStatistics {
                P95 = p95,
                SustainDuration = sustained,
                Variance = variance,
                RepetitionPressure = execution.RepetitionPressure,
                RecoveryFeasibility = execution.RecoveryFeasibility,
                RhythmComplexityResidual = execution.RhythmComplexityResidual,
                HandStrainPeak = execution.HandStrainPeak,
                SustainedAwkwardPressure = execution.SustainedAwkwardPressure,
                HandInstabilitySustain = execution.HandInstabilitySustain,
                TechnicalDensityAmplification = execution.TechnicalDensityAmplification,
                SustainedVariancePressure = SustainedVariancePressure(sustained, variance, p95),
                RepetitionSustain = execution.RepetitionPressure * sustained
            };
        }

        private static ExecutionFeatures ExtractExecutionFeatures(IReadOnlyCollection<Note> notes, double bpm, List<double> strains, double p95, double strideSeconds) {
            var events = BuildPlayableEvents(notes, bpm);
            if (events.Count < 2 || bpm <= 0) return new ExecutionFeatures();
            var durationSeconds = Math.Max(strideSeconds, Math.Max(0, events.Last().Time - events.First().Time));
            var transitions = events.Zip(events.Skip(1), (previous, current) => new {
                Previous = previous,
                Current = current,
                Interval = current.Time - previous.Time,
                Delta = current.CenterColumn - previous.CenterColumn
            }).Where(transition => transition.Interval > 0).ToList();
            if (transitions.Count == 0) return new ExecutionFeatures();

            var directionReversals = 0.0;
            var highCostTransitions = 0.0;
            var unstableAlternations = 0.0;
            var handInstabilityEvents = new List<TimedCost>();
            var repeatedPressure = 0.0;
            var stableAlternations = 0.0;
            var sameLaneTransitions = 0.0;
            var narrowTransitions = 0.0;
            var flowDeltas = new List<double>();
            var awkwardCosts = new List<TimedCost>();
            var handStrainCosts = new List<TimedCost>();
            var awkwardClusterDurations = new List<double>();
            var awkwardClusterStart = 0.0;
            var awkwardClusterEnd = 0.0;
            var inAwkwardCluster = false;
            var crossoverPressure = 0.0;
            var previousDirection = 0;
            var expectedHand = new Dictionary<int, int>();
            var lastSingleByHand = new Dictionary<int, PlayableEvent>();
            for (int index = 0; index < events.Count; index++) {
                foreach (var column in events[index].Columns) {
                    var inferredHand = events[index].NoteCount == 2 ? column % 2 : index % 2;
                    if (expectedHand.TryGetValue(column, out var lastHand) && lastHand == inferredHand) {
                        unstableAlternations += 1.0;
                        handInstabilityEvents.Add(new TimedCost(events[index].Time, 1.0));
                    }
                    expectedHand[column] = inferredHand;
                }
                if (events[index].NoteCount == 1) {
                    var hand = index % 2;
                    if (lastSingleByHand.TryGetValue(hand, out var previous)) {
                        var interval = events[index].Time - previous.Time;
                        if (interval > 0 && interval <= 0.75) {
                            var laneDistance = Math.Abs(events[index].CenterColumn - previous.CenterColumn);
                            var repeatBonus = laneDistance < 0.001 ? 1.5 : 1.0;
                            handStrainCosts.Add(new TimedCost(events[index].Time, repeatBonus * (1.0 + laneDistance) / Math.Max(0.08, interval)));
                        }
                    }
                    lastSingleByHand[hand] = events[index];
                }
                highCostTransitions += Math.Max(0, events[index].NoteCount - 1);
            }

            foreach (var transition in transitions) {
                var direction = Math.Sign(transition.Delta);
                var isDirectionReversal = direction != 0 && previousDirection != 0 && direction != previousDirection && transition.Interval <= 0.35;
                if (isDirectionReversal) {
                    directionReversals += (0.35 - transition.Interval) / 0.35;
                }
                if (direction != 0) previousDirection = direction;

                if (transition.Delta == 0 && transition.Interval <= 0.30) {
                    repeatedPressure += (0.30 - transition.Interval) / 0.30;
                }

                var laneDistance = Math.Abs(transition.Delta);
                flowDeltas.Add(transition.Delta);
                if (laneDistance < 0.001) sameLaneTransitions += 1.0;
                if (laneDistance <= 1.0) narrowTransitions += 1.0;
                if (transition.Interval <= 0.30 && laneDistance > 0 && laneDistance <= 1) {
                    stableAlternations += 1.0;
                }
                if (transition.Interval <= 0.35 && (laneDistance >= 2 || transition.Delta == 0)) {
                    highCostTransitions += laneDistance + (transition.Delta == 0 ? 1.0 : 0.0);
                }
                if (transition.Interval <= 0.35 && laneDistance >= 3) {
                    crossoverPressure += (0.35 - transition.Interval) / 0.35 * laneDistance;
                }
                var transitionCost = 0.0;
                if (transition.Interval <= 0.35 && laneDistance >= 2) transitionCost += 1.0;
                if (transition.Interval <= 0.30 && transition.Delta == 0) transitionCost += 1.0;
                if (isDirectionReversal) transitionCost += 1.0;
                if (transitionCost > 0.0) {
                    awkwardCosts.Add(new TimedCost(transition.Current.Time, transitionCost));
                }
                if (transitionCost > 0.0) {
                    var start = transition.Previous.Time;
                    var end = transition.Current.Time;
                    if (!inAwkwardCluster) {
                        awkwardClusterStart = start;
                        awkwardClusterEnd = end;
                        inAwkwardCluster = true;
                    } else {
                        awkwardClusterEnd = end;
                    }
                } else if (inAwkwardCluster) {
                    awkwardClusterDurations.Add(Math.Max(0.05, awkwardClusterEnd - awkwardClusterStart));
                    inAwkwardCluster = false;
                }
            }
            if (inAwkwardCluster) {
                awkwardClusterDurations.Add(Math.Max(0.05, awkwardClusterEnd - awkwardClusterStart));
            }

            var spikeThreshold = p95 * 0.85;
            var resetThreshold = p95 * 0.35;
            var hardSpikeCount = 0;
            var feasibleRecoveryCount = 0;
            for (int index = 1; index < strains.Count; index++) {
                if (strains[index] < spikeThreshold) continue;
                hardSpikeCount++;
                var lookbackStart = Math.Max(0, index - 8);
                if (strains.Skip(lookbackStart).Take(index - lookbackStart).Any(strain => strain <= resetThreshold)) {
                    feasibleRecoveryCount++;
                }
            }
            var recoveryFeasibility = hardSpikeCount == 0 ? 1.0 : feasibleRecoveryCount / (double)hardSpikeCount;
            var rhythmEntropy = RhythmEntropy(transitions.Select(transition => transition.Interval).ToList());
            var intervalCv = CoefficientOfVariation(transitions.Select(transition => transition.Interval).ToList());
            var laneEntropy = LaneEntropy(events.SelectMany(e => e.Columns));
            var reversalRatio = Math.Min(1.0, directionReversals / Math.Max(1.0, transitions.Count));
            var flowVariance = Variance(flowDeltas);
            var streamStructure = Math.Max(
                stableAlternations / transitions.Count,
                Math.Max(sameLaneTransitions / transitions.Count, narrowTransitions / transitions.Count)
            );
            var alternationStability = 1.0 - Math.Min(1.0, unstableAlternations / Math.Max(1.0, events.Count));
            var laneSimplicity = 1.0 - 0.35 * Math.Min(1.0, laneEntropy);
            var timingRegularity = 1.0 - Math.Min(1.0, rhythmEntropy);
            var rawStreamSimplicity = timingRegularity
                * laneSimplicity
                * (0.35 + 0.65 * streamStructure)
                * alternationStability
                * (1.0 - 0.5 * reversalRatio);
            var streamSimplicity = Math.Sqrt(Math.Max(0.0, rawStreamSimplicity));
            var rhythmComplexity = rhythmEntropy * Math.Min(2.0, 1.0 + intervalCv);
            var executionInstability = directionReversals / durationSeconds
                + unstableAlternations / durationSeconds
                + Math.Sqrt(flowVariance) / Math.Max(0.5, durationSeconds);
            var ergonomicComplexity = (highCostTransitions + crossoverPressure) / durationSeconds;
            var streamSuppression = 1.0 - 0.80 * Math.Min(1.0, streamSimplicity);
            var densityNormalizer = 1.0 + 0.35 * Math.Sqrt(Math.Max(0.0, p95));
            var executionInstabilityResidual = executionInstability * streamSuppression / densityNormalizer;
            var ergonomicComplexityResidual = ergonomicComplexity * streamSuppression / densityNormalizer;
            var rhythmComplexityResidual = rhythmComplexity * streamSuppression / densityNormalizer;
            var handStrainPeak = WindowedRateQuantile(handStrainCosts, 8.0, durationSeconds, 0.90) * streamSuppression;
            var sustainedAwkwardPressure = WindowedRateQuantile(awkwardCosts, 16.0, durationSeconds, 0.75) * streamSuppression;
            var handInstabilitySustain = WindowedRateQuantile(handInstabilityEvents, 16.0, durationSeconds, 0.75) * streamSuppression;
            var baseTechnicality = executionInstabilityResidual
                + ergonomicComplexityResidual
                + rhythmComplexityResidual
                + ClusterAverage(awkwardClusterDurations) * streamSuppression;

            return new ExecutionFeatures {
                RepetitionPressure = repeatedPressure / durationSeconds,
                RecoveryFeasibility = recoveryFeasibility,
                RhythmComplexityResidual = rhythmComplexityResidual,
                HandStrainPeak = handStrainPeak,
                SustainedAwkwardPressure = sustainedAwkwardPressure,
                HandInstabilitySustain = handInstabilitySustain,
                TechnicalDensityAmplification = baseTechnicality * Math.Sqrt(Math.Max(0.0, p95 - 3.0))
            };
        }

        private static double ApplyFinalAdjustment(TimelineStatistics statistics, double prediction) {
            var marathonStrainScale = Math.Min(1.5, Math.Max(0.0, (statistics.P95 - 4.5) / 4.0));
            var marathonBoost = DifficultyWeights.MarathonSustainBoostPerMinute
                * Math.Max(0.0, statistics.SustainDuration - 240.0)
                / 60.0
                * marathonStrainScale;
            var repetitionPenalty = DifficultyWeights.RepetitionSustainPenalty
                * Math.Max(0.0, statistics.RepetitionSustain - DifficultyWeights.RepetitionSustainPenaltyThreshold);
            if (prediction >= 10.0) {
                repetitionPenalty = 0.0;
            }
            return Math.Max(0.0, prediction + marathonBoost - repetitionPenalty);
        }

        private static List<double> NormalizeLocalStrains(List<double> strains, double softCap = 8.0) {
            return strains.Select(strain => softCap * Math.Log(1.0 + Math.Max(0.0, strain) / softCap)).ToList();
        }


        private static List<PlayableEvent> BuildPlayableEvents(IEnumerable<Note> notes, double bpm, double simultaneousWindowSeconds = 0.02) {
            if (bpm <= 0) return [];
            var timedNotes = notes
                .Select(note => new TimedNote(60.0 / bpm * note.beat, note.col))
                .OrderBy(note => note.Time)
                .ThenBy(note => note.Column)
                .ToList();
            var events = new List<PlayableEvent>();
            for (var index = 0; index < timedNotes.Count; index++) {
                var current = timedNotes[index];
                if (index + 1 < timedNotes.Count) {
                    var next = timedNotes[index + 1];
                    if (next.Time - current.Time <= simultaneousWindowSeconds && next.Column != current.Column) {
                        events.Add(new PlayableEvent((current.Time + next.Time) / 2.0, (current.Column + next.Column) / 2.0, 2, [current.Column, next.Column]));
                        index++;
                        continue;
                    }
                }
                events.Add(new PlayableEvent(current.Time, current.Column, 1, [current.Column]));
            }
            return events;
        }

        private static double RhythmEntropy(List<double> intervals) {
            if (intervals.Count == 0) return 0.0;
            var buckets = intervals
                .Select(interval => Math.Round(Math.Min(1.0, interval) / 0.05))
                .GroupBy(bucket => bucket)
                .Select(group => group.Count() / (double)intervals.Count)
                .ToList();
            var entropy = -buckets.Sum(probability => probability * Math.Log(probability, 2.0));
            return entropy / Math.Log(Math.Max(2, buckets.Count), 2.0);
        }

        private static double LaneEntropy(IEnumerable<int> lanes) {
            var laneList = lanes.ToList();
            if (laneList.Count == 0) return 0.0;
            var probabilities = laneList
                .GroupBy(lane => lane)
                .Select(group => group.Count() / (double)laneList.Count)
                .ToList();
            var entropy = -probabilities.Sum(probability => probability * Math.Log(probability, 2.0));
            return entropy / Math.Log(4.0, 2.0);
        }

        private static double CoefficientOfVariation(List<double> values) {
            if (values.Count < 2) return 0.0;
            var average = values.Average();
            if (average <= 1e-9) return 0.0;
            return Math.Sqrt(Variance(values)) / average;
        }

        private static double WindowedRateQuantile(List<TimedCost> costs, double windowSeconds, double durationSeconds, double quantile) {
            if (costs.Count == 0 || durationSeconds <= 0) return 0.0;
            var orderedCosts = costs.OrderBy(cost => cost.Time).ToList();
            var rates = new List<double>();
            var costStart = Math.Max(0.0, orderedCosts[0].Time - windowSeconds);
            var costEnd = Math.Max(durationSeconds, orderedCosts[^1].Time);
            for (var start = costStart; start <= costEnd; start += Math.Max(0.5, windowSeconds / 4.0)) {
                var end = start + windowSeconds;
                var total = orderedCosts
                    .Where(cost => cost.Time >= start && cost.Time < end)
                    .Sum(cost => cost.Cost);
                rates.Add(total / windowSeconds);
            }
            return rates.Count == 0 ? 0.0 : Quantile(rates.OrderBy(value => value).ToList(), quantile);
        }

        private static double Variance(List<double> values) {
            if (values.Count < 2) return 0.0;
            var average = values.Average();
            return values.Sum(value => (value - average) * (value - average)) / values.Count;
        }

        private static double ClusterAverage(List<double> durations) {
            return durations.Count == 0 ? 0.0 : durations.Average();
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

        private class ExecutionFeatures {
            public double RepetitionPressure { get; init; }
            public double RecoveryFeasibility { get; init; }
            public double RhythmComplexityResidual { get; init; }
            public double HandStrainPeak { get; init; }
            public double SustainedAwkwardPressure { get; init; }
            public double HandInstabilitySustain { get; init; }
            public double TechnicalDensityAmplification { get; init; }
        }

        private static double SustainedVariancePressure(double sustainDuration, double variance, double p95) {
            var sustainedMinutes = Math.Max(0.0, sustainDuration - 180.0) / 60.0;
            var varianceExcess = Math.Max(0.0, variance - 1.1);
            var densityExcess = Math.Sqrt(Math.Max(0.0, p95 - 4.5));
            return sustainedMinutes * varianceExcess * densityExcess;
        }

        private record TimedNote(double Time, int Column);
        private record PlayableEvent(double Time, double CenterColumn, int NoteCount, int[] Columns);
        private record TimedCost(double Time, double Cost);
    }
}
