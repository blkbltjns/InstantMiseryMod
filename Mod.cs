using MelonLoader;
using UnityEngine;
using HarmonyLib;
using Il2Cpp;
using ModSettings;
using System.Reflection;

[assembly: MelonInfo(typeof(CustomMiseryMod.CustomMiseryMod), "CustomMiseryMod", "1.0.0", "Author")]
[assembly: MelonGame("Hinterland", "TheLongDark")]

namespace CustomMiseryMod
{
    // --- MOD SETTINGS ---
    internal class CustomMiserySettings : JsonModSettings
    {
        [Name("Enable Instant Misery")]
        [Description("If enabled, launching a Misery run will instantly apply all 6 afflictions on Day 1.")]
        public bool EnableInstantMisery = false;

        [Name("Allow Healing with Broken Body")]
        [Description("If enabled, you can heal during sleep, but you STILL take double damage from Broken Body.")]
        public bool AllowHealing = true;
    }

    internal static class Settings
    {
        public static CustomMiserySettings options = new CustomMiserySettings();
        public static void OnLoad() { options.AddToModSettings("Custom Misery Options"); }
    }

    // --- MOD INIT ---
    public class CustomMiseryMod : MelonMod
    {
        public override void OnInitializeMelon()
        {
            Settings.OnLoad();
            MelonLogger.Msg("Custom Misery Mod loaded. Broken Body healing override active.");
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            Patches.hasCheckedForMiseryThisScene = false;
            Patches.frameDelay = 0;
        }
    }

    // --- HARMONY PATCHES ---
    internal static class Patches
    {
        public static bool hasCheckedForMiseryThisScene = false;
        public static int frameDelay = 0;

        // instant misery patch
        // inject afflictions 1 second after spawning
        [HarmonyPatch(typeof(Condition), "Update")]
        public class Condition_Update_Patch
        {
            public static void Prefix()
            {
                if (!hasCheckedForMiseryThisScene && Settings.options.EnableInstantMisery)
                {
                    if (frameDelay < 60)
                    {
                        frameDelay++;
                        return;
                    }

                    var miseryManager = UnityEngine.Object.FindObjectOfType<Il2CppTLD.Gameplay.MiseryManager>();
                    if (miseryManager != null)
                    {
                        // Lock the internal calendar so it doesn't double-apply the afflictions later
                        try {
                            var field = typeof(Il2CppTLD.Gameplay.MiseryManager).GetField("m_State", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            if (field != null) field.SetValue(miseryManager, 6);
                        } catch { }

                        // Inject all 6 Afflictions
                        miseryManager.ApplyMiseryAffliction(Il2CppTLD.Gameplay.MiseryManager.MiseryAffliction.PoorCirculation, true);
                        miseryManager.ApplyMiseryAffliction(Il2CppTLD.Gameplay.MiseryManager.MiseryAffliction.SourStomach, true);
                        miseryManager.ApplyMiseryAffliction(Il2CppTLD.Gameplay.MiseryManager.MiseryAffliction.UnsettledSleep, true);
                        miseryManager.ApplyMiseryAffliction(Il2CppTLD.Gameplay.MiseryManager.MiseryAffliction.WeakConstitution, true);
                        miseryManager.ApplyMiseryAffliction(Il2CppTLD.Gameplay.MiseryManager.MiseryAffliction.WeakJoints, true);
                        miseryManager.ApplyMiseryAffliction(Il2CppTLD.Gameplay.MiseryManager.MiseryAffliction.BrokenBody, true);
                    }
                    hasCheckedForMiseryThisScene = true; 
                }
            }
        }

        // HEAL WITH BROKEN BODY PATCH
        // We patch all 3 signatures of AddHealth to manually inject the health Broken Body tries to throw away.
        [HarmonyPatch(typeof(Condition), "AddHealth", new System.Type[] { typeof(float), typeof(DamageSource), typeof(bool) })]
        public class Condition_AddHealth_3Param_Patch
        {
            public static void Prefix(Condition __instance, float hp, out float __state)
            {
                __state = GetOriginalHP(__instance);
            }

            public static void Postfix(Condition __instance, float hp, float __state)
            {
                ApplyHealingOverride(__instance, hp, __state);
            }
        }

        [HarmonyPatch(typeof(Condition), "AddHealth", new System.Type[] { typeof(float), typeof(DamageSource) })]
        public class Condition_AddHealth_2Param_Patch
        {
            public static void Prefix(Condition __instance, float hp, out float __state)
            {
                __state = GetOriginalHP(__instance);
            }

            public static void Postfix(Condition __instance, float hp, float __state)
            {
                ApplyHealingOverride(__instance, hp, __state);
            }
        }

        [HarmonyPatch(typeof(Condition), "AddHealthWithNoHudNotification", new System.Type[] { typeof(float), typeof(DamageSource) })]
        public class Condition_AddHealthNoHud_Patch
        {
            public static void Prefix(Condition __instance, float hp, out float __state)
            {
                __state = GetOriginalHP(__instance);
            }

            public static void Postfix(Condition __instance, float hp, float __state)
            {
                ApplyHealingOverride(__instance, hp, __state);
            }
        }

        // Capture the Condition's current HP before the original AddHealth runs
        private static float GetOriginalHP(Condition instance)
        {
            return instance.m_CurrentHP;
        }

        private static void ApplyHealingOverride(Condition __instance, float hp, float originalHP)
        {
            if (hp > 0 && Settings.options.AllowHealing)
            {
                float expectedHP = Mathf.Min(originalHP + hp, __instance.m_MaxHP);
                if (__instance.m_CurrentHP < expectedHP)
                {
                    __instance.m_CurrentHP = expectedHP;
                }
            }
        }
    }
}