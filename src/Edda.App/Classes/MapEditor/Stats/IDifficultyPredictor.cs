using Edda.Classes.MapEditorNS.NoteNS;
using System;
using System.Collections.Generic;

namespace Edda.Classes.MapEditorNS.Stats {
    public interface IDifficultyPredictor {
        Features GetSupportedFeatures();
        float? PredictDifficulty(IReadOnlyCollection<Note> notes, double globalBPM, double songDuration);


        [Flags]
        public enum Features {
            None = 0,
            PreciseFloat = 1, // supports "precise" predictions with float values
            AlwaysPredict = 2, // guarantees to always return a valid value
            RealTime = 4 // supports real-time difficulty prediction of incomplete maps
        }
    }
}
