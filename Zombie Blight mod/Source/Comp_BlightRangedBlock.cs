using RimWorld;
using Verse;

namespace ZombieBlight
{
    public class CompProperties_BlightRangedBlock : CompProperties
    {
        public CompProperties_BlightRangedBlock()
        {
            this.compClass = typeof(Comp_BlightRangedBlock);
        }
    }

    public class Comp_BlightRangedBlock : ThingComp
    {
        public override bool CompAllowVerbCast(Verb verb)
        {
            // Проверяем только для зомби первого уровня
            if (parent is Pawn pawn)
            {
                var zombieHediff = pawn.health?.hediffSet?.GetFirstHediffOfDef(BlightDefOf.Hediff_BlightZombieState) as Hediff_BlightZombieState;
                if (zombieHediff != null && zombieHediff.GetZombieLevel() == 1)
                {
                    // Разрешаем только атаки ближнего боя
                    return verb.IsMeleeAttack;
                }
            }
            // Для всех остальных случаев разрешаем все атаки
            return true;
        }
    }
}