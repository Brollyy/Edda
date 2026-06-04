using Edda.Classes.MapEditorNS.NoteNS;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Edda.Classes.MapEditorNS.Stats.Timeline {
    public static class WindowBuilder {
        public static List<List<Note>> BuildWindows(IReadOnlyCollection<Note> notes, double globalBpm, double windowLength = 4.0, double stride = 0.5) {
            if (notes.Count == 0 || globalBpm <= 0) return [];
            var ordered = notes
                .OrderBy(n => n.beat)
                .Select(n => new TimedNote(n, 60d / globalBpm * n.beat))
                .ToList();
            var windows = new List<List<Note>>();
            var firstNoteTime = ordered[0].Time;
            var lastNoteTime = ordered[^1].Time;
            var start = firstNoteTime;
            var startIndex = 0;
            var endIndex = 0;
            do {
                var end = start + windowLength;
                while (startIndex < ordered.Count && ordered[startIndex].Time < start) {
                    startIndex++;
                }
                endIndex = Math.Max(endIndex, startIndex);
                while (endIndex < ordered.Count && ordered[endIndex].Time < end) {
                    endIndex++;
                }
                var window = new List<Note>(endIndex - startIndex);
                for (var index = startIndex; index < endIndex; index++) {
                    window.Add(ordered[index].Note);
                }
                windows.Add(window);
                start += stride;
            } while (start <= lastNoteTime);
            return windows;
        }

        private record TimedNote(Note Note, double Time);
    }
}
