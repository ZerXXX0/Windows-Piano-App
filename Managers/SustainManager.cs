using System.Collections.Generic;
using System.Linq;

namespace PianoApp.Managers
{
    /// <summary>
    /// Manages the sustain pedal behavior.
    /// 
    /// Sustain Logic:
    /// - Space down → sustain ON
    /// - Space up → sustain OFF
    /// - While sustain is ON:
    ///   - Releasing a key does NOT stop the note
    ///   - Notes enter a "sustained" state (still playing but not held)
    /// - When sustain turns OFF:
    ///   - Stop all sustained notes that are not currently held
    /// - Supports full polyphony
    /// 
    /// Note States:
    /// - Held: Key is physically pressed
    /// - Sustained: Key released while sustain was ON, note continues
    /// - Released: Note is not playing
    /// </summary>
    public class SustainManager
    {
        /// <summary>
        /// Whether the sustain pedal is currently active.
        /// </summary>
        public bool IsActive { get; private set; }

        /// <summary>
        /// Notes that are currently held (key is physically pressed).
        /// </summary>
        private readonly HashSet<int> _heldNotes = new();

        /// <summary>
        /// Notes that are sustained (released while sustain was on).
        /// </summary>
        private readonly HashSet<int> _sustainedNotes = new();

        /// <summary>
        /// Activates the sustain pedal.
        /// </summary>
        public void Activate()
        {
            IsActive = true;
        }

        /// <summary>
        /// Deactivates the sustain pedal.
        /// Returns the list of notes that should be stopped.
        /// </summary>
        public List<int> Deactivate()
        {
            IsActive = false;

            // Get all sustained notes that need to be stopped
            var notesToStop = _sustainedNotes.ToList();
            _sustainedNotes.Clear();

            return notesToStop;
        }

        /// <summary>
        /// Called when a note starts playing (key pressed).
        /// </summary>
        public void NoteOn(int midiNote)
        {
            _heldNotes.Add(midiNote);
            // If it was in sustained state, it's now held again
            _sustainedNotes.Remove(midiNote);
        }

        /// <summary>
        /// Called when a key is released.
        /// If sustain is active, the note moves to sustained state.
        /// </summary>
        public void NoteOff(int midiNote)
        {
            _heldNotes.Remove(midiNote);

            if (IsActive)
            {
                // Move to sustained state - continues playing
                _sustainedNotes.Add(midiNote);
            }
            // If sustain is not active, the note should be stopped
            // (handled by AudioEngine)
        }

        /// <summary>
        /// Gets all notes that are currently held (key pressed).
        /// </summary>
        public HashSet<int> GetHeldNotes()
        {
            return new HashSet<int>(_heldNotes);
        }

        /// <summary>
        /// Gets all notes that are sustained (released while sustain on).
        /// </summary>
        public HashSet<int> GetSustainedNotes()
        {
            return new HashSet<int>(_sustainedNotes);
        }

        /// <summary>
        /// Checks if a note is currently held.
        /// </summary>
        public bool IsNoteHeld(int midiNote) => _heldNotes.Contains(midiNote);

        /// <summary>
        /// Checks if a note is currently sustained.
        /// </summary>
        public bool IsNoteSustained(int midiNote) => _sustainedNotes.Contains(midiNote);

        /// <summary>
        /// Clears all held and sustained notes.
        /// </summary>
        public void Reset()
        {
            _heldNotes.Clear();
            _sustainedNotes.Clear();
            IsActive = false;
        }
    }
}
