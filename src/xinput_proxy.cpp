#include <windows.h>
#include <Xinput.h>

#include <atomic>

namespace {

using XInputGetStateFn = DWORD (WINAPI*)(DWORD, XINPUT_STATE*) noexcept;

XInputGetStateFn ResolveRealXInputGetState() noexcept
{
    // Load the Windows XInput implementation explicitly. The executable does
    // not link xinput.lib so calls from main.cpp resolve to the tiny wrapper
    // below, which can observe B without changing the helper's input loop.
    HMODULE module = LoadLibraryW(L"xinput1_4.dll");
    if (module == nullptr) {
        return nullptr;
    }

    return reinterpret_cast<XInputGetStateFn>(
        GetProcAddress(module, "XInputGetState"));
}

} // namespace

extern "C" DWORD WINAPI XInputGetState(DWORD userIndex, XINPUT_STATE* state) noexcept
{
    static const XInputGetStateFn realXInputGetState = ResolveRealXInputGetState();
    static std::atomic<bool> previousB[XUSER_MAX_COUNT] {};

    if (realXInputGetState == nullptr) {
        return ERROR_DEVICE_NOT_CONNECTED;
    }

    const DWORD result = realXInputGetState(userIndex, state);
    if (userIndex >= XUSER_MAX_COUNT) {
        return result;
    }

    if (result != ERROR_SUCCESS || state == nullptr) {
        previousB[userIndex].store(false, std::memory_order_relaxed);
        return result;
    }

    const bool currentB = (state->Gamepad.wButtons & XINPUT_GAMEPAD_B) != 0;
    const bool wasB = previousB[userIndex].exchange(currentB, std::memory_order_relaxed);

    if (currentB && !wasB) {
        if (HWND hwnd = FindWindowW(L"MoonlightLatencyHelperWindow", nullptr)) {
            PostMessageW(hwnd, WM_CLOSE, 0, 0);
        }
    }

    return result;
}
