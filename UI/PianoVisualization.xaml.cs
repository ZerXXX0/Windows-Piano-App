using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using PianoApp.Managers;
using PianoApp.Models;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace PianoApp.UI
{
    /// <summary>
    /// Visual representation of an 88-key piano.
    /// 
    /// Piano layout:
    /// - 52 white keys, 36 black keys
    /// - White keys are placed sequentially
    /// - Black keys are positioned between white keys (except E-F and B-C)
    /// - Keys change color based on state:
    ///   - Normal: White/Black
    ///   - Pressed: Green
    ///   - Sustained: Yellow/Orange
    /// - Keyboard overlay labels show which computer key maps to each piano key
    /// </summary>
    public partial class PianoVisualization : UserControl
    {
        private const double WhiteKeyWidth = 24;
        private const double WhiteKeyHeight = 150;
        private const double BlackKeyWidth = 14;
        private const double BlackKeyHeight = 95;

        // Maps MIDI note to its visual rectangle
        private readonly Dictionary<int, Rectangle> _keyRectangles = new();
        private readonly Dictionary<int, bool> _isBlackKey = new();
        
        // Maps MIDI note to keyboard label TextBlock
        private readonly Dictionary<int, TextBlock> _keyboardLabels = new();
        // Store key positions for label placement
        private readonly Dictionary<int, double> _keyPositions = new();

        private SustainManager? _sustainManager;
        private NoteMapper? _noteMapper;

        public PianoVisualization()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            DrawPiano();
        }

        /// <summary>
        /// Sets the sustain manager for state queries.
        /// </summary>
        public void SetSustainManager(SustainManager sustainManager)
        {
            _sustainManager = sustainManager;
        }

        /// <summary>
        /// Sets the note mapper for keyboard label display.
        /// </summary>
        public void SetNoteMapper(NoteMapper noteMapper)
        {
            _noteMapper = noteMapper;
            UpdateKeyboardLabels();
        }

        /// <summary>
        /// Draws all 88 piano keys.
        /// </summary>
        private void DrawPiano()
        {
            PianoCanvas.Children.Clear();
            _keyRectangles.Clear();
            _isBlackKey.Clear();
            _keyboardLabels.Clear();
            _keyPositions.Clear();

            var allNotes = PianoNote.GenerateAll88Keys();
            
            // First pass: calculate positions and draw white keys
            double whiteKeyX = 0;
            var whiteKeyPositions = new Dictionary<int, double>();

            foreach (var note in allNotes)
            {
                if (!note.IsBlackKey)
                {
                    whiteKeyPositions[note.MidiNote] = whiteKeyX;
                    DrawWhiteKey(note, whiteKeyX);
                    whiteKeyX += WhiteKeyWidth;
                }
            }

            // Second pass: draw black keys on top
            foreach (var note in allNotes)
            {
                if (note.IsBlackKey)
                {
                    // Black keys are positioned relative to the white key before them
                    // Find the preceding white key
                    int precedingWhiteMidi = note.MidiNote - 1;
                    if (whiteKeyPositions.TryGetValue(precedingWhiteMidi, out double whiteX))
                    {
                        double blackX = whiteX + WhiteKeyWidth - (BlackKeyWidth / 2);
                        DrawBlackKey(note, blackX);
                    }
                }
            }

            // Set canvas size
            PianoCanvas.Width = whiteKeyX;
        }

        private void DrawWhiteKey(PianoNote note, double x)
        {
            var rect = new Rectangle
            {
                Width = WhiteKeyWidth - 2,
                Height = WhiteKeyHeight,
                Fill = (Brush)FindResource("WhiteKeyBrush"),
                Stroke = (Brush)FindResource("KeyBorderBrush"),
                StrokeThickness = 1,
                RadiusX = 3,
                RadiusY = 3
            };

            Canvas.SetLeft(rect, x + 1);
            Canvas.SetTop(rect, 0);
            Canvas.SetZIndex(rect, 0);

            PianoCanvas.Children.Add(rect);
            _keyRectangles[note.MidiNote] = rect;
            _isBlackKey[note.MidiNote] = false;
            _keyPositions[note.MidiNote] = x;

            // Add label for C notes (note name)
            if (note.NoteName == "C")
            {
                var noteLabel = new TextBlock
                {
                    Text = note.FullName,
                    FontSize = 9,
                    Foreground = Brushes.Gray,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                Canvas.SetLeft(noteLabel, x + 2);
                Canvas.SetTop(noteLabel, WhiteKeyHeight - 16);
                Canvas.SetZIndex(noteLabel, 1);
                PianoCanvas.Children.Add(noteLabel);
            }

            // Create keyboard binding label (updated dynamically)
            var keyLabel = new TextBlock
            {
                Text = "",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(70, 130, 180)), // Steel blue
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Width = WhiteKeyWidth - 4
            };
            Canvas.SetLeft(keyLabel, x + 1);
            Canvas.SetTop(keyLabel, WhiteKeyHeight - 32);
            Canvas.SetZIndex(keyLabel, 1);
            PianoCanvas.Children.Add(keyLabel);
            _keyboardLabels[note.MidiNote] = keyLabel;
        }

        private void DrawBlackKey(PianoNote note, double x)
        {
            var rect = new Rectangle
            {
                Width = BlackKeyWidth,
                Height = BlackKeyHeight,
                Fill = (Brush)FindResource("BlackKeyBrush"),
                Stroke = Brushes.Black,
                StrokeThickness = 1,
                RadiusX = 2,
                RadiusY = 2
            };

            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, 0);
            Canvas.SetZIndex(rect, 2);

            PianoCanvas.Children.Add(rect);
            _keyRectangles[note.MidiNote] = rect;
            _isBlackKey[note.MidiNote] = true;
            _keyPositions[note.MidiNote] = x;

            // Create keyboard binding label for black key (Shift + key)
            var keyLabel = new TextBlock
            {
                Text = "",
                FontSize = 8,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(255, 200, 100)), // Gold/orange
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Width = BlackKeyWidth
            };
            Canvas.SetLeft(keyLabel, x);
            Canvas.SetTop(keyLabel, BlackKeyHeight - 16);
            Canvas.SetZIndex(keyLabel, 3);
            PianoCanvas.Children.Add(keyLabel);
            _keyboardLabels[note.MidiNote] = keyLabel;
        }

        /// <summary>
        /// Updates the visual state of all keys based on current play state.
        /// </summary>
        public void UpdateKeyStates()
        {
            if (_sustainManager == null)
                return;

            var heldNotes = _sustainManager.GetHeldNotes();
            var sustainedNotes = _sustainManager.GetSustainedNotes();

            foreach (var kvp in _keyRectangles)
            {
                int midiNote = kvp.Key;
                Rectangle rect = kvp.Value;
                bool isBlack = _isBlackKey[midiNote];

                Brush fill;

                if (heldNotes.Contains(midiNote))
                {
                    // Currently pressed
                    fill = (Brush)FindResource(isBlack ? "BlackKeyPressedBrush" : "WhiteKeyPressedBrush");
                }
                else if (sustainedNotes.Contains(midiNote))
                {
                    // Sustained (released while sustain pedal held)
                    fill = (Brush)FindResource(isBlack ? "BlackKeySustainedBrush" : "WhiteKeySustainedBrush");
                }
                else
                {
                    // Normal state
                    fill = (Brush)FindResource(isBlack ? "BlackKeyBrush" : "WhiteKeyBrush");
                }

                rect.Fill = fill;
            }
        }

        /// <summary>
        /// Highlights a specific key.
        /// </summary>
        public void HighlightKey(int midiNote, bool isPressed, bool isSustained)
        {
            if (!_keyRectangles.TryGetValue(midiNote, out var rect))
                return;

            bool isBlack = _isBlackKey[midiNote];
            Brush fill;

            if (isPressed)
            {
                fill = (Brush)FindResource(isBlack ? "BlackKeyPressedBrush" : "WhiteKeyPressedBrush");
            }
            else if (isSustained)
            {
                fill = (Brush)FindResource(isBlack ? "BlackKeySustainedBrush" : "WhiteKeySustainedBrush");
            }
            else
            {
                fill = (Brush)FindResource(isBlack ? "BlackKeyBrush" : "WhiteKeyBrush");
            }

            rect.Fill = fill;
        }

        /// <summary>
        /// Updates keyboard binding labels based on current transpose.
        /// Shows which computer keyboard key maps to each piano key.
        /// </summary>
        public void UpdateKeyboardLabels()
        {
            if (_noteMapper == null)
                return;

            foreach (var kvp in _keyboardLabels)
            {
                int midiNote = kvp.Key;
                TextBlock label = kvp.Value;
                bool isBlack = _isBlackKey[midiNote];

                Key? mappedKey;
                string labelText = "";

                if (isBlack)
                {
                    // Black keys use Shift + key
                    mappedKey = _noteMapper.GetKeyForSharpMidiNote(midiNote);
                    if (mappedKey.HasValue)
                    {
                        labelText = "⇧" + NoteMapper.GetKeyDisplayName(mappedKey.Value);
                    }
                }
                else
                {
                    // White keys
                    mappedKey = _noteMapper.GetKeyForMidiNote(midiNote);
                    if (mappedKey.HasValue)
                    {
                        labelText = NoteMapper.GetKeyDisplayName(mappedKey.Value);
                    }
                }

                label.Text = labelText;
            }
        }
    }
}
