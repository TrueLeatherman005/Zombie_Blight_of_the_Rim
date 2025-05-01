using RimWorld;
using Verse;

namespace ZombieBlight 
{
    public class CompProperties_BlightGas : CompProperties
    {
        public CompProperties_BlightGas()
        {
            this.compClass = typeof(Comp_BlightGas);
        }
    }

    public class Comp_BlightGas : ThingComp
    {
        private int ticksToNextEffect = 60;
        
        public override void CompTick()
        {
            base.CompTick();
            
            if (--ticksToNextEffect <= 0)
            {
                ticksToNextEffect = 60; // Проверка каждую секунду
                
                // Проверяем всех пешек в радиусе действия газа
                foreach (Thing thing in GenRadial.RadialDistinctThingsAround(parent.Position, parent.Map, 3f, true))
                {
                    if (thing is Pawn pawn)
                    {
                        // Пытаемся заразить с небольшой тяжестью
                        BlightUtility.TryInfectPawn(pawn, 0.02f);
                    }
                }
            }
        }
    }
}