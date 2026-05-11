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

                Type targetType = AccessTools.TypeByName(TargetTypeName);
                if (targetType == null)
                {
                    modEntry.Logger.Error($"Could not find type: {TargetTypeName}");
                    return true;
                }

                MethodInfo targetMethod = AccessTools.Method(targetType, TargetMethodName);
                if (targetMethod == null)
                {
                    modEntry.Logger.Error($"Could not find method: {TargetTypeName}.{TargetMethodName}");
                    return true;
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
            catch (Exception ex)
            {
                modEntry.Logger.Error("Failed to patch 2DxFX shader null crash: " + ex);
            }

            return true;
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
