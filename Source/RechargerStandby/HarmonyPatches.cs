using HarmonyLib;
using RimWorld;
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
    }
}