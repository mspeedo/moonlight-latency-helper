#include <windows.h>
#include <d3d11.h>
#include <d3dcompiler.h>
#include <dxgi1_5.h>
#include <Xinput.h>
#include <wrl/client.h>

#include <atomic>
#include <cstdint>
#include <cstdlib>
#include <cstring>
#include <iostream>
#include <string>
#include <thread>

using Microsoft::WRL::ComPtr;

#ifndef CREATE_WAITABLE_TIMER_HIGH_RESOLUTION
#define CREATE_WAITABLE_TIMER_HIGH_RESOLUTION 0x00000002
#endif

namespace {

struct Options {
    double fps = 120.0;
    DWORD controllerIndex = 0;
    float noise = 1.0f;
    float markerSize = 32.0f;
};

struct ShaderParams {
    uint32_t frameIndex;
    uint32_t markerWhite;
    float noiseStrength;
    float markerHalfSize;
    float width;
    float height;
    float padding0;
    float padding1;
};
static_assert(sizeof(ShaderParams) == 32, "ShaderParams/HLSL constant-buffer mismatch");

std::atomic<bool> g_Running { true };
std::atomic<uint32_t> g_MarkerWhite { 0 }; // 0 = black, 1 = white

const char* kShaderSource = R"HLSL(
cbuffer Params : register(b0)
{
    uint frameIndex;
    uint markerWhite;
    float noiseStrength;
    float markerHalfSize;
    float width;
    float height;
    float padding0;
    float padding1;
};

struct VSOut
{
    float4 position : SV_Position;
};

VSOut VSMain(uint vertexId : SV_VertexID)
{
    float2 p = float2((vertexId << 1) & 2, vertexId & 2);
    VSOut output;
    output.position = float4(p * float2(2.0, -2.0) + float2(-1.0, 1.0), 0.0, 1.0);
    return output;
}

uint Hash(uint x)
{
    x ^= x >> 16;
    x *= 0x7feb352dU;
    x ^= x >> 15;
    x *= 0x846ca68bU;
    x ^= x >> 16;
    return x;
}

float Random01(uint seed)
{
    return float(Hash(seed) & 0x00ffffffU) / 16777215.0;
}

float4 PSMain(VSOut input) : SV_Target
{
    const float2 center = float2(width, height) * 0.5;
    const float2 distanceFromCenter = abs(input.position.xy - center);

    // Moonlight samples the decoded stream's exact center pixel. Keep a static
    // square around that point so scaling/chroma filtering cannot mix the
    // animated background into the detector pixel.
    if (distanceFromCenter.x < markerHalfSize && distanceFromCenter.y < markerHalfSize) {
        const float marker = markerWhite != 0 ? 1.0 : 0.0;
        return float4(marker, marker, marker, 1.0);
    }

    // Background remains DARK in both states. Only the center marker changes.
    const uint2 pixel = uint2(input.position.xy);
    const uint seed = pixel.x * 73856093U ^ pixel.y * 19349663U ^ frameIndex * 83492791U;
    const float3 randomValue = float3(
        Random01(seed),
        Random01(seed ^ 0x9e3779b9U),
        Random01(seed ^ 0x85ebca6bU));

    const float low = 0.04;
    const float high = 0.30;
    const float midpoint = (low + high) * 0.5;
    const float amplitude = (high - low) * 0.5 * noiseStrength;
    const float3 rgb = midpoint + (randomValue - 0.5) * (2.0 * amplitude);
    return float4(rgb, 1.0);
}
)HLSL";

void PrintUsage()
{
    std::wcout
        << L"Moonlight Latency Helper\n\n"
        << L"Options:\n"
        << L"  --fps <value>              Render cadence, default 120\n"
        << L"  --controller-index <0-3>   XInput controller index, default 0\n"
        << L"  --noise <0-100>            Dark-background noise amplitude, default 100\n"
        << L"  --marker-size <pixels>     Center square size, default 32\n"
        << L"  --help                     Show this help\n\n"
        << L"Press Esc to exit. XInput A toggles center square BLACK <-> WHITE.\n";
}

