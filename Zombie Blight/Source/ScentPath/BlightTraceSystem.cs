using System.Collections.Generic;
using RimWorld;
using Verse;
using UnityEngine;
using System.Linq;

namespace ZombieBlight
{
    public class BlightTraceSystem : MapComponent
    {
        private HashSet<IntVec3> tracedCells = new HashSet<IntVec3>();
        private const float MIN_TRACE_INTERVAL = 2f; // Минимум 2 тика между следами
        private Dictionary<Pawn, int> lastTraceTickByPawn = new Dictionary<Pawn, int>();

        public BlightTraceSystem(Map map) : base(map) { }

        public override void MapComponentTick()
        {
            if (!ModsConfig.ActiveModsInLoadOrder.Any(m => m.Name.Contains("Zombie Blight")))
                return;

            // Проверяем всех пешек на карте
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                if (ShouldLeaveTrace(pawn))
                {
                    TryLeaveTrace(pawn);
                }
            }
        }

        private bool ShouldLeaveTrace(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Dead || pawn.Downed) return false;

            // Пропускаем зомби и механоидов
            if (pawn.Faction?.def == BlightDefOf.BlightSwarmFaction || 
                !pawn.RaceProps.IsFlesh) return false;

            // Проверяем интервал между следами
            int currentTick = Find.TickManager.TicksGame;
            int lastTraceTick;
            if (lastTraceTickByPawn.TryGetValue(pawn, out lastTraceTick))
            {
                if (currentTick - lastTraceTick < MIN_TRACE_INTERVAL)
                    return false;
            }

            return true;
        }

        public void TryLeaveTrace(Pawn pawn)
        {
            if (pawn?.Position == null || pawn.Map == null) return;

            // Проверяем существующий след
            Thing existingTrace = null;
            List<Thing> things = pawn.Position.GetThingList(map).ToList();
            foreach (Thing t in things)
            {
                if (t.def.defName == "BlightTrace")
                {
                    existingTrace = t;
                    break;
                }
            }

            if (existingTrace != null)
            {
                // Обновляем силу существующего следа
                var comp = existingTrace.TryGetComp<Comp_BlightTrace>();
                if (comp != null)
                {
                    comp.UpdateStrength(20f); // Базовая сила следа
                }
            }
            else if (!tracedCells.Contains(pawn.Position))
            {
                // Проверяем тип поверхности
                TerrainDef terrain = pawn.Position.GetTerrain(map);
                if (terrain != null && 
                    !terrain.HasTag("Marshy") && 
                    !terrain.HasTag("Water"))
                {
                    // Создаем новый след
                    Thing trace = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("BlightTrace"));
                    GenSpawn.Spawn(trace, pawn.Position, map);
                    tracedCells.Add(pawn.Position);
                }
            }

            // Обновляем время последнего следа
            lastTraceTickByPawn[pawn] = Find.TickManager.TicksGame;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref tracedCells, "tracedCells", LookMode.Value);
            // Не сохраняем lastTraceTickByPawn, он может быть пересоздан
        }

        public List<IntVec3> GetTracesInRadius(IntVec3 center, float radius)
        {
            List<IntVec3> result = new List<IntVec3>();
            int intRadius = Mathf.CeilToInt(radius);

            for (int dx = -intRadius; dx <= intRadius; dx++)
            {
                for (int dz = -intRadius; dz <= intRadius; dz++)
                {
                    IntVec3 cell = new IntVec3(center.x + dx, 0, center.z + dz);
                    if (cell.InBounds(map) && 
                        cell.DistanceTo(center) <= radius &&
                        tracedCells.Contains(cell))
                    {
                        Thing trace = cell.GetFirstThing(map, DefDatabase<ThingDef>.GetNamed("BlightTrace"));
                        if (trace != null)
                        {
                            result.Add(cell);
                        }
                        else
                        {
                            tracedCells.Remove(cell); // Очищаем устаревшие записи
                        }
                    }
                }
            }

            return result;
        }

        public float GetStrongestTraceStrength(List<IntVec3> cells)
        {
            float maxStrength = 0f;
            foreach (IntVec3 cell in cells)
            {
                Thing trace = cell.GetFirstThing(map, DefDatabase<ThingDef>.GetNamed("BlightTrace"));
                if (trace != null)
                {
                    var comp = trace.TryGetComp<Comp_BlightTrace>();
                    if (comp != null)
                    {
                        maxStrength = Mathf.Max(maxStrength, comp.Strength);
                    }
                }
            }
            return maxStrength;
        }

        public IntVec3? GetStrongestTrace(List<IntVec3> cells)
        {
            float maxStrength = 0f;
            IntVec3? result = null;

            foreach (IntVec3 cell in cells)
            {
                Thing trace = cell.GetFirstThing(map, DefDatabase<ThingDef>.GetNamed("BlightTrace"));
                if (trace != null)
                {
                    var comp = trace.TryGetComp<Comp_BlightTrace>();
                    if (comp != null && comp.Strength > maxStrength)
                    {
                        maxStrength = comp.Strength;
                        result = cell;
                    }
                }
            }

            return result;
        }
    }
}