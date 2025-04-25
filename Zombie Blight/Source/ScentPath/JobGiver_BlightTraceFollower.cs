using RimWorld;
using Verse;
using Verse.AI;

namespace ZombieBlight
{
    public class JobGiver_BlightTraceFollower : ThinkNode_JobGiver
    {
        private const float TRACE_SEARCH_RADIUS = 5f;
        private const float MIN_TRACE_STRENGTH = 5f;

        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn?.Map == null || 
                pawn.Faction?.def != BlightDefOf.BlightSwarmFaction || 
                pawn.Downed)
                return null;

            // Проверяем, есть ли уже цель для атаки
            if (pawn.mindState.enemyTarget != null && 
                !pawn.mindState.enemyTarget.Destroyed)
                return null;

            var traceSystem = pawn.Map.GetComponent<BlightTraceSystem>();
            if (traceSystem == null) return null;

            // Ищем следы поблизости
            var traces = traceSystem.GetTracesInRadius(pawn.Position, TRACE_SEARCH_RADIUS);
            if (traces.Count == 0) return null;

            // Проверяем силу следов
            float maxStrength = traceSystem.GetStrongestTraceStrength(traces);
            if (maxStrength < MIN_TRACE_STRENGTH) return null;

            // Получаем самый сильный след
            IntVec3? strongestTrace = traceSystem.GetStrongestTrace(traces);
            if (!strongestTrace.HasValue) return null;

            // Создаем задание на следование
            Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("FollowBlightTrace"));
            job.targetA = new LocalTargetInfo(strongestTrace.Value);
            return job;
        }
    }
}