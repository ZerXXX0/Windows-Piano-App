using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using PianoApp.Models;

namespace PianoApp.Audio
{
    /// <summary>
    /// Buffer-based audio engine using NAudio with WASAPI for low-latency playback.
    /// 
    /// Design:
    /// - Preloads all WAV files from external Samples folder at startup
    /// - Uses a mixing sample provider for polyphonic playback
    /// - Each note is a cached sample that can be triggered instantly
    /// - No allocations in the audio callback path
    /// - Supports interchangeable sample packs
    /// 
    /// Sample Pack Format:
    /// - WAV files named: A0.wav, A#0.wav, B0.wav, C1.wav, C#1.wav, ... C8.wav
    /// - 88 files total (A0 to C8)
    /// - Place in Samples/ folder or Samples/{PackName}/ subfolder
    /// </summary>
    public class AudioEngine : IDisposable
    {
        /// <summary>
        /// Base folder name for sample packs (relative to exe location).
        /// </summary>
        public const string SAMPLES_FOLDER = "Samples";

        /// <summary>
        /// Currently loaded sample pack name.
        /// </summary>
        public string CurrentSamplePack { get; private set; } = "Default";

        /// <summary>
        /// Full path to the samples base directory.
        /// </summary>
        public static string SamplesBasePath => Path.Combine(AppContext.BaseDirectory, SAMPLES_FOLDER);

        /// <summary>
        /// Gets the path to a specific sample pack.
        /// </summary>
        public static string GetSamplePackPath(string packName)
        {
            if (string.IsNullOrEmpty(packName) || packName == "Default")
                return SamplesBasePath;
            
            return Path.Combine(SamplesBasePath, packName);
        }

        /// <summary>
        /// Lists all available sample packs.
        /// </summary>
        public static List<string> GetAvailableSamplePacks()
        {
            var packs = new List<string>();
            
            // Check if default pack exists (WAV files directly in Samples/)
            if (Directory.Exists(SamplesBasePath))
            {
                var defaultWavs = Directory.GetFiles(SamplesBasePath, "*.wav");
                if (defaultWavs.Length > 0)
                    packs.Add("Default");
                
                // Check for subfolders (additional sample packs)
                foreach (var dir in Directory.GetDirectories(SamplesBasePath))
                {
                    var wavs = Directory.GetFiles(dir, "*.wav");
                    if (wavs.Length > 0)
                        packs.Add(Path.GetFileName(dir));
                }
            }
            
            return packs;
        }

        private readonly IWavePlayer _wavePlayer;
        private readonly MixingSampleProvider _mixer;
        private readonly Dictionary<int, CachedSound> _cachedSounds;
        private readonly Dictionary<int, PlayingNote> _activeNotes;
        private readonly object _lock = new();
        private bool _disposed;

        // Standard audio format: 44.1kHz, stereo, 32-bit float
        private static readonly WaveFormat OutputFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);

        public AudioEngine()
        {
            _cachedSounds = new Dictionary<int, CachedSound>();
            _activeNotes = new Dictionary<int, PlayingNote>();

            // Create mixer for polyphonic playback
            _mixer = new MixingSampleProvider(OutputFormat)
            {
                ReadFully = true
            };

            // Use WASAPI for low-latency playback
            // Shared mode with 50ms latency is a good balance
            _wavePlayer = new WasapiOut(
                NAudio.CoreAudioApi.AudioClientShareMode.Shared,
                latency: 50);

            _wavePlayer.Init(_mixer);
        }

        /// <summary>
        /// Preloads all 88 piano samples into memory from the default sample pack.
        /// Returns a list of missing notes for logging.
        /// </summary>
        public List<string> PreloadSamples()
        {
            return PreloadSamples("Default");
        }

