# Moonlight Latency Helper

Minimal Windows host helper for measuring Moonlight client input-to-present latency
and visually spotting streaming hitches.

The helper is designed to work with the latency probe in `mspeedo/moonlight-qt` (`vrr` branch):

1. Moonlight timestamps a physical controller **A** press on the client.
2. Sunshine forwards that input to the Windows host virtual XInput controller.
3. This helper detects the host-side **A** rising edge and toggles the center marker between black and white.
4. The marker change is committed on the next regular render deadline; the helper reports that cadence wait so Moonlight can subtract it from the benchmark result.
5. Moonlight samples the decoded stream's center pixel and associates the detected transition with its present submission timestamp.

The result is a software **client input receipt -> client present submission** measurement. It does not include the physical controller's pre-Moonlight polling delay or the client panel's scanout/pixel response.

## Design

- Native Win32 + D3D11 + DXGI.
- Borderless fullscreen on the primary monitor.
- `Present(0, DXGI_PRESENT_ALLOW_TEARING)` when supported.
- Dedicated high-priority XInput polling thread.
- Fixed render cadence driven by QPC; default **117 FPS**.
- Coloured technical tiles and clear upright/diamond/ring landmarks move right at
  half a screen-width per second, making pauses and jumps easy to judge.
- The background contains no random or stochastic grain. Instead, several clean
  high-frequency line families and smooth micro-lattices move in different
  directions through a low-amplitude sinusoidal spatial warp.
- The independent structured motions deliberately create prediction residuals so
  the encoder/decoder sees high-complexity video without a visibly noisy picture.
- Motion follows elapsed QPC time and all animated phases loop seamlessly over
  their defined periods, avoiding scene-wide jumps during normal testing.
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

GitHub Actions builds a Release x64 executable on pushes to `main`,
`sunshine-benchmark-integration`, or `structured-bandwidth-pattern`, plus pull
requests and manual workflow dispatches.

## Usage

```powershell
moonlight-latency-helper.exe [options]
```

Options:

```text
--fps <value>              Render cadence, default 117
--controller-index <0-3>   XInput controller index, default 0
--marker-size <pixels>     Center marker square size, default 32
--help                     Show help
```

Example:

```powershell
moonlight-latency-helper.exe --fps 117 --controller-index 0
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

No Sunshine changes are required for the current integration, which launches the
helper with only the FPS argument. The executable name, input controls, centre
marker, shared cadence-wait mapping, and `SUNSHINE_LATENCY_STOP_EVENT` integration
are unchanged. This test branch intentionally removes the old `--noise` option.

Track an upright or diamond as it moves across the screen. A repeated or late
frame appears as a pause followed by a jump. At 1920 pixels wide and 120 FPS,
the main layer advances about 8 pixels per normal frame. The animation exposes
end-to-end cadence problems; it cannot identify which streaming stage caused them.

The structured weave is designed to keep compression demand high without visible
random grain, but actual bitrate depends on resolution, FPS, codec, and encoder
rate control. Compare measured stream bitrate and decoder behavior against `main`
to determine whether the pattern reaches the same target load. The helper does not
control the encoder or force network traffic to a particular rate.

## Recommended test sequence

1. Start Sunshine and stream the host with the matching Moonlight `vrr` build.
2. Run this helper on the Sunshine host.
3. Enable Moonlight's performance OSD and wait for `Input to present latency: ready (press A)`.
4. Press physical **A** on the Moonlight client.
5. The helper toggles the center marker on the host; Moonlight detects the center-pixel transition and reports the latency result.

For the cleanest interpretation, keep Moonlight's normal face-button mapping enabled so physical client A arrives at the host as XInput A.
