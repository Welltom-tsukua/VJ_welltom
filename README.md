# BeatLinkUnityOverlay

Unity-based VJ/video overlay prototype for Pro DJ Link environments.

Japanese setup and operation guide: [README_JA.md](README_JA.md)

This repository contains the Unity project files needed to open and build the
app.

## Optional Assets

To use custom 3D models or skybox materials, place them under:

- `Assets/Resources/Models`
- `Assets/Resources/Skyboxes`

## Dependencies

Unity resolves package dependencies from `Packages/manifest.json`, including:

- KlakHap
- Unity LightBeamPerformance
- Unity Collections
- Unity Timeline

Open the project with the Unity version in `ProjectSettings/ProjectVersion.txt`
or newer.

## Runtime Notes

The app can use local video files, Hap/MOV via KlakHap, YouTube URL resolving,
and Pro DJ Link / Beat Link Trigger metadata modes depending on the local
environment. External tools such as `yt-dlp`, `ffmpeg`/`ffprobe`, and Beat Link
Trigger are not bundled.

Pro DJ Link integration has only been tested with CDJ-3000 players.
