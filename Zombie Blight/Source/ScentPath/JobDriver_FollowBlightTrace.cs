using System.Collections.Generic;
using Verse;
using Verse.AI;
using RimWorld;

namespace ZombieBlight
{
    public class JobDriver_FollowBlightTrace : JobDriver
    {
        private const float TRACE_SEARCH_RADIUS = 5f;
        private const int MAX_PATH_TICKS = 300;
        private int pathTicks = 0;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true; // Не нужна резервация для следования по следу
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);

            var followTrace = new Toil
            {
                initAction = () =>
                {
                    pathTicks = 0;
                },
                tickAction = () =>
                {
                    pathTicks++;
                    if (pathTicks >= MAX_PATH_TICKS)
                    {
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }

                    Pawn pawn = this.pawn;
                    if (pawn.Map == null) return;

                    var traceSystem = pawn.Map.GetComponent<BlightTraceSystem>();
                    if (traceSystem == null) return;

                    // Ищем следы в радиусе
                    var traces = traceSystem.GetTracesInRadius(pawn.Position, TRACE_SEARCH_RADIUS);
                    if (traces.Count == 0)
                    {
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }

                    // Получаем клетку с самым сильным следом
                    IntVec3? strongestTrace = traceSystem.GetStrongestTrace(traces);
                    if (!strongestTrace.HasValue)
                    {
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }

                    // Идем к следу если не рядом
                    if (pawn.Position != strongestTrace.Value)
                    {
                        if (!pawn.pather.Moving)
                        {
                            pawn.pather.StartPath(strongestTrace.Value, PathEndMode.OnCell);
                        }
                    }
                    else
                    {
                        // Если дошли до следа, ищем следующий
                        var nextTraces = traces.Where(t => t != strongestTrace.Value).ToList();
                        IntVec3? nextTrace = traceSystem.GetStrongestTrace(nextTraces);
                        
                        if (!nextTrace.HasValue)
                        {
                            EndJobWith(JobCondition.Succeeded);
                            return;
                        }

                        pawn.pather.StartPath(nextTrace.Value, PathEndMode.OnCell);
                    }
                },
                defaultCompleteMode = ToilCompleteMode.Never
            };

            yield return followTrace;
        }
    }
}