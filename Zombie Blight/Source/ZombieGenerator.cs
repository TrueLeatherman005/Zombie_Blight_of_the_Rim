using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using UnityEngine;

namespace ZombieBlight
{
    public static class ZombieGenerator
    {
        private static List<PawnKindDef> cachedValidKinds;
        private static int lastCacheUpdateTick = -1;
        private const int CACHE_UPDATE_INTERVAL = 10000; // ~167 сек

        public static Pawn GenerateAndSpawn(IntVec3 location, Map map)
        {
            if (map == null) return null;

            UpdateKindCache();
            
            if (cachedValidKinds.NullOrEmpty())
            {
                Log.Error("[Zombie Blight] No valid PawnKindDefs found for zombie generation");
                return null;
            }

            // Выбираем случайный вид пешки
            PawnKindDef kind = cachedValidKinds.RandomElement();
            
            try
            {
                // Создаем базовую пешку
                PawnGenerationRequest request = new PawnGenerationRequest(
                    kind: kind,
                    faction: Find.FactionManager.FirstFactionOfDef(BlightDefOf.BlightSwarmFaction),
                    context: PawnGenerationContext.NonPlayer,
                    tile: map.Tile,
                    forceGenerateNewPawn: true,
                    allowDead: false,
                    allowDowned: false,
                    canGeneratePawnRelations: true,
                    mustBeCapableOfViolence: true,
                    colonistRelationChanceFactor: 0f,
                    forceAddFreeWarmLayerIfNeeded: false,
                    allowGay: false,
                    allowFood: false,
                    allowAddictions: true,
                    inhabitant: false);

                Pawn pawn = PawnGenerator.GeneratePawn(request);
                if (pawn == null)
                {
                    Log.Error("[Zombie Blight] Failed to generate zombie pawn");
                    return null;
                }

                // Очищаем ненужные хедиффы
                ClearUnwantedHediffs(pawn);
                
                // Добавляем хедифф зомби
                Hediff zombieState = HediffMaker.MakeHediff(BlightDefOf.Hediff_BlightZombieState, pawn);
                pawn.health.AddHediff(zombieState);

                // Спавним на карту
                GenSpawn.Spawn(pawn, location, map);
                
                return pawn;
            }
            catch (Exception e)
            {
                Log.Error($"[Zombie Blight] Error generating zombie: {e}");
                return null;
            }
        }

        private static void UpdateKindCache()
        {
            int currentTick = Find.TickManager?.TicksGame ?? 0;
            if (cachedValidKinds != null && currentTick - lastCacheUpdateTick < CACHE_UPDATE_INTERVAL)
                return;

            lastCacheUpdateTick = currentTick;
            cachedValidKinds = new List<PawnKindDef>();

            // Собираем все подходящие виды пешек
            foreach (PawnKindDef kind in DefDatabase<PawnKindDef>.AllDefsListForReading)
            {
                if (IsValidForZombification(kind))
                    cachedValidKinds.Add(kind);
            }
        }

        private static bool IsValidForZombification(PawnKindDef kind)
        {
            if (kind == null || kind.RaceProps == null) return false;

            return kind.RaceProps.IsFlesh && // Только органические расы
                   !kind.RaceProps.IsMechanoid && // Не механоиды
                   kind.defaultFactionType != null && // Должны иметь фракцию
                   (kind.RaceProps.Humanlike || kind.RaceProps.Animal); // Люди или животные
        }

        private static void ClearUnwantedHediffs(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return;

            // Создаем копию списка, так как будем удалять из оригинала
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs.ToList();
            
            foreach (Hediff hediff in hediffs)
            {
                // Оставляем только базовые части тела и критически важные хедиффы
                if (!(hediff is Hediff_AddedPart || hediff is Hediff_ImplantedBionic))
                {
                    pawn.health.RemoveHediff(hediff);
                }
            }
        }
    }
}