        /// <summary>
        /// Preloads all 88 piano samples into memory from the specified sample pack.
        /// Returns a list of missing notes for logging.
        /// </summary>
        /// <param name="packName">Name of the sample pack to load</param>
        public List<string> PreloadSamples(string packName)
        {
            var missingNotes = new List<string>();
            var allNotes = PianoNote.GenerateAll88Keys();
            string samplePath = GetSamplePackPath(packName);

            // Clear existing cached sounds
            foreach (var cached in _cachedSounds.Values)
            {
                cached.Dispose();
            }
            _cachedSounds.Clear();

            foreach (var note in allNotes)
            {
                string filePath = Path.Combine(samplePath, note.FileName);

                if (File.Exists(filePath))
                {
                    try
                    {
                        var cached = new CachedSound(filePath, OutputFormat);
                        _cachedSounds[note.MidiNote] = cached;
                    }
                    catch (Exception ex)
                    {
                        missingNotes.Add($"{note.FullName} (load error: {ex.Message})");
                    }
                }
                else
                {
                    missingNotes.Add(note.FullName);
                }
            }

            CurrentSamplePack = packName;
            return missingNotes;
        }

        /// <summary>
        /// Starts audio playback.
        /// </summary>
        public void Start()
        {
            _wavePlayer.Play();
        }

        /// <summary>
        /// Plays a note (KeyDown event).
        /// </summary>
        public void NoteOn(int midiNote)
        {
            if (!_cachedSounds.TryGetValue(midiNote, out var cached))
                return;

            lock (_lock)
            {
                // Stop any existing instance of this note
                if (_activeNotes.TryGetValue(midiNote, out var existing))
                {
                    existing.Provider.FadeOut();
                }

                // Create new playing note
                var provider = new CachedSoundSampleProvider(cached);
                _activeNotes[midiNote] = new PlayingNote(provider, isHeld: true);
                _mixer.AddMixerInput(provider);
            }
        }

        /// <summary>
        /// Releases a note (KeyUp event).
        /// If sustain is active, the note continues playing.
        /// </summary>
        public void NoteOff(int midiNote, bool sustainActive)
        {
            lock (_lock)
            {
                if (!_activeNotes.TryGetValue(midiNote, out var note))
                    return;

                // Mark as no longer held
                note.IsHeld = false;

                if (!sustainActive)
                {
                    // Immediately fade out if no sustain
                    note.Provider.FadeOut();
                    _activeNotes.Remove(midiNote);
                }
                // If sustain is active, note continues playing (sustained state)
            }
        }

        /// <summary>
        /// Called when sustain pedal is released.
        /// Stops all notes that are sustained but not held.
        /// Uses gentler fade for realistic sustain pedal release.
        /// </summary>
        public void ReleaseSustainedNotes()
        {
            lock (_lock)
            {
                var toRemove = new List<int>();

                foreach (var kvp in _activeNotes)
                {
                    if (!kvp.Value.IsHeld)
                    {
                        // Use gentle sustain release fade (dampers falling softly)
                        kvp.Value.Provider.FadeOut(isSustainRelease: true);
                        toRemove.Add(kvp.Key);
                    }
                }

                foreach (int midi in toRemove)
                {
                    _activeNotes.Remove(midi);
                }
            }
        }

        /// <summary>
        /// Gets all currently active MIDI notes (for UI highlighting).
        /// </summary>
        public HashSet<int> GetActiveNotes()
        {
            lock (_lock)
            {
                return new HashSet<int>(_activeNotes.Keys);
            }
        }

        /// <summary>
        /// Gets notes that are sustained but not held (for distinct UI color).
        /// </summary>
        public HashSet<int> GetSustainedNotes()
        {
            lock (_lock)
            {
                return new HashSet<int>(
                    _activeNotes.Where(kvp => !kvp.Value.IsHeld).Select(kvp => kvp.Key));
            }
        }

        /// <summary>
        /// Checks if a specific MIDI note sample is loaded.
        /// </summary>
        public bool HasSample(int midiNote) => _cachedSounds.ContainsKey(midiNote);

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _wavePlayer.Stop();
            _wavePlayer.Dispose();

