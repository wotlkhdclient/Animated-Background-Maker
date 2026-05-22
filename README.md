# Animated Background Maker

![](https://i.imgur.com/3BTstxu.png)

### What is this

**ABGM** is an application that converts a video file into a set of **BLP2 (DXT1/DXT5)** textures compatible with **World of Warcraft: Wrath of the Lich King**. The resulting files are used as a frame-by-frame animated background in the in-game login screen.

---

### System Requirements

| Component | Requirement |
|-----------|-------------|
| OS | Windows 7 / 10 / 11 (32 or 64 bit) |
| .NET | .NET Framework 4.7.2 or later |
| ffmpeg | `ffmpeg.exe` and optionally `ffprobe.exe` placed next to `ABGM.exe` |

#### Where to get ffmpeg

Download the **full build with dependencies** (important — not a lite/minimal build):

https://github.com/BtbN/FFmpeg-Builds/releases

Look for a file like `ffmpeg-master-latest-win64-gpl.zip`. Extract the contents of the `bin\` folder next to `ABGM.exe`.

---

### Installation

1. Download the latest ABGM release.
2. Extract the archive to any folder.
3. Place `ffmpeg.exe` (and `ffprobe.exe`) in the same folder.
4. Run `ABGM.exe`.

The status bar at the bottom of the window will display the detected ffmpeg version. Red text means ffmpeg was not found or is not working.

---

### Usage

#### Select a video

- Click **Browse...** in the «Input Video» section and pick a video file.
- Supported formats: `mp4`, `avi`, `mkv`, `mov`, `wmv`, `flv`, `webm`, `m4v`.
- You can also **drag and drop** a video file directly onto the window.
- If `ffprobe.exe` is present next to `ABGM.exe`, the app will automatically show the video's resolution, frame rate, and duration below the file path.

#### Output folder

- Click **Browse...** in the «Output Folder for BLP Files» section and choose where to save the results.
- If no output folder is specified, it defaults to the same folder as the source video.

#### Options

| Option | Description |
|--------|-------------|
| **Generate mipmaps** | Generate a full mipmap chain for each texture (recommended) |
| **All frames** | Extract every frame of the video without any FPS limit |
| **Frames/sec** | Frames per second to extract (active when «All frames» is unchecked) |
| **Format** | `DXT1` (smaller files); `DXT5`  |
| **Texture size** | Output BLP file dimensions: `1024×1024` or `2048×1024` |

#### Convert

1. Click **Convert**.
2. The progress bar and status label show the current stage:
   - **Step 1/2** — ffmpeg extracts frames to a temporary folder.
   - **Step 2/2** — frames are converted to BLP2 and saved to the output folder.
3. When done, the log shows a summary: how many files were converted successfully and how many errors occurred.
4. A **SceneList entry** appears below the progress bar (see below).

The **Cancel** button stops the conversion at any point.

---

### Adding to the SceneList

After a successful conversion, a string like this appears in the interface:

```
MyAnimation|42
```

Where `MyAnimation` is the name of the folder containing the BLP files, and `42` is the frame count.

#### How to add the animation to the game

1. Copy your folder with BLP files into `Interface\AnimBackgrounds\`
2. Open `Interface\loginui.lua`.
3. Find the `SceneList` section:

```lua
["SceneList"] = {
    -- basic scenes
    "cl", -- classic
    "ww", -- war within
    -- ...
}
```

3. Add your entry to the list:

```lua
["SceneList"] = {
    "cl", -- classic
    "ww", -- war within
    -- ...
    "MyAnimation|42", -- your animation
}
```
