# 2DxFXShaderNullFix

Unity Mod Manager mod for **Pathfinder: Wrath of the Righteous** that prevents a crash when the shader `2DxFX/AL/DesintegrationFX` cannot be resolved while mods are loaded.

## What it does

- Patches `_2dxFX_AL_DesintegrationFX.OnEnable` with Harmony.
- In a prefix, checks `Shader.Find("2DxFX/AL/DesintegrationFX")`.
  - If the shader is missing, logs the full transform path, disables the component, and skips the original method.
- In a finalizer, suppresses `ArgumentNullException` where `ParamName == "shader"`, disables the component if possible, and logs the transform path.

## Build target

- .NET Framework 4.7.2 class library.

## Local assembly references

Update project references so they point to your local game installation's managed assemblies:

- `Wrath_Data/Managed/0Harmony.dll`
- `Wrath_Data/Managed/UnityEngine.CoreModule.dll`
- `Wrath_Data/Managed/UnityModManager/UnityModManager.dll`

No absolute paths are used in this repository.

## Output files

- `2DxFXShaderNullFix.csproj`
- `Main.cs`
- `Info.json`
- `README.md`
