using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ZombieBlight
{
    public class BlightZombieController : GameComponent
    {
        private Dictionary<Map, ZombieSpawnManager> spawnManagers;

        public BlightZombieController(Game game) : base()
        {
            spawnManagers = new Dictionary<Map, ZombieSpawnManager>();
        }

        public override void GameComponentTick()
        {
            // GameComponent тикает реже чем MapComponent,
            // поэтому здесь можно делать только редкие проверки
            if (Find.TickManager.TicksGame % 2000 == 0)
            {
                CheckAndUpdateMaps();
            }
        }

        private void CheckAndUpdateMaps()
        {
            // Удаляем неактивные карты
            spawnManagers.RemoveAll(kvp => kvp.Key == null || kvp.Key.ParentFaction == null);

            // Добавляем новые карты поселений
            foreach (Map map in Find.Maps)
            {
                if (!spawnManagers.ContainsKey(map) && map.IsPlayerHome)
                {
                    spawnManagers[map] = new ZombieSpawnManager(map);
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref spawnManagers, "spawnManagers", LookMode.Reference, LookMode.Deep, ref mapKeys, ref managerValues);
        }

        // Временные переменные для сохранения
        private List<Map> mapKeys;
        private List<ZombieSpawnManager> managerValues;

        public static BlightZombieController Get
        {
            get
            {
                return Current.Game.GetComponent<BlightZombieController>();
            }
        }
    }
}