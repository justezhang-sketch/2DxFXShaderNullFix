using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;

namespace _2DxFXShaderNullFix
{
    public static class Main
    {
        private static UnityModManager.ModEntry.ModLogger _logger;

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            _logger = modEntry.Logger;

            try
            {
                var harmony = new Harmony(modEntry.Info.Id);
                harmony.PatchAll(Assembly.GetExecutingAssembly());
                _logger.Log("2DxFXShaderNullFix loaded.");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to patch: {ex}");
                return false;
            }
        }

        internal static string GetTransformPath(Component component)
        {
            if (component == null)
                return "<null component>";

            var transform = component.transform;
            if (transform == null)
                return "<null transform>";

            var path = transform.name;
            var current = transform.parent;

            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }

        [HarmonyPatch(typeof(_2dxFX_AL_DesintegrationFX), "OnEnable")]
        private static class DesintegrationFxOnEnablePatch
        {
            private const string ShaderName = "2DxFX/AL/DesintegrationFX";

            private static bool Prefix(_2dxFX_AL_DesintegrationFX __instance)
            {
                var shader = Shader.Find(ShaderName);
                if (shader != null)
                    return true;

                var path = GetTransformPath(__instance);
                _logger?.Warning($"Shader '{ShaderName}' not found for '{path}'. Disabling component and skipping OnEnable.");

                if (__instance != null)
                    __instance.enabled = false;

                return false;
            }

            private static Exception Finalizer(_2dxFX_AL_DesintegrationFX __instance, Exception __exception)
            {
                if (__exception is ArgumentNullException argumentNullException && argumentNullException.ParamName == "shader")
                {
                    var path = GetTransformPath(__instance);
                    _logger?.Warning($"Suppressed ArgumentNullException(shader) in OnEnable for '{path}'. Disabling component.");

                    if (__instance != null)
                        __instance.enabled = false;

                    return null;
                }

                return __exception;
            }
        }
    }
}
