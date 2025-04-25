using RimWorld;
using Verse;
using Verse.AI;

namespace ZombieBlight
{
    public class WorkGiver_GatherBlightSlime : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest =>
            ThingRequest.ForDef(BlightDefOf.Filth_BlightSlime);

        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!(t is Filth filth) || !pawn.CanReserve(t, 1, -1, null, forced))
                return false;

            return true;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Job job = JobMaker.MakeJob(BlightDefOf.GatherBlightSlime, t);
            return job;
        }
    }

    public class JobDriver_GatherBlightSlime : JobDriver
    {
        private const TargetIndex FilthInd = TargetIndex.A;
        private const int WorkTicks = 200;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(FilthInd);
            
            yield return Toils_Goto.GotoThing(FilthInd, PathEndMode.Touch);
            
            Toil gatherToil = new Toil
            {
                initAction = () =>
                {
                    ticksLeftThisToil = WorkTicks;
                },
                tickAction = () =>
                {
                    if (ticksLeftThisToil <= 0)
                    {
                        // Шанс 30% получить пакет слизи
                        if (Rand.Chance(0.3f))
                        {
                            Thing slimePack = ThingMaker.MakeThing(BlightDefOf.Item_BlightSlimePack);
                            GenPlace.TryPlaceThing(slimePack, pawn.Position, Map, ThingPlaceMode.Near);
                        }
                        
                        // Удаляем грязь
                        job.targetA.Thing.Destroy();
                        ReadyForNextToil();
                    }
                },
                defaultCompleteMode = ToilCompleteMode.Never
            };
            gatherToil.WithProgressBar(FilthInd, () => 1f - (float)ticksLeftThisToil / WorkTicks);
            gatherToil.PlaySustainerOrSound(() => SoundDefOf.Harvest_Standard);
            yield return gatherToil;
        }
    }
}