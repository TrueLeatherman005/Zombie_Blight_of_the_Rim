using System.Collections.Generic;
using Verse;
using RimWorld;
using Verse;

namespace ZombieBlight
{
    // Component to disable ranged attacks and remove tame command for zombies
    public class HediffComp_ZombieBehavior : HediffComp
    {
        public override bool CompAllowsVerb(Verb verb)
        {
            return verb.verbProps.IsMeleeAttack;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (var gizmo in base.CompGetGizmosExtra())
            {
                if (gizmo is Command_TryTame)
                    continue;
                yield return gizmo;
            }
        }
    }

    public class HediffCompProperties_ZombieBehavior : HediffCompProperties
    {
        public HediffCompProperties_ZombieBehavior()
        {
            compClass = typeof(HediffComp_ZombieBehavior);
        }
    }
}
