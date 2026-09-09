# Moonlight Latency Helper

Minimal Windows host helper for measuring Moonlight client input-to-present latency.

The helper is designed to work with the latency probe in `mspeedo/moonlight-qt` (`vrr` branch):

1. Moonlight timestamps a physical controller **A** press on the client.
2. Sunshine forwards that input to the Windows host virtual XInput controller.
3. This helper detects the host-side **A** rising edge and toggles the center marker between black and white.
4. The helper renders the new marker state on the next fixed-cadence frame.
5. Moonlight samples the decoded stream's center pixel and associates the detected transition with its present submission timestamp.

The result is a software **client input receipt -> client present submission** measurement. It does not include the physical controller's pre-Moonlight polling delay or the client panel's scanout/pixel response.

## Design

- Native Win32 + D3D11 + DXGI.
- Borderless fullscreen on the primary monitor.
- `Present(0, DXGI_PRESENT_ALLOW_TEARING)` when supported.
- Dedicated high-priority XInput polling thread.
- Fixed render cadence driven by QPC; default **120 FPS**.
- Dark spatial/temporal RGB noise every frame.
- Background noise range: approximately `0.04 .. 0.30`.
- A fixed marker square around the exact stream center is forced to pure black or pure white, so Moonlight's single-center-pixel detector cannot be triggered by unrelated background noise.
- XInput **A** toggles the center marker black <-> white.
- XInput **B** closes the helper.

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

GitHub Actions also builds a Release x64 executable on every push.

## Usage

```powershell
moonlight-latency-helper.exe [options]
```

Options:

```text
--fps <value>              Render cadence, default 120
--controller-index <0-3>   XInput controller index, default 0
--noise <0-100>            Dark-background noise amplitude, default 100
--marker-size <pixels>     Center marker square size, default 32
--help                     Show help
```

Example:

```powershell
moonlight-latency-helper.exe --fps 120 --controller-index 0 --noise 100
```

Press **B** on the selected XInput controller or **Esc** on the keyboard to exit.

The initial center marker is black. Each rising edge of XInput **A** toggles black <-> white.

## Recommended test sequence

1. Start Sunshine and stream the host with the matching Moonlight `vrr` build.
2. Run this helper on the Sunshine host.
3. Enable Moonlight's performance OSD and wait for `Input to present latency: ready (press A)`.
4. Press physical **A** on the Moonlight client.
5. The helper toggles the center marker on the host; Moonlight detects the center-pixel transition and reports the latency result.

For the cleanest interpretation, keep Moonlight's normal face-button mapping enabled so physical client A arrives at the host as XInput A.
