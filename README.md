# Moonlight Latency Helper

Minimal Windows host helper for measuring Moonlight client input-to-present latency.

The helper is designed to work with the latency probe in `mspeedo/moonlight-qt` (`vrr` branch):

1. Moonlight timestamps a physical controller **A** press on the client.
2. Sunshine forwards that input to the Windows host virtual XInput controller.
3. This helper detects the host-side **A** rising edge and toggles the center marker BLACK/WHITE.
4. The helper renders the marker change on the next fixed-cadence frame.
5. Moonlight samples the decoded stream's center pixel and associates the detected transition with its present submission timestamp.

The result is a software **client input receipt -> client present submission** measurement. It does not include the physical controller's pre-Moonlight polling delay or the client panel's scanout/pixel response.

## Design

- Native Win32 + D3D11 + DXGI.
- Borderless fullscreen on the primary monitor.
- `Present(0, DXGI_PRESENT_ALLOW_TEARING)` when supported.
- Dedicated high-priority XInput polling thread at 1 kHz.
- Fixed render cadence driven by QPC; default **120 FPS**.
- Background is always animated DARK RGB noise (roughly `0.04 .. 0.30`).
- A static square at the exact display center toggles between pure BLACK (`0.0`) and pure WHITE (`1.0`).
- Moonlight samples one decoded pixel at stream center, so unrelated background noise cannot trigger the measurement.

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
--marker-size <pixels>     Center square size, default 32
--help                     Show help
```

Example:

```powershell
moonlight-latency-helper.exe --fps 120 --controller-index 0 --noise 100
```

Press **Esc** to exit.

The center square starts BLACK. Each rising edge of XInput **A** toggles BLACK <-> WHITE. The animated dark-noise background never changes brightness state.

## Recommended test sequence

1. Start Sunshine and stream the host with the matching Moonlight `vrr` build.
2. Run this helper on the Sunshine host.
3. Enable Moonlight's performance OSD and wait for `Input to present latency: ready (press A)`.
4. Press physical **A** on the Moonlight client.
5. The helper toggles the center square; Moonlight detects the center-pixel transition and reports the latency result.

Keep Moonlight's normal face-button mapping enabled so physical client A arrives at the host as XInput A.
