# Windows audio pipeline

## Control plane

Windows Core Audio exposes endpoint and session APIs suitable for the first Unshrill milestones. `IAudioSessionManager2` provides session enumeration and new-session notification, while session controls expose events, identity, mute, and volume.

Two implementation details are easy to miss:

1. Session notification callbacks require a multithreaded COM apartment rather than the UI thread.
2. Microsoft documents an initialization sequence in which the session enumerator's `GetCount` is called before relying on new-session notifications.

These requirements belong inside `Unshrill.WindowsAudio`, not in view models.

## Data plane

Volume/mute control and PCM filtering are different layers.

- Session APIs can reliably control volume and mute; they do not hand Unshrill each application's PCM stream.
- Process-loopback capture can include or exclude a process tree, but capture is observational and does not replace the application's original playback.
- An Audio Processing Object can modify audio in the Windows processing graph, usually at an endpoint effect position.
- A virtual endpoint can provide a fully controlled render path, at the cost of driver deployment and routing complexity.

Consequently, version 1 separates a reliable controller from optional DSP deployment.

## Primary references

- [Core Audio APIs](https://learn.microsoft.com/windows/win32/api/_coreaudio/)
- [`IAudioSessionManager2::RegisterSessionNotification`](https://learn.microsoft.com/windows/win32/api/audiopolicy/nf-audiopolicy-iaudiosessionmanager2-registersessionnotification)
- [Audio session events](https://learn.microsoft.com/windows/win32/coreaudio/audio-session-events)
- [Application loopback audio sample](https://learn.microsoft.com/samples/microsoft/windows-classic-samples/applicationloopbackaudio-sample/)
- [Audio Processing Object architecture](https://learn.microsoft.com/windows-hardware/drivers/audio/audio-processing-object-architecture)
- [SysVAD virtual audio device sample](https://learn.microsoft.com/samples/microsoft/windows-driver-samples/sysvad-virtual-audio-device-driver-sample/)

