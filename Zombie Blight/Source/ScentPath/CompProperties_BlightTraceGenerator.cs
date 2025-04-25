using Verse;

namespace ZombieBlight
{
    public class CompProperties_BlightTraceGenerator : CompProperties
    {
        public float traceInterval = 250f; // Ticks between trace generation
        public float traceChance = 0.75f;  // Chance to generate a trace each interval

        public CompProperties_BlightTraceGenerator()
        {
            compClass = typeof(Comp_BlightTraceGenerator);
        }
    }
}