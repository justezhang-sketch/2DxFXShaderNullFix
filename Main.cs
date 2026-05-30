using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;

namespace _2DxFXShaderNullFix
{
    public static class Main
    {
        private const string TargetTypeName = "_2dxFX_AL_DesintegrationFX";
        private const string TargetMethodName = "OnEnable";
        private const string ShaderName = "2DxFX/AL/DesintegrationFX";

        private static Harmony _harmony;
        private static UnityModManager.ModEntry _modEntry;

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            _modEntry = modEntry;

            try
            {
                _harmony = new Harmony(modEntry.Info.Id);

                Patch2DxFxOnEnable(modEntry);
                PatchParticleSystemPlay(modEntry);
            }
            catch (Exception ex)
            {
                modEntry.Logger.Error("Failed to patch 2DxFX shader null crash: " + ex);
            }

            return true;
        }

        private static void Patch2DxFxOnEnable(UnityModManager.ModEntry modEntry)
        {
            Type targetType = AccessTools.TypeByName(TargetTypeName);
            if (targetType == null)
            {
                modEntry.Logger.Error($"Could not find type: {TargetTypeName}");
                return;
            }

            MethodInfo targetMethod = AccessTools.Method(targetType, TargetMethodName);
            if (targetMethod == null)
            {
                modEntry.Logger.Error($"Could not find method: {TargetTypeName}.{TargetMethodName}");
                return;
            }

            HarmonyMethod prefix = new HarmonyMethod(
                typeof(Main).GetMethod(nameof(Prefix), BindingFlags.Static | BindingFlags.NonPublic)
            );

            HarmonyMethod finalizer = new HarmonyMethod(
                typeof(Main).GetMethod(nameof(Finalizer), BindingFlags.Static | BindingFlags.NonPublic)
            );

            _harmony.Patch(targetMethod, prefix: prefix, finalizer: finalizer);

            modEntry.Logger.Log($"Patched {TargetTypeName}.{TargetMethodName}");
        }

        private static void PatchParticleSystemPlay(UnityModManager.ModEntry modEntry)
        {
            HarmonyMethod prefix = new HarmonyMethod(
                typeof(Main).GetMethod(nameof(ParticleSystemPlayPrefix), BindingFlags.Static | BindingFlags.NonPublic)
            );

            PatchParticleSystemPlayOverload(modEntry, prefix, Type.EmptyTypes, "Play()");
            PatchParticleSystemPlayOverload(modEntry, prefix, new Type[] { typeof(bool) }, "Play(bool)");
        }

        private static void PatchParticleSystemPlayOverload(
            UnityModManager.ModEntry modEntry,
            HarmonyMethod prefix,
            Type[] argumentTypes,
            string displayName)
        {
            MethodInfo method = AccessTools.Method(typeof(ParticleSystem), nameof(ParticleSystem.Play), argumentTypes);
            if (method == null)
            {
                modEntry.Logger.Warning($"Could not find ParticleSystem.{displayName}; skipping particle mesh shape guard for this overload.");
                return;
            }

            _harmony.Patch(method, prefix: prefix);
            modEntry.Logger.Log($"Patched ParticleSystem.{displayName} with unreadable mesh shape guard");
        }

        private static bool Prefix(object __instance)
        {
            Shader shader = Shader.Find(ShaderName);

            if (shader != null)
            {
                return true;
            }

            MonoBehaviour behaviour = __instance as MonoBehaviour;
            if (behaviour != null)
            {
                _modEntry.Logger.Warning(
                    $"Missing shader '{ShaderName}' on object '{GetPath(behaviour.transform)}'. Disabling component and skipping OnEnable."
                );

                behaviour.enabled = false;
            }
            else
            {
                _modEntry.Logger.Warning(
                    $"Missing shader '{ShaderName}'. Skipping OnEnable."
                );
            }

            return false;
        }

        private static Exception Finalizer(object __instance, Exception __exception)
        {
            if (__exception == null)
            {
                return null;
            }

            ArgumentNullException argEx = __exception as ArgumentNullException;
            if (argEx != null && argEx.ParamName == "shader")
            {
                MonoBehaviour behaviour = __instance as MonoBehaviour;
                if (behaviour != null)
                {
                    _modEntry.Logger.Warning(
                        $"Suppressed ArgumentNullException(shader) on object '{GetPath(behaviour.transform)}'. Disabling component."
                    );

                    behaviour.enabled = false;
                }
                else
                {
                    _modEntry.Logger.Warning("Suppressed ArgumentNullException(shader).");
                }

                return null;
            }

            return __exception;
        }

        private static void ParticleSystemPlayPrefix(ParticleSystem __instance)
        {
            FixUnreadableMeshShape(__instance);
        }

        private static bool FixUnreadableMeshShape(ParticleSystem particleSystem)
        {
            if (particleSystem == null)
            {
                return false;
            }

            ParticleSystem.ShapeModule shape;

            try
            {
                shape = particleSystem.shape;
            }
            catch (Exception ex)
            {
                _modEntry.Logger.Warning($"[ParticleMeshFix] Failed to access shape module: {ex.GetType().Name}: {ex.Message}");
                return false;
            }

            if (!shape.enabled)
            {
                return false;
            }

            if (shape.shapeType != ParticleSystemShapeType.Mesh)
            {
                return false;
            }

            Mesh mesh;

            try
            {
                mesh = shape.mesh;
            }
            catch (Exception ex)
            {
                _modEntry.Logger.Warning($"[ParticleMeshFix] Failed to access shape mesh: {ex.GetType().Name}: {ex.Message}");
                return false;
            }

            if (mesh == null)
            {
                return false;
            }

            bool isReadable;

            try
            {
                isReadable = mesh.isReadable;
            }
            catch (Exception ex)
            {
                _modEntry.Logger.Warning($"[ParticleMeshFix] Failed to check mesh readability: {ex.GetType().Name}: {ex.Message}");
                return false;
            }

            if (isReadable)
            {
                return false;
            }

            _modEntry.Logger.Warning(
                $"[ParticleMeshFix] Unreadable mesh shape detected. " +
                $"path={GetPath(particleSystem.transform)}, " +
                $"scene={particleSystem.gameObject.scene.name}, " +
                $"mesh={mesh.name}. Disabling shape module only."
            );

            try
            {
                shape.enabled = false;
                return true;
            }
            catch (Exception ex)
            {
                _modEntry.Logger.Warning($"[ParticleMeshFix] Failed to disable shape module: {ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }

        private static string GetPath(Transform transform)
        {
            if (transform == null)
            {
                return "<null>";
            }

            string path = transform.name;

            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }

            return path;
        }
    }
}
