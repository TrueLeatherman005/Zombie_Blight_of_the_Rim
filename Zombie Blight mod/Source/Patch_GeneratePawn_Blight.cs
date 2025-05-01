using HarmonyLib;
using Verse;
using RimWorld;

namespace ZombieBlight
{
    [HarmonyPatch(typeof(PawnGenerator), "GeneratePawn")]
    public static class Patch_GeneratePawn_Blight
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn __result)
        {
            if (__result != null && __result.Faction != null && __result.Faction.def.defName == "BlightSwarmFaction")
            {
                // Add zombie state hediff if not already present
                if (__result.health.hediffSet.GetFirstHediffOfDef(DefDatabase<HediffDef>.GetNamed("Hediff_BlightZombieState"), false) == null)
                {
                    var hediff = HediffMaker.MakeHediff(DefDatabase<HediffDef>.GetNamed("Hediff_BlightZombieState"), __result);
                    __result.health.AddHediff(hediff);
                }
            }
        }
    }
}
