using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using PianoApp.Audio;
using PianoApp.Models;

namespace PianoApp.Managers
{
    /// <summary>
    /// Handles keyboard input for the piano application.
    /// 
    /// Responsibilities:
    /// - Processes KeyDown/KeyUp events
    /// - Tracks currently pressed keys to prevent key repeat
    /// - Tracks Shift state for sharp notes
    /// - Tracks Space state for sustain pedal
    /// - Coordinates with NoteMapper, SustainManager, and AudioEngine
    /// </summary>
    public class KeyboardInputManager
    {
        private readonly NoteMapper _noteMapper;
        private readonly SustainManager _sustainManager;
        private readonly AudioEngine _audioEngine;

        /// <summary>
        /// Tracks which keyboard keys are currently held down.
        /// Maps Key to the MIDI note it triggered (to handle transpose during key hold).
        /// </summary>
        private readonly Dictionary<Key, int> _pressedKeys = new();

        /// <summary>
        /// Current state of the Shift key.
        /// </summary>
        public bool IsShiftPressed { get; private set; }

        /// <summary>
        /// Current state of the sustain pedal (Space).
        /// </summary>
        public bool IsSustainActive => _sustainManager.IsActive;

        /// <summary>
        /// Event raised when transpose changes.
        /// </summary>
        public event Action? TransposeChanged;

        /// <summary>
        /// Event raised when sustain state changes.
        /// </summary>
        public event Action? SustainChanged;

        /// <summary>
        /// Event raised when any note state changes (for UI updates).
        /// </summary>
        public event Action? NoteStateChanged;

        public KeyboardInputManager(NoteMapper noteMapper, SustainManager sustainManager, AudioEngine audioEngine)
        {
            _noteMapper = noteMapper;
            _sustainManager = sustainManager;
            _audioEngine = audioEngine;
        }

        /// <summary>
        /// Handles KeyDown events.
        /// </summary>
        public void HandleKeyDown(KeyEventArgs e)
        {
            Key key = e.Key == Key.System ? e.SystemKey : e.Key;

            // Track Shift state
            if (key == Key.LeftShift || key == Key.RightShift)
            {
                IsShiftPressed = true;
                return;
            }

            // Sustain pedal (Space) - Toggle mode
            if (key == Key.Space)
            {
                if (_sustainManager.IsActive)
                {
                    // Turn sustain OFF
                    _sustainManager.Deactivate();
                    _audioEngine.ReleaseSustainedNotes();
                }
                else
                {
                    // Turn sustain ON
                    _sustainManager.Activate();
                }
                SustainChanged?.Invoke();
                NoteStateChanged?.Invoke();
                e.Handled = true;
                return;
            }

            // Transpose controls
            if (key == Key.OemOpenBrackets)  // [ = transpose down
            {
                _noteMapper.TransposeDown();
                TransposeChanged?.Invoke();
                return;
            }
            if (key == Key.OemCloseBrackets)  // ] = transpose up
            {
                _noteMapper.TransposeUp();
                TransposeChanged?.Invoke();
                return;
            }
            if (key == Key.D0 && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                // Ctrl+0 = reset transpose
                _noteMapper.ResetTranspose();
                TransposeChanged?.Invoke();
                e.Handled = true;
                return;
            }

            // Prevent key repeat - if this key is already pressed, ignore
            if (_pressedKeys.ContainsKey(key))
                return;

            // Map key to piano note
            PianoNote? note = _noteMapper.MapKeyToNote(key, IsShiftPressed);
            if (note == null)
                return;

            // Check if we have a sample for this note
            if (!_audioEngine.HasSample(note.MidiNote))
                return;

            // Record the key press with the MIDI note it triggered
            _pressedKeys[key] = note.MidiNote;

            // Register with sustain manager and play
            _sustainManager.NoteOn(note.MidiNote);
            _audioEngine.NoteOn(note.MidiNote);

            NoteStateChanged?.Invoke();
            e.Handled = true;
        }

        /// <summary>
        /// Handles KeyUp events.
        /// </summary>
        public void HandleKeyUp(KeyEventArgs e)
        {
            Key key = e.Key == Key.System ? e.SystemKey : e.Key;

            // Track Shift state
            if (key == Key.LeftShift || key == Key.RightShift)
            {
                IsShiftPressed = false;
                return;
            }

            // Sustain pedal is now toggle mode - ignore KeyUp for Space
            if (key == Key.Space)
            {
                return;
            }

            // Check if this key was pressed
            if (!_pressedKeys.TryGetValue(key, out int midiNote))
                return;

            // Remove from pressed keys
            _pressedKeys.Remove(key);

            // Release the note
            _sustainManager.NoteOff(midiNote);
            _audioEngine.NoteOff(midiNote, _sustainManager.IsActive);

            NoteStateChanged?.Invoke();
        }

        /// <summary>
        /// Gets the current transpose value.
        /// </summary>
        public int CurrentTranspose => _noteMapper.Transpose;

        /// <summary>
        /// Releases all currently pressed keys.
        /// Called when window loses focus.
        /// </summary>
        public void ReleaseAllKeys()
        {
            foreach (var kvp in _pressedKeys.ToList())
            {
                _sustainManager.NoteOff(kvp.Value);
                _audioEngine.NoteOff(kvp.Value, _sustainManager.IsActive);
            }
            _pressedKeys.Clear();
            IsShiftPressed = false;

            NoteStateChanged?.Invoke();
        }
    }
}
