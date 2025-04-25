using Verse;
using RimWorld;
using System.Linq;

namespace ZombieBlight
{
    public static class BlightTraceUtility
    {
        public static Thing FindNearestTrace(IntVec3 center, Map map, float radius)
        {
            return GenClosest.ClosestThingReachable(
                center,
                map,
                ThingRequest.ForDef(BlightDefOf.BlightTrace),
                PathEndMode.Touch,
                TraverseParms.For(TraverseMode.NoPassClosedDoors),
                radius);
        }

        public static bool HasRecentTrace(IntVec3 cell, Map map)
        {
            var traces = map.thingGrid.ThingsListAtFast(cell)
                .Where(t => t.def == BlightDefOf.BlightTrace);
                
            return traces.Any();
        }
    }
}