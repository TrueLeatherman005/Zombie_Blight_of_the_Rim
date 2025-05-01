using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace ZombieBlight
{
    public class CompGatherableSlime : ThingComp
    {
        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);

            if (Rand.Chance(0.3f))
            {
                Thing slime = ThingMaker.MakeThing(ThingDef.Named("Item_BlightSlimePack"));
                GenPlace.TryPlaceThing(slime, parent.Position, previousMap, ThingPlaceMode.Near);
            }
        }
    }

}