using System;
using System.Linq;
using System.Windows;
using PianoApp.Audio;
using PianoApp.Managers;
using System.Windows.Input;
using System.Windows.Media;

namespace PianoApp
{
    /// <summary>
    /// Main application window.
    /// 
    /// Integrates all components:
    /// - AudioEngine: Buffer-based audio playback with WASAPI
    /// - NoteMapper: Keyboard key to piano note mapping with transpose
    /// - SustainManager: Tracks held and sustained notes
    /// - KeyboardInputManager: Processes keyboard events
    /// - PianoVisualization: Visual keyboard display
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly AudioEngine _audioEngine;
        private readonly NoteMapper _noteMapper;
        private readonly SustainManager _sustainManager;
        private readonly KeyboardInputManager _inputManager;

        public MainWindow()
        {
            InitializeComponent();

            // Initialize components
            _audioEngine = new AudioEngine();
            _noteMapper = new NoteMapper();
            _sustainManager = new SustainManager();
            _inputManager = new KeyboardInputManager(_noteMapper, _sustainManager, _audioEngine);

            // Wire up events
            _inputManager.TransposeChanged += OnTransposeChanged;
            _inputManager.SustainChanged += OnSustainChanged;
            _inputManager.NoteStateChanged += OnNoteStateChanged;

            // Initialize
            Loaded += OnWindowLoaded;
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            // Set up piano visualization
            PianoView.SetSustainManager(_sustainManager);
            PianoView.SetNoteMapper(_noteMapper);

            // Load saved settings
            var settings = SettingsManager.Load();

            // Populate sample pack selector
            PopulateSamplePacks(settings.SelectedSamplePack);

            // Load the selected sample pack
            LoadSamplePack(_audioEngine.CurrentSamplePack);

            // Restore transpose setting
            if (settings.Transpose != 0)
            {
                _noteMapper.SetTranspose(settings.Transpose);
                UpdateTransposeDisplay();
                PianoView.UpdateKeyboardLabels();
            }

            // Start audio engine
            _audioEngine.Start();

            // Focus window to receive keyboard input
            Focus();
        }

        private void PopulateSamplePacks(string selectedPack)
        {
            var packs = AudioEngine.GetAvailableSamplePacks();
            SamplePackCombo.Items.Clear();
            
            foreach (var pack in packs)
            {
                SamplePackCombo.Items.Add(pack);
            }
            
            if (packs.Count > 0)
            {
                // Select the saved pack, or first available
                int index = packs.IndexOf(selectedPack);
                SamplePackCombo.SelectedIndex = index >= 0 ? index : 0;
            }
            else
            {
                SamplePackCombo.Items.Add("(No packs found)");
                SamplePackCombo.SelectedIndex = 0;
                SamplePackCombo.IsEnabled = false;
            }
        }

        private void LoadSamplePack(string packName)
        {
            var missingSamples = _audioEngine.PreloadSamples(packName);

            if (missingSamples.Count > 0)
            {
                string message = $"Missing {missingSamples.Count} audio samples:\n\n";
                message += string.Join("\n", missingSamples.Take(10));
                if (missingSamples.Count > 10)
                    message += $"\n... and {missingSamples.Count - 10} more";
                message += $"\n\nExpected location: {AudioEngine.GetSamplePackPath(packName)}";
                message += "\n\nThe application will continue, but some notes may not play.";

                MessageBox.Show(message, "Missing Audio Samples", MessageBoxButton.OK, MessageBoxImage.Warning);
                StatusText.Text = $"Pack: {packName} ({88 - missingSamples.Count}/88 samples)";
            }
            else
            {
                StatusText.Text = $"Pack: {packName} - All 88 samples loaded";
            }

            // Save the selected pack
            SettingsManager.SaveSelectedSamplePack(packName);
        }

        private void SamplePackCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (SamplePackCombo.SelectedItem is string packName && !string.IsNullOrEmpty(packName) && !packName.StartsWith("("))
            {
                // Only reload if different from current
                if (packName != _audioEngine.CurrentSamplePack)
                {
                    // Release all notes before switching
                    _sustainManager.Reset();
                    _inputManager.ReleaseAllKeys();
                    
                    // Load the new sample pack
                    LoadSamplePack(packName);
                }
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            _inputManager.HandleKeyDown(e);
        }

        private void Window_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            _inputManager.HandleKeyUp(e);
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            // Release all keys when window loses focus
            _inputManager.ReleaseAllKeys();
            UpdateSustainDisplay();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _audioEngine.Dispose();
        }

        private void OnTransposeChanged()
        {
            UpdateTransposeDisplay();
            
            // Update keyboard labels on piano visualization
            PianoView.UpdateKeyboardLabels();

            // Save transpose setting
            SettingsManager.SaveTranspose(_inputManager.CurrentTranspose);
        }

        private void UpdateTransposeDisplay()
        {
            int transpose = _inputManager.CurrentTranspose;
            TransposeText.Text = transpose >= 0 ? $"+{transpose}" : transpose.ToString();
            TransposeText.Foreground = transpose == 0 
                ? new SolidColorBrush(Color.FromRgb(76, 175, 80))   // Green for 0
                : new SolidColorBrush(Color.FromRgb(255, 193, 7));  // Yellow for non-zero
        }

        private void OnSustainChanged()
        {
            UpdateSustainDisplay();
        }

        private void UpdateSustainDisplay()
        {
            if (_inputManager.IsSustainActive)
            {
                SustainIndicator.Background = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                SustainText.Text = "SUSTAIN ON";
                SustainText.Foreground = Brushes.White;
            }
            else
            {
                SustainIndicator.Background = new SolidColorBrush(Color.FromRgb(85, 85, 85));
                SustainText.Text = "SUSTAIN OFF";
                SustainText.Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170));
            }
        }

        private void OnNoteStateChanged()
        {
            // Update piano visualization on UI thread
            Dispatcher.InvokeAsync(() =>
            {
                PianoView.UpdateKeyStates();
            });
        }
    }
}
