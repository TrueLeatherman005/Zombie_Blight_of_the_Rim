using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using UnityEngine;

namespace ZombieBlight
{
    public class ZombieSpawnManager : MapComponent
    {
        private ZombieTrendCalculator trendCalculator;
        private int lastSpawnTick = -1;
        private const int MIN_SPAWN_INTERVAL = 2500; // Минимум 2500 тиков (~41 секунда)
        private const int MAX_ZOMBIES = 300; // Максимум зомби на карте
        
        private List<IntVec3> spawnPoints = new List<IntVec3>();
        private int spawnPointsUpdateInterval = 500;
        private int lastSpawnPointsUpdateTick = -1;

        public ZombieSpawnManager(Map map) : base(map)
        {
            trendCalculator = new ZombieTrendCalculator();
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();

            if (!map.IsPlayerHome) return;

            int currentTick = Find.TickManager.TicksGame;
            
            // Обновляем точки спавна периодически
            if (currentTick - lastSpawnPointsUpdateTick > spawnPointsUpdateInterval)
            {
                UpdateSpawnPoints();
                lastSpawnPointsUpdateTick = currentTick;
            }

            // Проверяем необходимость спавна
            if (currentTick - lastSpawnTick < MIN_SPAWN_INTERVAL) return;

            // Проверяем лимит зомби
            int currentZombieCount = map.mapPawns.AllPawnsSpawned
                .Count(p => p.Faction?.def == BlightDefOf.BlightSwarmFaction);

            if (currentZombieCount >= MAX_ZOMBIES) return;

            // Получаем текущее значение тренда
            float spawnChance = trendCalculator.GetCurrentTrendValue();
            
            // Базовый шанс спавна (раз в ~2500 тиков при тренде 1.0)
            if (!Rand.Chance(spawnChance * 0.04f)) return;

            TrySpawnZombieGroup();
            lastSpawnTick = currentTick;
        }

        private void UpdateSpawnPoints()
        {
            spawnPoints.Clear();
            
            // Собираем все точки по краям карты
            int mapSize = map.Size.x;
            for (int i = 0; i < mapSize; i++)
            {
                // Верхний край
                CheckAndAddSpawnPoint(new IntVec3(i, 0, mapSize - 1));
                // Нижний край
                CheckAndAddSpawnPoint(new IntVec3(i, 0, 0));
                // Левый край
                CheckAndAddSpawnPoint(new IntVec3(0, 0, i));
                // Правый край
                CheckAndAddSpawnPoint(new IntVec3(mapSize - 1, 0, i));
            }
        }

        private void CheckAndAddSpawnPoint(IntVec3 cell)
        {
            if (cell.Walkable(map) && !cell.Fogged(map))
            {
                spawnPoints.Add(cell);
            }
        }

        private void TrySpawnZombieGroup()
        {
            if (spawnPoints.Count == 0) return;

            // Выбираем случайную точку спавна
            IntVec3 spawnPoint = spawnPoints.RandomElement();

            // Определяем размер группы (1-3 базовый, 2-5 при высоком тренде)
            float trend = trendCalculator.GetCurrentTrendValue();
            int groupSize = trend > 0.7f ? 
                Rand.Range(2, 6) : 
                Rand.Range(1, 4);

            for (int i = 0; i < groupSize; i++)
            {
                IntVec3 spawnLoc = CellFinder.RandomClosewalkCellNear(spawnPoint, map, 4);
                SpawnZombie(spawnLoc);
            }
        }

        private void SpawnZombie(IntVec3 location)
        {
            Pawn zombie = ZombieGenerator.GenerateAndSpawn(location, map);
            if (zombie != null)
            {
                // После спавна проверяем, не превысили ли лимит
                ZombieValueSystem.DespawnExcessZombies(map, MAX_ZOMBIES);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref trendCalculator, "trendCalculator");
            Scribe_Values.Look(ref lastSpawnTick, "lastSpawnTick", -1);
        }
    }
}