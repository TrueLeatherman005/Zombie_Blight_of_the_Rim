using HarmonyLib;
using RimWorld;
using Verse;

namespace ZombieBlight
{
    [HarmonyPatch(typeof(PawnRenderer))]
    [HarmonyPatch("GetBodyOverlayMeshSet")]
    public static class Patch_ZombieRotDrawMode
    {
        public static void Postfix(PawnRenderer __instance, Pawn ___pawn, ref RotDrawMode __result)
        {
            if (___pawn?.health?.hediffSet == null) return;

            // Проверяем наличие нашего хедиффа зомби
            if (___pawn.health.hediffSet.HasHediff(BlightDefOf.Hediff_BlightZombieState))
            {
                __result = RotDrawMode.Rotting;
            }
        }
    }
}