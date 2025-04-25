using RimWorld;
using Verse;
using UnityEngine;

namespace ZombieBlight
{
    public class Comp_BlightTraceGenerator : ThingComp
    {
        private int nextTraceGenTick = -1;

        public CompProperties_BlightTraceGenerator Props => (CompProperties_BlightTraceGenerator)props;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            ResetNextTraceGenTick();
        }

        public override void CompTick()
        {
            base.CompTick();
            
            if (!parent.Spawned || parent is not Pawn pawn)
                return;

            // Only generate traces if the pawn has the blight infection
            if (!HasBlightInfection(pawn))
                return;

            if (Find.TickManager.TicksGame >= nextTraceGenTick)
            {
                TryGenerateTrace();
                ResetNextTraceGenTick();
            }
        }

        private void TryGenerateTrace()
        {
            if (Rand.Value > Props.traceChance)
                return;

            Thing trace = ThingMaker.MakeThing(BlightDefOf.BlightTrace);
            GenSpawn.Spawn(trace, parent.Position, parent.Map);
        }

        private void ResetNextTraceGenTick()
        {
            nextTraceGenTick = Find.TickManager.TicksGame + (int)Props.traceInterval;
        }

        private bool HasBlightInfection(Pawn pawn)
        {
            return pawn.health?.hediffSet?.HasHediff(BlightDefOf.Hediff_BlightInfection) ?? false;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref nextTraceGenTick, "nextTraceGenTick", -1);
        }
    }
}