            foreach (var cached in _cachedSounds.Values)
            {
                cached.Dispose();
            }
        }
    }

    /// <summary>
    /// Tracks the state of a currently playing note.
    /// </summary>
    internal class PlayingNote
    {
        public CachedSoundSampleProvider Provider { get; }
        public bool IsHeld { get; set; }

        public PlayingNote(CachedSoundSampleProvider provider, bool isHeld)
        {
            Provider = provider;
            IsHeld = isHeld;
        }
    }

    /// <summary>
    /// Stores a preloaded audio sample in memory.
    /// Audio data is kept as float samples for direct mixing.
    /// </summary>
    internal class CachedSound : IDisposable
    {
        public float[] AudioData { get; }
        public WaveFormat WaveFormat { get; }

        public CachedSound(string filePath, WaveFormat targetFormat)
        {
            using var reader = new AudioFileReader(filePath);
            
            // Resample if necessary to match target format
            ISampleProvider source = reader;
            
            if (reader.WaveFormat.SampleRate != targetFormat.SampleRate)
            {
                source = new WdlResamplingSampleProvider(reader, targetFormat.SampleRate);
            }

            // Convert to stereo if needed
            if (source.WaveFormat.Channels == 1)
            {
                source = new MonoToStereoSampleProvider(source);
            }

            WaveFormat = targetFormat;

            // Read all samples into memory
            var samples = new List<float>();
            float[] buffer = new float[targetFormat.SampleRate * targetFormat.Channels];
            int read;
            
            while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (int i = 0; i < read; i++)
                {
                    samples.Add(buffer[i]);
                }
            }

            AudioData = samples.ToArray();
        }

        public void Dispose()
        {
            // AudioData is managed, no explicit disposal needed
        }
    }

    /// <summary>
    /// Sample provider that plays from a cached sound buffer.
    /// Includes realistic fade-out capability simulating piano damper behavior.
    /// 
    /// Real piano damper physics:
    /// - When key released: damper touches string, causing exponential decay
    /// - Fast initial damping, then gradual tail-off
    /// - Sustain release: softer, more gradual decay (dampers fall gently)
    /// </summary>
    internal class CachedSoundSampleProvider : ISampleProvider
    {
        private readonly CachedSound _cached;
        private int _position;
        private bool _fadingOut;
        private float _fadeGain = 1.0f;
        
        // Fade parameters for realistic piano behavior
        // Using exponential decay: gain = gain * decayFactor per sample
        private float _decayFactor = 1.0f;
        private const float NormalReleaseDecay = 0.99985f;   // ~150ms fade (key release)
        private const float SustainReleaseDecay = 0.99995f;  // ~400ms fade (sustain pedal release)
        private const float MinGainThreshold = 0.001f;       // Stop when inaudible

        public WaveFormat WaveFormat => _cached.WaveFormat;

        public CachedSoundSampleProvider(CachedSound cached)
        {
            _cached = cached;
            _position = 0;
        }

        /// <summary>
        /// Start normal fade out (key release with damper).
        /// </summary>
        public void FadeOut()
        {
            FadeOut(isSustainRelease: false);
        }

        /// <summary>
        /// Start fade out with specified decay type.
        /// </summary>
        /// <param name="isSustainRelease">True for softer sustain pedal release</param>
        public void FadeOut(bool isSustainRelease)
        {
            if (_fadingOut) return; // Already fading
            
            _fadingOut = true;
            // Sustain release is gentler (dampers fall softly onto all strings)
            // Normal release is quicker (single damper pressed firmly)
            _decayFactor = isSustainRelease ? SustainReleaseDecay : NormalReleaseDecay;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int available = _cached.AudioData.Length - _position;
            int toCopy = Math.Min(available, count);

            if (toCopy <= 0 || _fadeGain < MinGainThreshold)
            {
                // Fill with silence and signal completion
                Array.Clear(buffer, offset, count);
                return 0;
            }

            for (int i = 0; i < toCopy; i++)
            {
                buffer[offset + i] = _cached.AudioData[_position + i] * _fadeGain;

                if (_fadingOut)
                {
                    // Exponential decay for realistic damper behavior
                    _fadeGain *= _decayFactor;
                    
                    if (_fadeGain < MinGainThreshold)
                    {
                        _fadeGain = 0;
                        // Fill rest with silence
                        int remaining = toCopy - i - 1;
                        if (remaining > 0)
                        {
                            Array.Clear(buffer, offset + i + 1, remaining);
                        }
                        _position = _cached.AudioData.Length;
                        return i + 1;
                    }
                }
            }

            _position += toCopy;

            // Fill any remaining buffer with silence
            if (toCopy < count)
            {
                Array.Clear(buffer, offset + toCopy, count - toCopy);
            }

            return toCopy;
        }
    }
}
