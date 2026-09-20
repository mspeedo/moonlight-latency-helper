# Moonlight Latency Helper

Minimal Windows host helper for measuring Moonlight client input-to-present latency
and visually spotting streaming hitches.

The helper is designed to work with the latency probe in `mspeedo/moonlight-qt` (`vrr` branch):

1. Moonlight timestamps a physical controller **A** press on the client.
2. Sunshine forwards that input to the Windows host virtual XInput controller.
3. This helper detects the host-side **A** rising edge and toggles the center marker between black and white.
4. The helper wakes rendering immediately when the marker changes, between its regular cadence deadlines if needed.
5. Moonlight samples the decoded stream's center pixel and associates the detected transition with its present submission timestamp.

The result is a software **client input receipt -> client present submission** measurement. It does not include the physical controller's pre-Moonlight polling delay or the client panel's scanout/pixel response.

## Design

- Native Win32 + D3D11 + DXGI.
- Borderless fullscreen on the primary monitor.
- `Present(0, DXGI_PRESENT_ALLOW_TEARING)` when supported.
- Dedicated high-priority XInput polling thread.
- Fixed render cadence driven by QPC; default **117 FPS**.
- Coloured textured tiles and clear vertical/diamond landmarks move right at
  half a screen-width per second. All landmarks share the same motion, making
  pauses and jumps easier to judge without a competing motion layer.
- Motion follows elapsed QPC time, including on input-triggered renders; seamless
  eight-screen repeats keep positions precise during long runs.
- Fresh fine RGB grain every frame stresses compression while the landmarks
  remain clean enough to track visually. At the default `--noise 50`, grain blocks
  are approximately 1.68 pixels wide; the grain's channel range is +/-0.12.
- The existing `--noise` control still adjusts spatial frequency from 64-pixel
  blocks at 0 to per-pixel grain at 100. Its curve is now biased toward fine detail
  (the previous default produced 32.5-pixel blocks). It does not adjust amplitude.
- A fixed marker square around the exact stream center is forced to pure black or pure white, so Moonlight's single-center-pixel detector cannot be triggered by unrelated background noise.
- XInput **A** toggles the center marker black <-> white.
- Keyboard **Space** also toggles the center marker for local PC smoke testing.
- XInput **B** closes the helper.
- Keyboard **Esc** closes the helper.

## Build

Requires Visual Studio 2022 with the Desktop development with C++ workload and Windows SDK.

```powershell
cmake -S . -B build -A x64
cmake --build build --config Release
```

Executable:

```text
build\Release\moonlight-latency-helper.exe
```

GitHub Actions builds a Release x64 executable on pushes to `main` or
`sunshine-benchmark-integration`, pull requests, or a manual workflow dispatch.

## Usage

```powershell
moonlight-latency-helper.exe [options]
```

Options:

```text
--fps <value>              Render cadence, default 117
--controller-index <0-3>   XInput controller index, default 0
--noise <0-100>            Grain spatial frequency: 100=1px, 0=64px blocks; default 50 (~1.68px)
--marker-size <pixels>     Center marker square size, default 32
--help                     Show help
```

Example:

```powershell
moonlight-latency-helper.exe --fps 117 --controller-index 0 --noise 50
```

Controls while running:

```text
Controller A   Toggle center marker black <-> white
Space          Toggle center marker black <-> white (local PC smoke test)
Controller B   Exit
Esc            Exit
```

The initial center marker is black.

## Visual hitch detection and drop-in replacement

No new options or configuration changes are required. The executable name,
option names/ranges/defaults, input controls, centre marker, and Sunshine
`SUNSHINE_LATENCY_STOP_EVENT` integration are preserved. Replace the existing
executable after building; the existing Moonlight/Sunshine launcher arguments work
unchanged.

Track an upright or diamond as it moves across the screen. A repeated or late
frame appears as a pause followed by a jump. At 1920 pixels wide and 120 FPS,
the main layer advances about 8 pixels per normal frame. The animation exposes
end-to-end cadence problems; it cannot identify which streaming stage caused them.

The default grain is designed to keep compression demand high, but actual bitrate
depends on resolution, FPS, codec, and encoder rate control. Check the stream's
measured bitrate to establish whether it reaches your configured target. The helper
does not control the encoder or force network traffic to a particular rate.

## Recommended test sequence

1. Start Sunshine and stream the host with the matching Moonlight `vrr` build.
2. Run this helper on the Sunshine host.
3. Enable Moonlight's performance OSD and wait for `Input to present latency: ready (press A)`.
4. Press physical **A** on the Moonlight client.
5. The helper toggles the center marker on the host; Moonlight detects the center-pixel transition and reports the latency result.

For the cleanest interpretation, keep Moonlight's normal face-button mapping enabled so physical client A arrives at the host as XInput A.