bool ParseOptions(int argc, wchar_t** argv, Options& options)
{
    for (int i = 1; i < argc; ++i) {
        const std::wstring arg = argv[i];
        auto requireValue = [&](const wchar_t* name) -> const wchar_t* {
            if (i + 1 >= argc) {
                std::wcerr << L"Missing value for " << name << L"\n";
                return nullptr;
            }
            return argv[++i];
        };

        if (arg == L"--help" || arg == L"-h") {
            PrintUsage();
            return false;
        }
        if (arg == L"--fps") {
            const wchar_t* value = requireValue(L"--fps");
            if (!value) return false;
            options.fps = std::wcstod(value, nullptr);
            if (!(options.fps >= 1.0 && options.fps <= 1000.0)) {
                std::wcerr << L"--fps must be between 1 and 1000\n";
                return false;
            }
            continue;
        }
        if (arg == L"--controller-index") {
            const wchar_t* value = requireValue(L"--controller-index");
            if (!value) return false;
            const unsigned long parsed = std::wcstoul(value, nullptr, 10);
            if (parsed > 3) {
                std::wcerr << L"--controller-index must be 0..3\n";
                return false;
            }
            options.controllerIndex = static_cast<DWORD>(parsed);
            continue;
        }
        if (arg == L"--noise") {
            const wchar_t* value = requireValue(L"--noise");
            if (!value) return false;
            const double parsed = std::wcstod(value, nullptr);
            if (!(parsed >= 0.0 && parsed <= 100.0)) {
                std::wcerr << L"--noise must be between 0 and 100\n";
                return false;
            }
            options.noise = static_cast<float>(parsed / 100.0);
            continue;
        }
        if (arg == L"--marker-size") {
            const wchar_t* value = requireValue(L"--marker-size");
            if (!value) return false;
            const double parsed = std::wcstod(value, nullptr);
            if (!(parsed >= 2.0 && parsed <= 512.0)) {
                std::wcerr << L"--marker-size must be between 2 and 512\n";
                return false;
            }
            options.markerSize = static_cast<float>(parsed);
            continue;
        }

        std::wcerr << L"Unknown option: " << arg << L"\n";
        return false;
    }

    return true;
}

LRESULT CALLBACK WindowProc(HWND hwnd, UINT message, WPARAM wParam, LPARAM lParam)
{
    switch (message) {
    case WM_KEYDOWN:
        if (wParam == VK_ESCAPE) {
            DestroyWindow(hwnd);
            return 0;
        }
        break;
    case WM_SETCURSOR:
        SetCursor(nullptr);
        return TRUE;
    case WM_DESTROY:
        g_Running.store(false, std::memory_order_release);
        PostQuitMessage(0);
        return 0;
    default:
        break;
    }

    return DefWindowProcW(hwnd, message, wParam, lParam);
}

bool CompileShader(const char* entryPoint, const char* target, ComPtr<ID3DBlob>& bytecode)
{
    ComPtr<ID3DBlob> errors;
    const UINT flags = D3DCOMPILE_ENABLE_STRICTNESS | D3DCOMPILE_OPTIMIZATION_LEVEL3;
    const HRESULT hr = D3DCompile(
        kShaderSource,
        std::strlen(kShaderSource),
        "latency-helper.hlsl",
        nullptr,
        nullptr,
        entryPoint,
        target,
        flags,
        0,
        &bytecode,
        &errors);

    if (FAILED(hr)) {
        if (errors) {
            std::cerr.write(
                static_cast<const char*>(errors->GetBufferPointer()),
                static_cast<std::streamsize>(errors->GetBufferSize()));
        }
        std::cerr << "D3DCompile failed: 0x" << std::hex << static_cast<unsigned long>(hr) << "\n";
        return false;
    }
    return true;
}

class DeadlinePacer {
public:
    DeadlinePacer(double frequencyHz, double spinSeconds)
    {
        LARGE_INTEGER qpcFrequency {};
        QueryPerformanceFrequency(&qpcFrequency);
        m_QpcFrequency = qpcFrequency.QuadPart;
        m_PeriodTicks = static_cast<double>(m_QpcFrequency) / frequencyHz;
        m_SpinTicks = static_cast<int64_t>(static_cast<double>(m_QpcFrequency) * spinSeconds);

        m_Timer = CreateWaitableTimerExW(
            nullptr, nullptr, CREATE_WAITABLE_TIMER_HIGH_RESOLUTION, TIMER_ALL_ACCESS);
        if (!m_Timer) {
            m_Timer = CreateWaitableTimerW(nullptr, FALSE, nullptr);
        }

        LARGE_INTEGER now {};
        QueryPerformanceCounter(&now);
        m_NextDeadline = static_cast<double>(now.QuadPart);
    }

