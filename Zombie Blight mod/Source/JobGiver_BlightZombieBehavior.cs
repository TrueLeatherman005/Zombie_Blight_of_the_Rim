using RimWorld;
using Verse;
using Verse.AI;

namespace ZombieBlight
{
    public class JobGiver_BlightZombieBehavior : ThinkNode_JobGiver
    {
        private const float WanderRadius = 15f;

        protected override Job TryGiveJob(Pawn pawn)
        {
            // Simple wandering behavior when no scents are detected
            IntVec3 wanderLoc;
            if (CellFinder.TryFindRandomCellNear(pawn.Position, pawn.Map, (int)WanderRadius,
    (IntVec3 c) => c.Standable(pawn.Map) && pawn.CanReach(c, PathEndMode.OnCell, Danger.Deadly), out wanderLoc))
            {
                Job job = JobMaker.MakeJob(JobDefOf.Goto, wanderLoc);
                job.locomotionUrgency = LocomotionUrgency.Amble;
                job.expiryInterval = 500;
                return job;
            }
            return null;
        }
    }
}