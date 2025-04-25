using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ZombieBlight
{
    public static class ZombieValueSystem
    {
        private const float BASE_ZOMBIE_VALUE = 500f;
        private const float LEVEL_MULTIPLIER = 0.15f;

        public static int CalculateZombieValue(Pawn zombie)
        {
            if (zombie == null) return 0;
            
            // 1. Базовые 500 очков
            float value = BASE_ZOMBIE_VALUE;
            
            // 2. Рыночная стоимость (без множителей)
            value += zombie.MarketValue;
            
            // 3. Прямой множитель размера тела (как есть)
            value *= zombie.BodySize;
            
            // 4. Множитель уровня (1.0, 1.2, 1.3...)
            Hediff_BlightZombieState zombieState = zombie.health?.hediffSet?.GetFirstHediffOfDef(BlightDefOf.Hediff_BlightZombieState) as Hediff_BlightZombieState;
            if (zombieState != null)
            {
                value *= 1f + (zombieState.GetZombieLevel() * LEVEL_MULTIPLIER);
            }
            
            return Mathf.RoundToInt(value);
        }

        public static List<Pawn> GetRemovalCandidates(Map map, int count)
        {
            if (map == null || count <= 0) return new List<Pawn>();

            var zombies = map.mapPawns.AllPawnsSpawned
                .Where(p => p.Faction?.def == BlightDefOf.BlightSwarmFaction)
                .Select(p => new { Pawn = p, Value = CalculateZombieValue(p) })
                .OrderBy(x => x.Value) // Сначала удаляем слабых
                .ThenBy(x => GetEdgeDistance(x.Pawn.Position, map)) // Ближе к краю
                .Take(count)
                .Select(x => x.Pawn)
                .ToList();

            return zombies;
        }

        private static float GetEdgeDistance(IntVec3 pos, Map map)
        {
            int minDist = Math.Min(
                Math.Min(pos.x, map.Size.x - pos.x),
                Math.Min(pos.z, map.Size.z - pos.z)
            );
            return minDist;
        }

        public static void DespawnExcessZombies(Map map, int maxCount)
        {
            var currentCount = map.mapPawns.AllPawnsSpawned
                .Count(p => p.Faction?.def == BlightDefOf.BlightSwarmFaction 
                    && !p.mindState.exitMapAfterTick  // Не считаем уже уходящих
                    && p.Map?.exitMapGrid?.IsInExitGroup(p.Position) == false); // Не считаем тех, кто уже в зоне выхода

            if (currentCount <= maxCount) return;

            var toRemove = GetRemovalCandidates(map, currentCount - maxCount);
            foreach (var zombie in toRemove)
            {
                // Пропускаем зомби, если:
                if (zombie.Downed || // повержен
                    zombie.Map?.exitMapGrid?.IsInExitGroup(zombie.Position) == true || // уже в зоне выхода
                    zombie.mindState.exitMapAfterTick || // уже уходит
                    zombie.jobs?.curDriver?.locked == true) // заблокирован в каком-то действии
                    continue;

                // Пробуем найти точку выхода
                IntVec3 exitSpot;
                if (RCellFinder.TryFindExitSpot(zombie, out exitSpot))
                {
                    // Только если путь доступен
                    if (zombie.CanReach(exitSpot, Verse.AI.PathEndMode.OnCell, Danger.Deadly))
                    {
                        zombie.mindState.exitMapAfterTick = Find.TickManager.TicksGame + Rand.Range(100, 250);
                        zombie.pather?.StartPath(exitSpot, Verse.AI.PathEndMode.OnCell);
                    }
                }
            }
        }
    }
}