    ~DeadlinePacer()
    {
        if (m_Timer) {
            CloseHandle(m_Timer);
        }
    }

    void WaitNext()
    {
        const int64_t deadline = static_cast<int64_t>(m_NextDeadline);
        WaitUntil(deadline);

        LARGE_INTEGER now {};
        QueryPerformanceCounter(&now);
        m_NextDeadline += m_PeriodTicks;

        if (static_cast<double>(now.QuadPart) - m_NextDeadline > m_PeriodTicks * 2.0) {
            m_NextDeadline = static_cast<double>(now.QuadPart) + m_PeriodTicks;
        }
    }

private:
    void WaitUntil(int64_t deadline)
    {
        for (;;) {
            LARGE_INTEGER now {};
            QueryPerformanceCounter(&now);
            const int64_t remaining = deadline - now.QuadPart;
            if (remaining <= 0) {
                return;
            }

            if (m_Timer && remaining > m_SpinTicks) {
                const int64_t sleepTicks = remaining - m_SpinTicks;
                LARGE_INTEGER due {};
                due.QuadPart = -static_cast<LONGLONG>(
                    (static_cast<long double>(sleepTicks) * 10000000.0L) /
                    static_cast<long double>(m_QpcFrequency));
                if (due.QuadPart == 0) {
                    due.QuadPart = -1;
                }

                if (SetWaitableTimer(m_Timer, &due, 0, nullptr, nullptr, FALSE)) {
                    WaitForSingleObject(m_Timer, INFINITE);
                    continue;
                }
            }

            YieldProcessor();
        }
    }

    HANDLE m_Timer = nullptr;
    int64_t m_QpcFrequency = 0;
    int64_t m_SpinTicks = 0;
    double m_PeriodTicks = 0.0;
    double m_NextDeadline = 0.0;
};

void InputThread(DWORD controllerIndex)
{
    SetThreadPriority(GetCurrentThread(), THREAD_PRIORITY_HIGHEST);

    // 1 kHz host-side polling keeps helper-added detection delay below ~1 ms
    // without dedicating a full logical core to an unlimited busy-poll loop.
    DeadlinePacer pacer(1000.0, 0.00005);
    bool previousA = false;

    while (g_Running.load(std::memory_order_acquire)) {
        pacer.WaitNext();

        XINPUT_STATE state {};
        const DWORD result = XInputGetState(controllerIndex, &state);
        if (result == ERROR_SUCCESS) {
            const bool currentA = (state.Gamepad.wButtons & XINPUT_GAMEPAD_A) != 0;
            if (currentA && !previousA) {
                g_MarkerWhite.fetch_xor(1U, std::memory_order_acq_rel);
            }
            previousA = currentA;
        }
        else {
            previousA = false;
        }
    }
}

bool PumpMessages()
{
    MSG message {};
    while (PeekMessageW(&message, nullptr, 0, 0, PM_REMOVE)) {
        if (message.message == WM_QUIT) {
            return false;
        }
        TranslateMessage(&message);
        DispatchMessageW(&message);
    }
    return true;
}

} // namespace

