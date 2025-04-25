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
        public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
        {
            // Проверяем, что это труп
            Corpse corpse = thing as Corpse;
            if (corpse == null) return false;
            
            // Проверяем наличие нужного хедиффа через расширение
            RecipeExtension_RequiredHediff extension = def.GetModExtension<RecipeExtension_RequiredHediff>();
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
            // Получаем труп из ингредиентов
            Corpse corpse = ingredients.OfType<Corpse>().FirstOrDefault();
            if (corpse == null) return;
            
            // После извлечения слизи, уменьшаем тяжесть хедиффа или удаляем его
            Pawn innerPawn = corpse.InnerPawn;
            if (innerPawn == null) return;
            
            // Получаем расширение рецепта
            RecipeExtension_RequiredHediff extension = def.GetModExtension<RecipeExtension_RequiredHediff>();
            if (extension == null || extension.hediffDef == null) return;
            
            // Находим хедифф заражения
            Hediff taintHediff = innerPawn.health.hediffSet.GetFirstHediffOfDef(extension.hediffDef);
            if (taintHediff == null) return;
            
            // Уменьшаем тяжесть хедиффа (представляет количество оставшейся слизи)
            taintHediff.Severity -= 0.25f;
            
            // Если тяжесть упала до минимума или ниже, удаляем хедифф
            if (taintHediff.Severity <= 0.1f)
            {
                innerPawn.health.RemoveHediff(taintHediff);
                
                // Выводим сообщение
                Messages.Message(
                    "BlightSlimeExtracted_All".Translate(corpse.LabelShort), 
                    corpse, 
                    MessageTypeDefOf.NeutralEvent);
            }
            else
            {
                // Выводим сообщение
                Messages.Message(
                    "BlightSlimeExtracted_Part".Translate(corpse.LabelShort), 
                    corpse, 
                    MessageTypeDefOf.NeutralEvent);
            }
            
            base.Notify_IterationCompleted(billDoer, ingredients);
        }
    }
}
