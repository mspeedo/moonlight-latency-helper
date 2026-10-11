#!/usr/bin/env bash
set -euo pipefail
mkdir -p build/stubs build/out
mcs -target:library -sdk:2 -out:build/stubs/UnityEngine.CoreModule.dll stubs/UnityEngine.CoreModule.cs
mcs -target:library -sdk:2 -r:build/stubs/UnityEngine.CoreModule.dll -out:build/stubs/BepInEx.dll stubs/BepInEx.cs
mcs -target:library -sdk:2 -r:build/stubs/UnityEngine.CoreModule.dll -out:build/stubs/Assembly-CSharp.dll stubs/Assembly-CSharp.cs
mcs -target:library -sdk:2 -r:build/stubs/Assembly-CSharp.dll -r:build/stubs/UnityEngine.CoreModule.dll -out:build/stubs/ModTheGungeonAPI.dll stubs/ModTheGungeonAPI.cs
mcs -target:library -sdk:2 -optimize+ -r:build/stubs/BepInEx.dll -r:build/stubs/UnityEngine.CoreModule.dll -r:build/stubs/Assembly-CSharp.dll -r:build/stubs/ModTheGungeonAPI.dll -out:build/out/BonusEnemyHearts.dll BonusEnemyHearts.cs
file build/out/BonusEnemyHearts.dll
