# BeatLinkUnityOverlay

This Unity project listens to Pro DJ Link packets directly in Unity and renders
the current player state without starting Beat Link Trigger.

The same UI also opens a Windows capture device with Unity's WebCamTexture API
and shows an HDMI capture preview above the DJ Link status panel.

Beat Link Trigger can also be started as a metadata and waveform companion
server. Unity polls `http://127.0.0.1:17081/params.json` for track metadata and
loads runtime waveforms from `/wave-preview/1` and `/wave-preview/2`, while
keeping CDJ status parsing inside Unity.

Current direct packet support:

- UDP 50000 device announcements
- UDP 50001 precise playback position
- UDP 50002 CDJ status
- TCP 12523 dbserver port discovery

The first Unity-only pass displays device name/address, master, play/sync/on-air
flags, BPM, effective BPM, pitch, rekordbox ID, track number, beat, and playback
time when precise position packets are present. It also detects each player's
dbserver port so the next port can request metadata and waveforms.

Track title, artist, and real waveform images are not in the normal status
packets. They still need the Beat Link Trigger metadata and waveform query
protocols to be ported.

Build the player with Unity, then start it:

```powershell
..\start-beatlink-unity.ps1
```

Open this folder with Unity 6000.3.10f1 or newer. The runtime script creates
its own GameObject automatically, so no scene setup is required.

If Unity picks the wrong camera/capture device, set the `HDMI_CAPTURE_DEVICE`
environment variable to part of the device name, or use the in-app
`Next Capture Device` button.
