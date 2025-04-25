using HarmonyLib;
using RimWorld;
using Verse;
using System.Collections.Generic;
using System.Linq;

namespace ZombieBlight
{
    // Harmony patch для динамического наполнения pawnGroup для BlightSwarmFaction
    [HarmonyPatch(typeof(PawnGroupMakerUtility), "GeneratePawns")]
    public static class Patch_PawnGroupMakerUtility_Blight
    {
        [HarmonyPrefix]
        public static bool Prefix(PawnGroupMakerParms parms, PawnGroupMaker groupMaker, ref IEnumerable<Pawn> __result)
        {
            if (parms.faction != null && parms.faction.def.defName == "BlightSwarmFaction")
            {
                // Собираем все PawnKindDef, которые являются животными или humanlike (и не механоиды)
                var allKinds = DefDatabase<PawnKindDef>.AllDefsListForReading
                    .Where(pk =>
                        (pk.RaceProps.Animal || (pk.RaceProps.Humanlike && !pk.RaceProps.IsMechanoid))
                        && pk.RaceProps.IsFlesh
                        && pk.defaultFactionType != null // Только те, что могут быть в фракциях
                    ).ToList();

                // Генерируем пачку зомби
                List<Pawn> pawns = new List<Pawn>();
                int count = Rand.RangeInclusive(3, 8); // Кол-во зомби в рейде (можно сделать зависимым от очков)
                for (int i = 0; i < count; i++)
                {
                    var kind = allKinds.RandomElement();
                    PawnGenerationRequest req = new PawnGenerationRequest(
                        kind,
                        parms.faction,
                        PawnGenerationContext.NonPlayer,
                        -1,
                        forceGenerateNewPawn: true,
                        allowDead: false,
                        allowDowned: false,
                        canGeneratePawnRelations: false,
                        mustBeCapableOfViolence: true
                    );
                    Pawn pawn = PawnGenerator.GeneratePawn(req);
                    pawns.Add(pawn);
                }
                __result = pawns;
                return false; // Блокируем оригинальный метод
            }
            return true;
        }
    }
}