int wmain(int argc, wchar_t** argv)
{
    Options options;
    if (!ParseOptions(argc, argv, options)) {
        if (argc > 1 && (std::wstring(argv[1]) == L"--help" || std::wstring(argv[1]) == L"-h")) {
            return 0;
        }
        PrintUsage();
        return 1;
    }

    SetPriorityClass(GetCurrentProcess(), HIGH_PRIORITY_CLASS);
    SetThreadPriority(GetCurrentThread(), THREAD_PRIORITY_HIGHEST);
    SetProcessDPIAware();
    SetThreadExecutionState(ES_CONTINUOUS | ES_DISPLAY_REQUIRED | ES_SYSTEM_REQUIRED);

    const POINT origin { 0, 0 };
    const HMONITOR monitor = MonitorFromPoint(origin, MONITOR_DEFAULTTOPRIMARY);
    MONITORINFO monitorInfo {};
    monitorInfo.cbSize = sizeof(monitorInfo);
    if (!GetMonitorInfoW(monitor, &monitorInfo)) {
        std::cerr << "GetMonitorInfo failed\n";
        return 1;
    }

    const int width = monitorInfo.rcMonitor.right - monitorInfo.rcMonitor.left;
    const int height = monitorInfo.rcMonitor.bottom - monitorInfo.rcMonitor.top;

    const HINSTANCE instance = GetModuleHandleW(nullptr);
    WNDCLASSW windowClass {};
    windowClass.lpfnWndProc = WindowProc;
    windowClass.hInstance = instance;
    windowClass.lpszClassName = L"MoonlightLatencyHelperWindow";

    if (!RegisterClassW(&windowClass) && GetLastError() != ERROR_CLASS_ALREADY_EXISTS) {
        std::cerr << "RegisterClass failed\n";
        return 1;
    }

    const HWND hwnd = CreateWindowExW(
        WS_EX_TOPMOST,
        windowClass.lpszClassName,
        L"Moonlight Latency Helper",
        WS_POPUP,
        monitorInfo.rcMonitor.left,
        monitorInfo.rcMonitor.top,
        width,
        height,
        nullptr,
        nullptr,
        instance,
        nullptr);

    if (!hwnd) {
        std::cerr << "CreateWindowEx failed\n";
        return 1;
    }

    ShowWindow(hwnd, SW_SHOW);
    SetForegroundWindow(hwnd);

    UINT deviceFlags = D3D11_CREATE_DEVICE_BGRA_SUPPORT;
    const D3D_FEATURE_LEVEL featureLevels[] = {
        D3D_FEATURE_LEVEL_11_1,
        D3D_FEATURE_LEVEL_11_0,
    };

    ComPtr<ID3D11Device> device;
    ComPtr<ID3D11DeviceContext> context;
    D3D_FEATURE_LEVEL selectedFeatureLevel {};
    HRESULT hr = D3D11CreateDevice(
        nullptr,
        D3D_DRIVER_TYPE_HARDWARE,
        nullptr,
        deviceFlags,
        featureLevels,
        static_cast<UINT>(std::size(featureLevels)),
        D3D11_SDK_VERSION,
        &device,
        &selectedFeatureLevel,
        &context);

    if (hr == E_INVALIDARG) {
        hr = D3D11CreateDevice(
            nullptr,
            D3D_DRIVER_TYPE_HARDWARE,
            nullptr,
            deviceFlags,
            &featureLevels[1],
            1,
            D3D11_SDK_VERSION,
            &device,
            &selectedFeatureLevel,
            &context);
    }
    if (FAILED(hr)) {
        std::cerr << "D3D11CreateDevice failed: 0x" << std::hex << static_cast<unsigned long>(hr) << "\n";
        return 1;
    }

    ComPtr<IDXGIDevice> dxgiDevice;
    ComPtr<IDXGIAdapter> adapter;
    ComPtr<IDXGIFactory2> factory;
    if (FAILED(device.As(&dxgiDevice)) ||
        FAILED(dxgiDevice->GetAdapter(&adapter)) ||
        FAILED(adapter->GetParent(IID_PPV_ARGS(&factory)))) {
        std::cerr << "Unable to obtain DXGI factory\n";
        return 1;
    }

    bool allowTearing = false;
    ComPtr<IDXGIFactory5> factory5;
    if (SUCCEEDED(factory.As(&factory5))) {
        BOOL supported = FALSE;
        if (SUCCEEDED(factory5->CheckFeatureSupport(
                DXGI_FEATURE_PRESENT_ALLOW_TEARING, &supported, sizeof(supported)))) {
            allowTearing = supported == TRUE;
        }
    }

    DXGI_SWAP_CHAIN_DESC1 swapDesc {};
    swapDesc.Width = static_cast<UINT>(width);
    swapDesc.Height = static_cast<UINT>(height);
    swapDesc.Format = DXGI_FORMAT_R8G8B8A8_UNORM;
    swapDesc.SampleDesc.Count = 1;
    swapDesc.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
    swapDesc.BufferCount = 2;
    swapDesc.SwapEffect = DXGI_SWAP_EFFECT_FLIP_DISCARD;
    swapDesc.Scaling = DXGI_SCALING_STRETCH;
    swapDesc.AlphaMode = DXGI_ALPHA_MODE_IGNORE;
    swapDesc.Flags = allowTearing ? DXGI_SWAP_CHAIN_FLAG_ALLOW_TEARING : 0;

    ComPtr<IDXGISwapChain1> swapChain;
    hr = factory->CreateSwapChainForHwnd(device.Get(), hwnd, &swapDesc, nullptr, nullptr, &swapChain);
    if (FAILED(hr)) {
        std::cerr << "CreateSwapChainForHwnd failed: 0x" << std::hex << static_cast<unsigned long>(hr) << "\n";
        return 1;
    }
    factory->MakeWindowAssociation(hwnd, DXGI_MWA_NO_ALT_ENTER);

    ComPtr<ID3D11Texture2D> backBuffer;
    ComPtr<ID3D11RenderTargetView> renderTarget;
    if (FAILED(swapChain->GetBuffer(0, IID_PPV_ARGS(&backBuffer))) ||
        FAILED(device->CreateRenderTargetView(backBuffer.Get(), nullptr, &renderTarget))) {
        std::cerr << "Unable to create render target\n";
        return 1;
    }

    ComPtr<ID3DBlob> vertexBytecode;
    ComPtr<ID3DBlob> pixelBytecode;
    if (!CompileShader("VSMain", "vs_5_0", vertexBytecode) ||
        !CompileShader("PSMain", "ps_5_0", pixelBytecode)) {
        return 1;
    }

    ComPtr<ID3D11VertexShader> vertexShader;
    ComPtr<ID3D11PixelShader> pixelShader;
    if (FAILED(device->CreateVertexShader(
            vertexBytecode->GetBufferPointer(), vertexBytecode->GetBufferSize(), nullptr, &vertexShader)) ||
        FAILED(device->CreatePixelShader(
            pixelBytecode->GetBufferPointer(), pixelBytecode->GetBufferSize(), nullptr, &pixelShader))) {
        std::cerr << "Unable to create shaders\n";
        return 1;
    }

    D3D11_BUFFER_DESC constantDesc {};
    constantDesc.ByteWidth = sizeof(ShaderParams);
    constantDesc.Usage = D3D11_USAGE_DEFAULT;
    constantDesc.BindFlags = D3D11_BIND_CONSTANT_BUFFER;

    ComPtr<ID3D11Buffer> constantBuffer;
    if (FAILED(device->CreateBuffer(&constantDesc, nullptr, &constantBuffer))) {
        std::cerr << "Unable to create constant buffer\n";
        return 1;
    }

    const D3D11_VIEWPORT viewport {
        0.0f,
        0.0f,
        static_cast<float>(width),
        static_cast<float>(height),
        0.0f,
        1.0f,
    };

    context->OMSetRenderTargets(1, renderTarget.GetAddressOf(), nullptr);
    context->RSSetViewports(1, &viewport);
    context->IASetInputLayout(nullptr);
    context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
    context->VSSetShader(vertexShader.Get(), nullptr, 0);
    context->PSSetShader(pixelShader.Get(), nullptr, 0);
    context->PSSetConstantBuffers(0, 1, constantBuffer.GetAddressOf());

    std::wcout
        << L"Moonlight Latency Helper\n"
        << L"Display: " << width << L"x" << height << L"\n"
        << L"Render cadence: " << options.fps << L" FPS\n"
        << L"XInput controller: " << options.controllerIndex << L"\n"
        << L"Center marker: " << options.markerSize << L" px, starts BLACK\n"
        << L"Background: dark animated noise\n"
        << L"Tearing present: " << (allowTearing ? L"yes" : L"no") << L"\n"
        << L"Press Esc to exit.\n";

    std::thread inputThread(InputThread, options.controllerIndex);
    DeadlinePacer framePacer(options.fps, 0.00030);
    uint32_t frameIndex = 0;

    while (g_Running.load(std::memory_order_acquire) && PumpMessages()) {
        framePacer.WaitNext();

        ShaderParams params {};
        params.frameIndex = frameIndex++;
        params.markerWhite = g_MarkerWhite.load(std::memory_order_acquire);
        params.noiseStrength = options.noise;
        params.markerHalfSize = options.markerSize * 0.5f;
        params.width = static_cast<float>(width);
        params.height = static_cast<float>(height);
        context->UpdateSubresource(constantBuffer.Get(), 0, nullptr, &params, 0, 0);

        context->Draw(3, 0);

        const UINT presentFlags = allowTearing ? DXGI_PRESENT_ALLOW_TEARING : 0;
        hr = swapChain->Present(0, presentFlags);
        if (FAILED(hr)) {
            std::cerr << "Present failed: 0x" << std::hex << static_cast<unsigned long>(hr) << "\n";
            g_Running.store(false, std::memory_order_release);
            break;
        }
    }

    g_Running.store(false, std::memory_order_release);
    if (inputThread.joinable()) {
        inputThread.join();
    }

    SetThreadExecutionState(ES_CONTINUOUS);
    return 0;
}
