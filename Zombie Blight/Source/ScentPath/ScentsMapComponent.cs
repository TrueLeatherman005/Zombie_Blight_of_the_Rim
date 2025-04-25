using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace ZombieBlight 
{
    public class ScentInfo
    {
        public float strength;
        public int lastUpdatedTick;
        public bool sourceWasInfected;

        public ScentInfo(float strength, int lastUpdatedTick, bool sourceWasInfected)
        {
            this.strength = strength;
            this.lastUpdatedTick = lastUpdatedTick;
            this.sourceWasInfected = sourceWasInfected;
        }
    }

    public class ScentsMapComponent : MapComponent 
    {
        private Dictionary<IntVec3, ScentInfo> scents = new Dictionary<IntVec3, ScentInfo>();
        private const int UPDATE_INTERVAL = 250;
        private const float DECAY_RATE = 0.12f / 2500f; // 12% в час (2500 тиков = 1 час)
        
        public ScentsMapComponent(Map map) : base(map) { }

        public override void MapComponentTick()
        {
            if (Find.TickManager.TicksGame % UPDATE_INTERVAL == 0)
            {
                UpdateScents();
            }
        }

        public void AddOrUpdateScent(IntVec3 position, float strength, Pawn source)
        {
            bool isInfected = source.health?.hediffSet?.HasHediff(BlightDefOf.Hediff_BlightZombieState) ?? false;
            
            if (scents.TryGetValue(position, out var existingScent))
            {
                existingScent.strength = Mathf.Max(existingScent.strength, strength);
                existingScent.lastUpdatedTick = Find.TickManager.TicksGame;
                existingScent.sourceWasInfected |= isInfected;
            }
            else
            {
                scents[position] = new ScentInfo(strength, Find.TickManager.TicksGame, isInfected);
            }
        }

        private void UpdateScents()
        {
            var ticksGame = Find.TickManager.TicksGame;
            var toRemove = new List<IntVec3>();

            foreach (var kvp in scents)
            {
                var ticksPassed = ticksGame - kvp.Value.lastUpdatedTick;
                if (ticksPassed > 0)
                {
                    kvp.Value.strength -= DECAY_RATE * ticksPassed;
                    if (kvp.Value.strength <= 0.1f)
                    {
                        toRemove.Add(kvp.Key);
                    }
                }
            }

            foreach (var pos in toRemove)
            {
                scents.Remove(pos);
            }
        }

        public float GetScentIntensityAt(IntVec3 cell)
        {
            if (scents.TryGetValue(cell, out var scent))
            {
                return scent.strength;
            }
            return 0f;
        }

        public bool HasInfectedScentAt(IntVec3 cell)
        {
            return scents.TryGetValue(cell, out var scent) && scent.sourceWasInfected;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref scents, "scents", LookMode.Value, LookMode.Deep);
        }
    }
}