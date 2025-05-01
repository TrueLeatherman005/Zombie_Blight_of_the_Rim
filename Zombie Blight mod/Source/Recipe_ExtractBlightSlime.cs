using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ZombieBlight
{
    // Расширение для рецепта, требующего определенный хедифф
    public class RecipeExtension_RequiredHediff : DefModExtension
    {
        public HediffDef hediffDef;
    }

    // Класс обработчика рецепта извлечения слизи
    public class Recipe_ExtractBlightSlime : RecipeWorker
    {
        public RecipeDef recipeDef;

        public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
        {
            // Проверяем, что это труп
            Corpse corpse = thing as Corpse;
            if (corpse == null) return false;

            // Проверяем наличие нужного хедиффа через расширение
            var extension = recipeDef.GetModExtension<RecipeExtension_RequiredHediff>();
            if (extension == null || extension.hediffDef == null)
            {
                return base.AvailableOnNow(thing, part);
            }

            // Проверяем наличие хедиффа на трупе
            Pawn innerPawn = corpse.InnerPawn;
            if (innerPawn == null) return false;

            return innerPawn.health?.hediffSet?.HasHediff(extension.hediffDef) == true;
        }

        public override void Notify_IterationCompleted(Pawn billDoer, List<Thing> ingredients)
        {
            Corpse corpse = ingredients.OfType<Corpse>().FirstOrDefault();
            if (corpse == null) return;

            Pawn innerPawn = corpse.InnerPawn;
            if (innerPawn == null) return;

            var extension = recipeDef.GetModExtension<RecipeExtension_RequiredHediff>();
            if (extension == null || extension.hediffDef == null) return;

            Hediff taintHediff = innerPawn.health.hediffSet.GetFirstHediffOfDef(extension.hediffDef);
            if (taintHediff == null) return;

            taintHediff.Severity -= 0.25f;

            if (taintHediff.Severity <= 0.1f)
            {
                innerPawn.health.RemoveHediff(taintHediff);
                Messages.Message("BlightSlimeExtracted_All".Translate(corpse.LabelShort), corpse, MessageTypeDefOf.NeutralEvent);
            }
            else
            {
                Messages.Message("BlightSlimeExtracted_Part".Translate(corpse.LabelShort), corpse, MessageTypeDefOf.NeutralEvent);
            }

            base.Notify_IterationCompleted(billDoer, ingredients);
        }
    }

}
