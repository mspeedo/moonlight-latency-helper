cmake_minimum_required(VERSION 3.20)
project(moonlight_latency_helper LANGUAGES CXX)

add_executable(moonlight-latency-helper
    src/main.cpp
    src/xinput_proxy.cpp
)

target_compile_features(moonlight-latency-helper PRIVATE cxx_std_17)
target_compile_definitions(moonlight-latency-helper PRIVATE
    UNICODE
    _UNICODE
    WIN32_LEAN_AND_MEAN
    NOMINMAX
)
target_link_libraries(moonlight-latency-helper PRIVATE
    d3d11
    dxgi
    d3dcompiler
)

if(MSVC)
    target_compile_options(moonlight-latency-helper PRIVATE /W4 /permissive-)
endif()
