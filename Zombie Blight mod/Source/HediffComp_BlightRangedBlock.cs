using RimWorld;
using Verse;
using System;

namespace ZombieBlight
{
    public class HediffCompProperties_BlightRangedBlock : HediffCompProperties
    {
        public HediffCompProperties_BlightRangedBlock()
        {
            this.compClass = typeof(HediffComp_BlightRangedBlock);
        }
    }
    public class HediffComp_BlightRangedBlock : HediffComp
    {
        private bool added = false;

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            if (!added && Pawn != null)
            {
                if (Pawn.AllComps.Find(c => c is Comp_BlightRangedBlock) == null)
                {
                    var props = new CompProperties_BlightRangedBlock();
                    var comp = (ThingComp)Activator.CreateInstance(props.compClass);
                    comp.parent = Pawn;
                    Pawn.AllComps.Add(comp);
                    comp.Initialize(props);
                }
                added = true;
            }
        }
    }
}
