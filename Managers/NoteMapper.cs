using System;
using System.Collections.Generic;
using System.Windows.Input;
using PianoApp.Models;

namespace PianoApp.Managers
{
    /// <summary>
    /// Maps keyboard keys to piano notes.
    /// 
    /// Mapping rules:
    /// - Each keyboard key maps to a WHITE piano key
    /// - White keys progress sequentially across octaves (after B comes C of next octave)
    /// - Shift + key produces the SHARP of that note if valid
    /// - E and B have no sharps (Shift does nothing)
    /// - Transpose is applied globally to all notes
    /// 
    /// Keyboard layout (white keys only):
    /// 
    /// Number row (1-0): C2, D2, E2, F2, G2, A2, B2, C3, D3, E3
    /// Q row (Q-P):      F3, G3, A3, B3, C4, D4, E4, F4, G4, A4
    /// A row (A-L):      B4, C5, D5, E5, F5, G5, A5, B5, C6
    /// Z row (Z-M):      D6, E6, F6, G6, A6, B6, C7
    /// </summary>
    public class NoteMapper
    {
        /// <summary>
        /// Current transpose value in semitones.
        /// Range: -36 to +36
        /// </summary>
        public int Transpose { get; private set; } = 0;

        public const int MinTranspose = -36;
        public const int MaxTranspose = 36;

        /// <summary>
        /// Maps keyboard keys to their corresponding white piano notes.
        /// The tuple contains (NoteName, Octave) for the white key.
        /// </summary>
        private static readonly Dictionary<Key, (string NoteName, int Octave)> KeyToWhiteNote = new()
        {
            // Number row: C2 - E3
            { Key.D1, ("C", 2) },
            { Key.D2, ("D", 2) },
            { Key.D3, ("E", 2) },
            { Key.D4, ("F", 2) },
            { Key.D5, ("G", 2) },
            { Key.D6, ("A", 2) },
            { Key.D7, ("B", 2) },
            { Key.D8, ("C", 3) },
            { Key.D9, ("D", 3) },
            { Key.D0, ("E", 3) },

            // Q row: F3 - A4
            { Key.Q, ("F", 3) },
            { Key.W, ("G", 3) },
            { Key.E, ("A", 3) },
            { Key.R, ("B", 3) },
            { Key.T, ("C", 4) },
            { Key.Y, ("D", 4) },
            { Key.U, ("E", 4) },
            { Key.I, ("F", 4) },
            { Key.O, ("G", 4) },
            { Key.P, ("A", 4) },

            // A row: B4 - C6
            { Key.A, ("B", 4) },
            { Key.S, ("C", 5) },
            { Key.D, ("D", 5) },
            { Key.F, ("E", 5) },
            { Key.G, ("F", 5) },
            { Key.H, ("G", 5) },
            { Key.J, ("A", 5) },
            { Key.K, ("B", 5) },
            { Key.L, ("C", 6) },

            // Z row: D6 - C7
            { Key.Z, ("D", 6) },
            { Key.X, ("E", 6) },
            { Key.C, ("F", 6) },
            { Key.V, ("G", 6) },
            { Key.B, ("A", 6) },
            { Key.N, ("B", 6) },
            { Key.M, ("C", 7) },
        };

        /// <summary>
        /// Maps a keyboard key to a piano note, considering shift and transpose.
        /// </summary>
        /// <param name="key">The keyboard key pressed</param>
        /// <param name="shiftPressed">True if Shift is held for sharp</param>
        /// <returns>The resulting PianoNote, or null if the key is not mapped</returns>
        public PianoNote? MapKeyToNote(Key key, bool shiftPressed)
        {
            if (!KeyToWhiteNote.TryGetValue(key, out var whiteNote))
                return null;

            string noteName = whiteNote.NoteName;
            int octave = whiteNote.Octave;

            // Apply sharp if shift is pressed and the note has a sharp
            // E and B do NOT have sharps (E# = F, B# = C conceptually, but we don't auto-increment)
            if (shiftPressed && PianoNote.HasSharp(noteName))
            {
                noteName += "#";
            }

            // Create the base note
            var note = new PianoNote(noteName, octave);

            // Apply transpose
            int transposedMidi = note.MidiNote + Transpose;

            // Clamp to valid piano range (A0=21 to C8=108)
            if (transposedMidi < 21 || transposedMidi > 108)
                return null;

            return PianoNote.FromMidi(transposedMidi);
        }

        /// <summary>
        /// Increases transpose by 1 semitone.
        /// </summary>
        public void TransposeUp()
        {
            if (Transpose < MaxTranspose)
                Transpose++;
        }

        /// <summary>
        /// Decreases transpose by 1 semitone.
        /// </summary>
        public void TransposeDown()
        {
            if (Transpose > MinTranspose)
                Transpose--;
        }

        /// <summary>
        /// Resets transpose to 0.
        /// </summary>
        public void ResetTranspose()
        {
            Transpose = 0;
        }

        /// <summary>
        /// Sets transpose to a specific value.
        /// </summary>
        public void SetTranspose(int value)
        {
            Transpose = Math.Clamp(value, MinTranspose, MaxTranspose);
        }

        /// <summary>
        /// Gets all mapped keyboard keys.
        /// </summary>
        public static IEnumerable<Key> GetMappedKeys() => KeyToWhiteNote.Keys;

        /// <summary>
        /// Gets the white note for a keyboard key (without transpose).
        /// Used for UI display.
        /// </summary>
        public static (string NoteName, int Octave)? GetWhiteNoteForKey(Key key)
        {
            return KeyToWhiteNote.TryGetValue(key, out var note) ? note : null;
        }

        /// <summary>
        /// Gets the keyboard key that maps to a specific MIDI note (considering current transpose).
        /// Used for UI display of key bindings.
        /// </summary>
        public Key? GetKeyForMidiNote(int midiNote)
        {
            foreach (var kvp in KeyToWhiteNote)
            {
                var baseNote = new PianoNote(kvp.Value.NoteName, kvp.Value.Octave);
                if (baseNote.MidiNote + Transpose == midiNote)
                    return kvp.Key;
            }
            return null;
        }

        /// <summary>
        /// Gets the keyboard key that maps to a specific MIDI note for a sharp (considering transpose).
        /// Returns the key that when Shift is held produces this sharp note.
        /// </summary>
        public Key? GetKeyForSharpMidiNote(int midiNote)
        {
            foreach (var kvp in KeyToWhiteNote)
            {
                string noteName = kvp.Value.NoteName;
                int octave = kvp.Value.Octave;
                
                // Only notes with sharps (C, D, F, G, A)
                if (!PianoNote.HasSharp(noteName))
                    continue;
                
                var sharpNote = new PianoNote(noteName + "#", octave);
                if (sharpNote.MidiNote + Transpose == midiNote)
                    return kvp.Key;
            }
            return null;
        }

        /// <summary>
        /// Gets a display-friendly string for a keyboard key.
        /// </summary>
        public static string GetKeyDisplayName(Key key)
        {
            return key switch
            {
                Key.D0 => "0",
                Key.D1 => "1",
                Key.D2 => "2",
                Key.D3 => "3",
                Key.D4 => "4",
                Key.D5 => "5",
                Key.D6 => "6",
                Key.D7 => "7",
                Key.D8 => "8",
                Key.D9 => "9",
                _ => key.ToString()
            };
        }
    }
}
