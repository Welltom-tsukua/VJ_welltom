# BeatLinkUnityOverlay

Unity-based VJ/video overlay prototype for Pro DJ Link environments.

This public repository contains only the source code and Unity project files
needed to open and build the project. Proprietary, paid, and third-party asset
packs are intentionally not included.

## Included

- Unity runtime/editor scripts
- Layer compositing shaders
- Main Unity scene
- Unity package manifest and project settings

## Excluded assets

The working project used additional local assets such as video plugins, skybox
materials, city models, and recovery/build/cache folders. They are excluded from
this public repository.

The 3D object / lighting scene can still run with fallback generated geometry,
but optional city models and skybox materials must be supplied separately under:

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
