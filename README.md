# PianoApp 🎹

A Windows desktop piano application playable entirely via the computer keyboard, built with C#, WPF, and NAudio.

![.NET 9.0](https://img.shields.io/badge/.NET-9.0-purple)
![Platform](https://img.shields.io/badge/Platform-Windows-blue)
![License](https://img.shields.io/badge/License-MIT-green)

## Features

- **88-Key Piano** - Full piano range from A0 to C8 (MIDI notes 21-108)
- **Low-Latency Audio** - WASAPI audio output (~50ms latency)
- **Keyboard Input** - Play notes using your computer keyboard
- **Sustain Pedal** - Toggle sustain with the Space bar (realistic exponential decay)
- **Transpose** - Shift the keyboard mapping up/down with `[` and `]` keys
- **Sharp Notes** - Hold Shift while pressing a key to play the sharp note
- **Visual Feedback** - Piano keys light up when pressed (blue = held, purple = sustained)
- **Keyboard Labels** - See which keys map to which notes (updates with transpose)
- **Interchangeable Sample Packs** - Switch between different piano sounds
- **Settings Persistence** - Selected sample pack and transpose are saved automatically

## Keyboard Layout

| Keys | Notes | Range |
|------|-------|-------|
| `1` - `0` (number row) | C2 - E3 | Low register |
| `Q` - `P` | F3 - A4 | Middle-low register |
| `A` - `L` | B4 - C6 | Middle-high register |
| `Z` - `M` | D6 - C7 | High register |

### Controls

| Key | Action |
|-----|--------|
| `Space` | Toggle sustain pedal ON/OFF |
| `Shift` | Play sharp note (hold while pressing note key) |
| `[` | Transpose down (shift all keys lower) |
| `]` | Transpose up (shift all keys higher) |

## Download & Installation

1. Download the release package from this repository
2. Extract to any folder
3. Run `PianoApp.exe`

**Contents:**
- `PianoApp.exe` - Main executable
- `Samples/` folder - Piano sample WAV files

Your selected sample pack and transpose setting are saved automatically to `PianoApp.settings.json`.

## Sample Packs

The app supports interchangeable sample packs. Each sample pack is a folder containing 88 WAV files.

### Adding a New Sample Pack

1. Create a new folder in `Samples/` (e.g., `Samples/Grand Piano/`)
2. Add WAV files named `A0.wav` through `C8.wav` (including sharps like `C#4.wav`)
3. Restart the app - the new pack will appear in the dropdown

### Required Sample Files (88 total)

```
A0.wav, A#0.wav, B0.wav,
C1.wav, C#1.wav, D1.wav, D#1.wav, E1.wav, F1.wav, F#1.wav, G1.wav, G#1.wav, A1.wav, A#1.wav, B1.wav,
... (repeat pattern for octaves 2-7)
C8.wav
```

## Requirements

- Windows 10/11 (x64)
- Audio output device

## Building from Source

### Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) or later
- Visual Studio 2022 or VS Code with C# extension

### Clone & Build

```bash
git clone https://github.com/yourusername/PianoApp.git
cd PianoApp
dotnet build
```

### Run (Development)

```bash
dotnet run
```

### Publish (Standalone Executable)

To create a standalone executable (~162MB exe + Samples folder, includes .NET runtime):

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Output: `bin/Release/net9.0-windows/win-x64/publish/`

**Published contents:**
- `PianoApp.exe` (~162MB) - Self-contained executable
- `Samples/` - 88 WAV files (must stay alongside exe)

## Project Structure

```
PianoApp/
├── Audio/
│   └── AudioEngine.cs           # WASAPI audio playback, sample loading, mixing
├── Managers/
│   ├── KeyboardInputManager.cs  # Keyboard event handling
│   ├── NoteMapper.cs            # Key-to-note mapping with transpose
│   ├── SettingsManager.cs       # Settings persistence (JSON)
│   └── SustainManager.cs        # Sustain pedal logic
├── Models/
│   └── PianoNote.cs             # Piano note data (MIDI, frequency, name)
├── UI/
│   ├── PianoVisualization.xaml      # 88-key piano UI
│   └── PianoVisualization.xaml.cs   # Key rendering and highlighting
├── Samples/                     # Default sample pack (88 WAV files)
├── MainWindow.xaml              # Main application window
├── MainWindow.xaml.cs           # Application logic
├── App.xaml                     # Application entry point
└── PianoApp.csproj              # Project configuration
```

## Technical Details

- **Audio Engine**: NAudio with WASAPI (shared mode, 50ms latency)
- **Sample Format**: 44.1kHz, stereo, 32-bit float WAV
- **Polyphony**: Unlimited simultaneous notes
- **Fade-out**: Exponential decay simulating real damper behavior
  - Normal release: ~150ms decay
  - Sustain release: ~400ms decay

## License

MIT License

## Credits

- Audio library: [NAudio](https://github.com/naudio/NAudio)
- Piano samples: [Tedagame on Freesound](https://freesound.org/people/tedagame/)
