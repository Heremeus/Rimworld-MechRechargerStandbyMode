using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;
using Verse.AI;

namespace RechargerStandby
{
    [StaticConstructorOnStartup]
    static class HarmonyPatches
    {
        private const string SUPERCHARGER_MOD_NAME = "MechSupercharger";
        private const string SUPERCHARGER_MOD_PACKAGE_ID_PREFIX = "rselbo.mechsupercharger";
        private static bool superchargerCompatibilityMode = false;

        // this static constructor runs to create a HarmonyInstance and install a patch.
        static HarmonyPatches()
        {
            Patch();
        }

        public static void Patch()
        {
            Harmony harmony = new Harmony("heremeus.rimworld.rechargerstandby.main");

            // find the MakeRecipeProducts method of the class GenRecipe
            MethodInfo targetMethod = AccessTools.Method(typeof(Building_MechCharger), "StartCharging");
            // find the static method to call after (i.e. postfix) the targetmethod
            HarmonyMethod postfixmethod = new HarmonyMethod(typeof(HarmonyPatches).GetMethod("Building_MechCharger_StartOrStopCharging_Postfix"));
            // patch the targetmethod, by calling postfixmethod after it ran, with no prefixmethod (i.e. null)
            harmony.Patch(targetMethod, null, postfixmethod);

            targetMethod = AccessTools.Method(typeof(Building_MechCharger), "StopCharging");
            harmony.Patch(targetMethod, null, postfixmethod); // Patch with same postfix method

            targetMethod = AccessTools.Method(typeof(CompPowerTrader), "SetUpPowerVars");
            postfixmethod = new HarmonyMethod(typeof(HarmonyPatches).GetMethod("CompPowerTrader_SetUpPowerVars_Postfix"));
            harmony.Patch(targetMethod, null, postfixmethod);

            targetMethod = AccessTools.Method(typeof(CompPowerTrader), "CompInspectStringExtra");
            postfixmethod = new HarmonyMethod(typeof(HarmonyPatches).GetMethod("CompPowerTrader_CompInspectStringExtra_Postfix"));
            harmony.Patch(targetMethod, null, postfixmethod);

            CompatibilityChecks();
        }

        private static void CompatibilityChecks()
        {
            superchargerCompatibilityMode = LoadedModManager.RunningModsListForReading.Any(x => x.Name == SUPERCHARGER_MOD_NAME && x.PackageId.StartsWith(SUPERCHARGER_MOD_PACKAGE_ID_PREFIX));
        }

        public static void Building_MechCharger_StartOrStopCharging_Postfix(Building_MechCharger __instance, Pawn ___currentlyChargingMech)
        {
            UpdatePowerConsumptionOfCharger(__instance, ___currentlyChargingMech);
        }

        public static void CompPowerTrader_SetUpPowerVars_Postfix(CompPowerTrader __instance)
        {
            if (__instance.parent == null) return;

            Building_MechCharger charger = __instance.parent as Building_MechCharger;
            if (charger == null) return;

            FieldInfo field = charger.GetType().GetField("currentlyChargingMech", BindingFlags.NonPublic | BindingFlags.Instance);
            Pawn currentlyChargingMech = field.GetValue(charger) as Pawn;

            UpdatePowerConsumptionOfCharger(charger, currentlyChargingMech);
        }

        private static void UpdatePowerConsumptionOfCharger(Building_MechCharger charger, Pawn currentlyChargingMech)
        {
            if (superchargerCompatibilityMode && charger.GetType().FullName == "MechSupercharger.Building_MechSupercharger")
            {
                // This is a supercharger from https://steamcommunity.com/sharedfiles/filedetails/?id=2881643178
                // The mod has it's own idle power settings and therefore does not need to be modified
                return;
            }

            CompPowerTrader compPowerTrader = charger.GetComp<CompPowerTrader>();
            if (compPowerTrader == null)
            {
                Log.Warning("Missing CompPowerTrader on mech charger");
                return;
            }
            bool isCharging = currentlyChargingMech != null;
            float powerConsumption;
            if (isCharging)
            {
                powerConsumption = compPowerTrader.Props.PowerConsumption;
            }
            else
            {
                StandbyConsumptionDefModExtension defExtension = charger.def.GetModExtension<StandbyConsumptionDefModExtension>();
                if (defExtension == null)
                {
                    Log.Warning("Missing StandbyConsumptionDefModExtension on mech charger def '" + charger.def.defName + "'");
                    return;
                }
                powerConsumption = defExtension.standbyPowerConsumption;
            }
            compPowerTrader.PowerOutput = -1f * powerConsumption;
        }

        public static void CompPowerTrader_CompInspectStringExtra_Postfix(CompPowerTrader __instance, ref string __result)
        {
            if (__instance.parent == null) return;

            Building_MechCharger charger = __instance.parent as Building_MechCharger;
            if (charger == null) return;

            FieldInfo field = charger.GetType().GetField("currentlyChargingMech", BindingFlags.NonPublic | BindingFlags.Instance);
            Pawn currentlyChargingMech = field.GetValue(charger) as Pawn;
            if (currentlyChargingMech != null)
            {
                return;
            }

            String originalPowerConsumption = __instance.Props.PowerConsumption.ToString("#####0") + " W";
            String actualPowerConsumption = (-1 * __instance.PowerOutput).ToString("#####0") + " W";
            __result = __result.Replace(originalPowerConsumption, actualPowerConsumption);
        }
    }
}