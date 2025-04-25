using Verse;

namespace ZombieBlight
{
    public class CompProperties_ScentMarker : CompProperties
    {
        public float baseStrength = 1f;
        public int updateInterval = 60; // Every 60 ticks
        
        public CompProperties_ScentMarker()
        {
            compClass = typeof(Comp_ScentMarker);
        }
    }

    public class Comp_ScentMarker : ThingComp
    {
        private int ticksToNextUpdate;
        
        public CompProperties_ScentMarker Props => (CompProperties_ScentMarker)props;

        public override void Initialize(CompProperties props)
        {
            base.Initialize(props);
            ResetUpdateTimer();
        }

        private void ResetUpdateTimer()
        {
            ticksToNextUpdate = Props.updateInterval;
        }

        public override void CompTick()
        {
            if (parent is not Pawn pawn || !pawn.Spawned)
                return;

            // Don't leave scents if already a zombie
            if (pawn.health?.hediffSet?.HasHediff(BlightDefOf.Hediff_BlightZombieState) ?? false)
                return;
                
            if (--ticksToNextUpdate <= 0)
            {
                var scents = pawn.Map.GetComponent<ScentsMapComponent>();
                if (scents != null)
                {
                    // Leave stronger scent if the pawn is bleeding
                    float strengthMultiplier = pawn.health?.hediffSet?.BleedRateTotal > 0 ? 2f : 1f;
                    scents.AddOrUpdateScent(pawn.Position, Props.baseStrength * strengthMultiplier, pawn);
                }
                ResetUpdateTimer();
            }
        }
    }
}