using Edda.Classes.MapEditorNS.NoteNS;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Edda.Classes.MapEditorNS.Stats {
    public static class WindowBuilder {
        public static List<List<Note>> BuildWindows(IReadOnlyCollection<Note> notes, double globalBpm, double songDuration, double windowLength = 4.0, double stride = 0.5) {
            var ordered = notes.OrderBy(n => n.beat).ToList();
            var windows = new List<List<Note>>();
            var maxDuration = Math.Max(songDuration, ordered.Count > 0 ? 60d / globalBpm * ordered.Last().beat : 0d);
            var start = 0d;
            do {
                var end = start + windowLength;
                windows.Add(ordered.Where(n => {
                    var t = 60d / globalBpm * n.beat;
                    return t >= start && t < end;
                }).ToList());
                start += stride;
            } while (start < maxDuration);
            return windows;
        }
    }
}
