using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using System.Linq; 
using Verse.AI; 

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

            // 2. Рыночная стоимость (без множителей) - MarketValue может быть не очень релевантен для зомби,
            // но оставляем как было в оригинале.
            value += zombie.MarketValue;

            // 3. Прямой множитель размера тела (как есть)
            value *= zombie.BodySize;

           
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
                .OrderBy(x => x.Value) // Сначала удаляем слабых (меньшее значение)
                .ThenBy(x => GetEdgeDistance(x.Pawn.Position, map)) // Ближе к краю (меньшее расстояние)
                .Take(count)
                .Select(x => x.Pawn)
                .ToList();

            return zombies;
        }

        private static float GetEdgeDistance(IntVec3 pos, Map map)
        {
            // Вычисление минимального расстояния до любого края карты
            int minDist = Math.Min(
                Math.Min(pos.x, map.Size.x - pos.x),
                Math.Min(pos.z, map.Size.z - pos.z)
            );
            return minDist;
        }

        public static void DespawnExcessZombies(Map map, int maxCount)
        {
            if (map == null) return;

            
            var currentCount = map.mapPawns.AllPawnsSpawned
                .Count(p => p.Faction?.def == BlightDefOf.BlightSwarmFaction
                    && p.mindState.exitMapAfterTick < 0); // Не считаем уже уходящих (тех, у кого exitMapAfterTick >= 0)

            if (currentCount <= maxCount) return;

            var toRemove = GetRemovalCandidates(map, currentCount - maxCount);
            foreach (var zombie in toRemove)
            {
                // Пропускаем зомби, если:
                // Убрана некорректная проверка zombie.jobs?.curDriver?.locked
                // Исправлено: zombie.mindState.exitMapAfterTick заменено на zombie.mindState.exitMapAfterTick >= 0
                if (zombie.Downed || // повержен
                    zombie.mindState.exitMapAfterTick >= 0) // уже уходит (exitMapAfterTick >= 0)
                    continue;

                IntVec3 exitSpot = IntVec3.Invalid;
                float bestDistance = float.MaxValue;
                IntVec3 mapSize = map.Size;

                foreach (var cell in map.AllCells)
                {
                    bool isEdge = cell.x == 0 || cell.z == 0 || cell.x == mapSize.x - 1 || cell.z == mapSize.z - 1;

                    if (!isEdge || !cell.Standable(map)) continue;

                    if (zombie.CanReach(cell, PathEndMode.OnCell, Danger.Deadly))
                    {
                        float distance = (zombie.Position - cell).LengthHorizontalSquared;
                        if (distance < bestDistance)
                        {
                            bestDistance = distance;
                            exitSpot = cell;
                        }
                    }
                }
                bool foundSpot = exitSpot.IsValid;


                if (foundSpot && exitSpot.IsValid) // Проверяем, что точка найдена и валидна
                {
                    // Проверка досяжимости: убедимся, что пешка может добраться до найденной точки
                    if (zombie.CanReach(exitSpot, PathEndMode.OnCell, Danger.Deadly))
                    {
                        // Устанавливаем флаг выхода и путь к найденной точке
                        zombie.mindState.exitMapAfterTick = Find.TickManager.TicksGame + Rand.Range(100, 250);
                        zombie.pather?.StartPath(exitSpot, PathEndMode.OnCell);
                    }
                    // Если точка недостижима, пешка пропускается в этой итерации.
                }
                
            }
        }
    }
}