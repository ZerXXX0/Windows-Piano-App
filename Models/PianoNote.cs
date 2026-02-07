using System.Collections.Generic;

namespace PianoApp.Models
{
    /// <summary>
    /// Represents a piano note with its properties.
    /// 
    /// MIDI Note Number Formula:
    /// MIDI = (Octave + 1) * 12 + SemitoneOffset
    /// 
    /// Where SemitoneOffset is:
    /// C=0, C#=1, D=2, D#=3, E=4, F=5, F#=6, G=7, G#=8, A=9, A#=10, B=11
    /// 
    /// Examples:
    /// - A0: (0+1)*12 + 9 = 21 (lowest piano note)
    /// - C4: (4+1)*12 + 0 = 60 (middle C)
    /// - C8: (8+1)*12 + 0 = 108 (highest piano note)
    /// </summary>
    public class PianoNote
    {
        /// <summary>
        /// Note name without octave (e.g., "C", "C#", "D")
        /// </summary>
        public string NoteName { get; }

        /// <summary>
        /// Octave number (0-8 for 88-key piano)
        /// </summary>
        public int Octave { get; }

        /// <summary>
        /// MIDI note number (21-108 for 88-key piano)
        /// </summary>
        public int MidiNote { get; }

        /// <summary>
        /// True if this is a black key (sharp note)
        /// </summary>
        public bool IsBlackKey { get; }

        /// <summary>
        /// Full note name with octave (e.g., "C4", "F#3")
        /// </summary>
        public string FullName => $"{NoteName}{Octave}";

        /// <summary>
        /// Expected WAV filename for this note
        /// </summary>
        public string FileName => $"{NoteName}{Octave}.wav";

        // Semitone offsets for each note within an octave
        private static readonly Dictionary<string, int> SemitoneOffsets = new()
        {
            {"C", 0}, {"C#", 1}, {"D", 2}, {"D#", 3}, {"E", 4}, {"F", 5},
            {"F#", 6}, {"G", 7}, {"G#", 8}, {"A", 9}, {"A#", 10}, {"B", 11}
        };

        // All note names in chromatic order
        private static readonly string[] ChromaticNotes = 
            { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

        // White key note names (no sharps)
        private static readonly HashSet<string> WhiteKeyNames = 
            new() { "C", "D", "E", "F", "G", "A", "B" };

        public PianoNote(string noteName, int octave)
        {
            NoteName = noteName;
            Octave = octave;
            IsBlackKey = noteName.Contains('#');
            
            // Calculate MIDI note: (Octave + 1) * 12 + SemitoneOffset
            MidiNote = (octave + 1) * 12 + SemitoneOffsets[noteName];
        }

        /// <summary>
        /// Creates a PianoNote from a MIDI note number.
        /// </summary>
        public static PianoNote FromMidi(int midiNote)
        {
            // Reverse the MIDI formula to get octave and semitone
            int octave = (midiNote / 12) - 1;
            int semitone = midiNote % 12;
            string noteName = ChromaticNotes[semitone];
            return new PianoNote(noteName, octave);
        }

        /// <summary>
        /// Returns true if the given note name has a valid sharp.
        /// E and B do not have sharps (E# = F, B# = C).
        /// </summary>
        public static bool HasSharp(string noteName)
        {
            // Only C, D, F, G, A have sharps
            // E# = F and B# = C, so E and B don't have sharps
            return noteName == "C" || noteName == "D" || 
                   noteName == "F" || noteName == "G" || noteName == "A";
        }

        /// <summary>
        /// Gets the sharp version of a white key note.
        /// Returns null if the note has no sharp (E or B).
        /// </summary>
        public static PianoNote? GetSharp(string whiteNoteName, int octave)
        {
            if (!HasSharp(whiteNoteName))
                return null;
            
            return new PianoNote(whiteNoteName + "#", octave);
        }

        /// <summary>
        /// Generates all 88 piano notes from A0 to C8.
        /// </summary>
        public static List<PianoNote> GenerateAll88Keys()
        {
            var notes = new List<PianoNote>();
            
            // 88-key piano: A0 (MIDI 21) to C8 (MIDI 108)
            for (int midi = 21; midi <= 108; midi++)
            {
                notes.Add(FromMidi(midi));
            }
            
            return notes;
        }

        public override string ToString() => FullName;

        public override bool Equals(object? obj)
        {
            return obj is PianoNote note && MidiNote == note.MidiNote;
        }

        public override int GetHashCode() => MidiNote;
    }
}
