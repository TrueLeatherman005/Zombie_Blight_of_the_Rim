using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ZombieBlight
{
    public class Hediff_BlightZombieState : HediffWithComps
    {
        // Хранение оригинальной фракции для восстановления при удалении хедиффа
        private Faction originalFaction;
        
        // Интервал проверки окружения (целей, температуры и т.д.)
        private static readonly IntRange CheckIntervalTicks = new IntRange(300, 600);
        
        // Состояние гибернации
        private bool isHibernating = false;
        
        // Интервал авто-лечения
        private int healingInterval = 60;
        
        // Метка поля для сохранения/загрузки
        private int nextCheckTick = -999;
        
        // Последний тик обработки регенерации
        private int lastHealTick = -999;
        
        // Отслеживаем удаление оружия
        private bool weaponRemoved = false;
        
        // Уровень зомби (1 - базовый, не может стрелять, 2+ - продвинутый)
        private int zombieLevel = 1;
        
        // Динамически добавляемые тулзы
        private List<Tool> originalTools = null;
        
        // --- Графика зомби ---
        private string originalNakedTexPath;
        private Graphic originalNakedGraphic;
        private bool usingFallbackZombieColor = false;

        // Запуск механики при создании
        public override void PostMake()
        {
            base.PostMake();
            
            // Запоминаем оригинальную фракцию перед изменением
            if (!pawn.Dead)
            {
                originalFaction = pawn.Faction;
                
                // Меняем фракцию на фракцию зомби
                if (originalFaction != BlightDefOf.BlightSwarmFaction)
                {
                    pawn.SetFaction(BlightDefOf.BlightSwarmFaction);
                }
                
                // Подавляем пацифизм
                if (pawn.story != null && pawn.story.traits != null)
                {
                    var pacifistTrait = pawn.story.traits.GetTrait(TraitDefOf.Pacifist);
                    if (pacifistTrait != null)
                    {
                        // НЕ удаляем, а только подавляем через internal флаг (или просто игнорируем при проверках)
                        // Вся логика подавления будет в CompAllowsVerb/Harmony
                    }
                    // Добавляем агрессивную черту зомби
                    if (!pawn.story.traits.HasTrait(DefDatabase<TraitDef>.GetNamedSilentFail("BlightZombiePacifismSuppressor")))
                    {
                        pawn.story.traits.GainTrait(DefDatabase<TraitDef>.GetNamedSilentFail("BlightZombiePacifismSuppressor"));
                    }
                }
                
                // Сбрасываем все негативные хедиффы уже мертвого тела
                CleanupBodyHediffs();
                
                // Проверяем и удаляем оружие дальнего боя для зомби первого уровня
                CheckAndDropRangedWeapon();
                
                // Динамически добавляем Tool/Verb для заражающих атак
                AddZombieToolsAndVerbs();
                
                // --- Гнилые текстуры ---
                ApplyRottingGraphics();
                
                // Устанавливаем интервалы
                nextCheckTick = Find.TickManager.TicksGame + CheckIntervalTicks.RandomInRange;
                lastHealTick = Find.TickManager.TicksGame;
            }
        }
        
        // Сохранение/загрузка данных
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref originalFaction, "originalFaction");
            Scribe_Values.Look(ref isHibernating, "isHibernating", false);
            Scribe_Values.Look(ref healingInterval, "healingInterval", 60);
            Scribe_Values.Look(ref nextCheckTick, "nextCheckTick", -999);
            Scribe_Values.Look(ref lastHealTick, "lastHealTick", -999);
            Scribe_Values.Look(ref weaponRemoved, "weaponRemoved", false);
            Scribe_Values.Look(ref zombieLevel, "zombieLevel", 1);
        }
        
        // Тик обработки состояния
        public override void Tick()
        {
            base.Tick();
            
            if (!pawn.Spawned || pawn.Dead) return;
            
            // Регенерация и другие эффекты только если не в гибернации
            if (!isHibernating)
            {
                // Блокируем возможность стрелять для зомби первого уровня
                if (zombieLevel == 1 && !weaponRemoved)
                {
                    CheckAndDropRangedWeapon();
                }
                
                // Обработка автолечения
                if (Find.TickManager.TicksGame - lastHealTick > healingInterval)
                {
                    lastHealTick = Find.TickManager.TicksGame;
                    AutoHeal();
                }
                
                // Периодическая проверка окружения и состояния
                if (Find.TickManager.TicksGame > nextCheckTick)
                {
                    nextCheckTick = Find.TickManager.TicksGame + CheckIntervalTicks.RandomInRange;
                    
                    // Проверяем температуру и обновляем состояние гибернации
                    CheckTemperatureAndHibernate();
                    
                    // Создание слизи при движении
                    TryCreateSlime();
                    
                    // Поиск жертв, если не в гибернации
                    if (!isHibernating)
                    {
                        FindVictimsNearby();
                    }
                }
            }
        }
        
        // Удаляем оружие дальнего боя у зомби первого уровня
        private void CheckAndDropRangedWeapon()
        {
            // Проверяем только для зомби первого уровня
            if (zombieLevel > 1) return;
            
            if (pawn.equipment != null && pawn.equipment.Primary != null)
            {
                ThingWithComps weapon = pawn.equipment.Primary;
                
                // Проверяем, является ли оружие оружием дальнего боя
                if (weapon.def.IsRangedWeapon)
                {
                    // Отбрасываем оружие на землю
                    pawn.equipment.TryDropEquipment(weapon, out _, pawn.Position);
                    
                    // Отмечаем, что оружие уже удалено
                    weaponRemoved = true;
                    
                    if (Prefs.DevMode)
                    {
                        Log.Message($"[Zombie Blight] Zombie {pawn.LabelShort} dropped ranged weapon {weapon.Label} due to being level 1 zombie");
                    }
                }
            }
            
            // Если у пешки нет оружия или оно не дальнего боя, отмечаем проверку как выполненную
            if (pawn.equipment == null || pawn.equipment.Primary == null || !pawn.equipment.Primary.def.IsRangedWeapon)
            {
                weaponRemoved = true;
            }
        }
        
        // Выдает текущий уровень зомби
        public int GetZombieLevel()
        {
            return zombieLevel;
        }
        
        // Устанавливает уровень зомби (для эволюции)
        public void SetZombieLevel(int newLevel)
        {
            if (newLevel < 1) newLevel = 1;
            bool wasLevel1 = zombieLevel == 1;
            zombieLevel = newLevel;
            
            // Если зомби был первого уровня и получил повышение,
            // сбрасываем флаг удаления оружия, чтобы он мог использовать новое
            if (wasLevel1 && zombieLevel > 1)
            {
                weaponRemoved = false;
            }
        }
        
        // Изменение внешнего вида зомби - применение трупной текстуры
        private void ApplyDessicatedGraphics()
        {
            if (pawn.Drawer?.renderer == null) return;
            
            try 
            {
                // В реальной реализации здесь будет использоваться отражение (Reflection)
                // для доступа к приватным свойствам рендерера и изменения типа графики на Dessicated
                
                // Примерный псевдокод:
                // FieldInfo dessicatedField = pawn.Drawer.renderer.GetType().GetField("dessicated", BindingFlags.NonPublic | BindingFlags.Instance);
                // if (dessicatedField != null)
                //     dessicatedField.SetValue(pawn.Drawer.renderer, true);
                
                // Или через официальный API, если он существует в текущей версии
                
                // Одновременно можно изменить анимацию
                // pawn.Drawer.renderer.SetAnimation(AnimationDefOf.ZombieWalk);
            }
            catch (Exception ex)
            {
                Log.Error($"Error applying dessicated graphics to zombie {pawn}: {ex.Message}");
            }
        }
        
        // Очистка ненужных хедиффов для зомби
        private void CleanupBodyHediffs()
        {
            if (pawn.health == null || pawn.health.hediffSet == null) return;
            
            // Список хедиффов для удаления
            List<Hediff> hediffsToRemove = new List<Hediff>();
            
            // Просматриваем все хедиффы
            foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
            {
                // Удаляем все инфекции
                if (hediff.def.makesSickThought)
                {
                    hediffsToRemove.Add(hediff);
                    continue;
                }
                
                // Удаляем кровопотерю - зомби не кровоточат
                if (hediff.def == HediffDefOf.BloodLoss)
                {
                    hediffsToRemove.Add(hediff);
                    continue;
                }
                
                // Удаляем гипотермию - зомби не чувствуют холода
                if (hediff.def == HediffDefOf.Hypothermia)
                {
                    hediffsToRemove.Add(hediff);
                    continue;
                }
                
                // Удаляем голодание - зомби не голодают обычным образом
                if (hediff.def == HediffDefOf.Malnutrition)
                {
                    hediffsToRemove.Add(hediff);
                    continue;
                }
                
                // Замечание: мы НЕ удаляем перегрев (Heatstroke), зомби могут перегреваться
                
                // Можно добавить и другие хедиффы, которые не должны влиять на зомби
            }
            
            // Удаляем найденные хедиффы
            foreach (Hediff hediff in hediffsToRemove)
            {
                pawn.health.RemoveHediff(hediff);
            }
        }
        
        // Создание слизи при движении зомби
        private void TryCreateSlime()
        {
            if (!pawn.Spawned || pawn.Dead || !pawn.Map.weatherManager.growthSeasonMemory.GrowthSeasonOutdoorsNow)
                return;
            
            // Шанс создать слизь при каждой проверке
            if (Rand.Chance(0.2f))
            {
                // Создаем Filth_BlightSlime на позиции пешки
                FilthMaker.TryMakeFilth(pawn.Position, pawn.Map, BlightDefOf.Filth_BlightSlime);
            }
        }
        
        // Проверка температуры и гибернация при холоде
        private void CheckTemperatureAndHibernate()
        {
            if (!pawn.Spawned) return;
            
            float ambientTemperature = pawn.Position.GetTemperature(pawn.Map);
            
            // Получаем минимальную комфортную температуру с учетом всех модификаторов
            float minComfortableTemp = pawn.GetStatValue(StatDefOf.ComfortableTemperatureMin);
            
            // Для зомби снижаем порог на 10 градусов (они более устойчивы к холоду)
            minComfortableTemp -= 10f;
            
            if (ambientTemperature < minComfortableTemp)
            {
                if (!isHibernating)
                {
                    EnterHibernation();
                }
            }
            else if (isHibernating && ambientTemperature > (minComfortableTemp + 2f)) // Небольшой гистерезис
            {
                ExitHibernation();
            }
            
            // Удаляем гипотермию только если температура выше порога гибернации
            if (ambientTemperature >= minComfortableTemp)
            {
                Hediff hypothermia = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Hypothermia);
                if (hypothermia != null)
                {
                    pawn.health.RemoveHediff(hypothermia);
                }
            }
            
            // Удаляем голодание, если оно появилось
            Hediff malnutrition = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Malnutrition);
            if (malnutrition != null)
            {
                pawn.health.RemoveHediff(malnutrition);
            }
            
            // Примечание: мы не удаляем тепловой удар, так как зомби могут перегреваться
        }
        
        // Вход в режим гибернации
        private void EnterHibernation()
        {
            if (isHibernating) return;
        
            isHibernating = true;
            // Добавляем эффекты гибернации
            pawn.stances.SetStance(new Stance_Cooldown(180, pawn, null));
            pawn.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Wait));
            
            // Замедляем потребление еды
            if (pawn.needs?.food != null)
            {
                pawn.needs.food.FallPerTickModifier = 0.1f;
            }
            
            // Блокируем возможность драфта
            pawn.drafter?.Drafted = false;
            
            // Завершаем текущую работу/задание
            pawn.jobs?.EndCurrentJob(JobCondition.InterruptForced);
            
            // В полной реализации, здесь будет добавление эффекта ступора/оглушения
            // и специальная поза для гибернации
            
            // А также снижение потребности в пище
            // pawn.needs.food?.tempFoodNeedMultiplier = 0.1f;
            
            Log.Message($"Zombie {pawn.LabelShort} entered hibernation due to low temperature");
        }
        
        // Выход из режима гибернации
        private void ExitHibernation()
        {
            if (!isHibernating) return;
        
            isHibernating = false;
            // Восстанавливаем нормальное состояние
            if (pawn.needs?.food != null)
            {
                pawn.needs.food.FallPerTickModifier = 1f;
            }
            
            // Возвращаем нормальную работу мозгов
            // Восстанавливаем нормальный рейт нужд
            
            Log.Message($"Zombie {pawn.LabelShort} exited hibernation as temperature rose");
        }
        
        // Автоматическое лечение повреждений
        private void AutoHeal()
        {
            if (pawn.Dead) return;
            
            // Получаем все повреждения (типа Hediff_Injury) пешки
            List<Hediff_Injury> injuries = pawn.health.hediffSet.GetHediffs<Hediff_Injury>().ToList();
            
            if (injuries.Count > 0)
            {
                // Выбираем случайное повреждение для лечения
                Hediff_Injury injury = injuries.RandomElement();
                
                // Уменьшаем тяжесть повреждения
                float healAmount = 0.01f * (1f - GetStarvationPenalty());
                injury.Severity -= healAmount;
                
                // Если тяжесть стала равна 0 или меньше, удаляем повреждение
                if (injury.Severity <= 0f)
                {
                    pawn.health.RemoveHediff(injury);
                }
                
                // Можно добавить эффект/частицы для визуализации лечения
                // EffectMaker.SpawnEffect(EffectDefOf.HealEffecter, pawn.DrawPos, pawn.Map);
            }
        }
        
        // Получение штрафа от голодания (влияет на регенерацию и другие способности)
        private float GetStarvationPenalty()
        {
            // Проверяем наличие хедиффа голодания
            Hediff_BlightStarvation starvation = pawn.health.hediffSet.GetFirstHediffOfDef(BlightDefOf.Hediff_BlightStarvation) as Hediff_BlightStarvation;
            
            if (starvation != null)
            {
                // Возвращаем тяжесть как штраф (0 до 1)
                return starvation.Severity;
            }
            
            return 0f;
        }
        
        // Поиск потенциальных жертв поблизости
        private void FindVictimsNearby()
        {
            if (!pawn.Spawned || pawn.Dead || isHibernating) return;
            
            // Проверка, есть ли текущая цель
            if (pawn.mindState?.enemyTarget != null && pawn.mindState.enemyTarget.Spawned) return;
            
            // Ищем потенциальные цели вокруг
            // Создаем параметры поиска
            TargetScanFlags flags = TargetScanFlags.NeedLOSToPawns | TargetScanFlags.NeedReachable;
            
            // Радиус поиска
            float searchRadius = 20f;
            
            // Ищем потенциальную цель
            Pawn targetPawn = (Pawn)AttackTargetFinder.BestAttackTarget(
                pawn,
                flags,
                (Thing t) => t is Pawn p && p.Faction != pawn.Faction && !p.Dead && !BlightUtility.IsSomehowImmuneToBlight(p),
                0f,
                searchRadius,
                default(IntVec3),
                float.MaxValue,
                canBashDoors: true,
                canTakeTargetsCloserThanEffectiveMinRange: true);
                
            // Если нашли цель
            if (targetPawn != null)
            {
                // Устанавливаем её как цель
                pawn.mindState.enemyTarget = targetPawn;
                
                // Оповещаем другие зомби рядом
                ActivateNearbyZombies(targetPawn);
                
                // Можно добавить звуки/эффекты охоты
                // SoundDef.BlightZombieAlert.PlayOneShot(pawn);
            }
        }
        
        // Активация других зомби рядом
        private void ActivateNearbyZombies(Thing target)
        {
            if (!pawn.Spawned || target == null) return;
            
            float activationRadius = 10f; // Радиус активации других зомби
            
            // Ищем зомби поблизости
            List<Pawn> nearbyZombies = pawn.Map.mapPawns.AllPawnsSpawned
                .Where(p => p != pawn && 
                       p.Faction == pawn.Faction && 
                       p.Position.InHorDistOf(pawn.Position, activationRadius) &&
                       p.health.hediffSet.HasHediff(BlightDefOf.Hediff_BlightZombieState))
                .ToList();
                
            // Активируем найденных зомби
            foreach (Pawn zombie in nearbyZombies)
            {
                if (zombie.mindState != null && zombie.mindState.enemyTarget == null)
                {
                    zombie.mindState.enemyTarget = target;
                    
                    // Можно добавить звуки/эффекты активации
                    // SoundDef.BlightZombieAlert.PlayOneShot(zombie);
                }
            }
        }
        
        // Динамическое добавление тулзов и вербов зомби
        private void AddZombieToolsAndVerbs()
        {
            // Сохраняем оригинальные тулзы только один раз
            if (originalTools == null && pawn.def.tools != null)
                originalTools = pawn.def.tools.ToList();
            
            if (pawn.def.tools == null)
                pawn.def.tools = new List<Tool>();
            
            var biteDef = DefDatabase<ToolDef>.GetNamedSilentFail("BlightZombieBite");
            var scratchDef = DefDatabase<ToolDef>.GetNamedSilentFail("BlightZombieScratch");
            if (biteDef != null)
                pawn.def.tools.AddRange(biteDef.tools);
            if (scratchDef != null)
                pawn.def.tools.AddRange(scratchDef.tools);
        }
        
        // Динамическое восстановление тулзов при снятии состояния
        private void RestoreOriginalTools()
        {
            if (originalTools != null)
                pawn.def.tools = originalTools.ToList();
        }
        
        // --- Графика зомби ---
        private void ApplyRottingGraphics()
        {
            var graphics = pawn.Drawer?.renderer?.graphics;
            if (graphics == null) return;

            // Сохраняем оригинал для возврата
            if (originalNakedGraphic == null)
            {
                originalNakedGraphic = graphics.nakedGraphic;
                originalNakedTexPath = (graphics.nakedGraphic as Graphic_Multi)?.path;
            }

            string rottingPath = GetRottingTexPath(originalNakedTexPath);
            Graphic rottingGraphic = null;
            try
            {
                rottingGraphic = GraphicDatabase.Get<Graphic_Multi>(
                    rottingPath, ShaderDatabase.Cutout, Vector2.one, pawn.story?.SkinColor ?? Color.white);
            }
            catch { }

            if (rottingGraphic != null && rottingGraphic.MatSingle != BaseContent.BadMat)
            {
                graphics.nakedGraphic = rottingGraphic;
                usingFallbackZombieColor = false;
            }
            else
            {
                // Fallback: делаем "грязно-черный" цвет только для тела (не для одежды)
                Color fallbackColor = new Color(0.22f, 0.16f, 0.13f, 1f); // грязно-темный
                graphics.nakedGraphic = GraphicDatabase.Get<Graphic_Multi>(
                    originalNakedTexPath, ShaderDatabase.Cutout, Vector2.one, fallbackColor);
                usingFallbackZombieColor = true;
            }
            graphics.ResolveAllGraphics();
        }

        private void RestoreOriginalGraphics()
        {
            var graphics = pawn.Drawer?.renderer?.graphics;
            if (graphics == null || originalNakedGraphic == null) return;
            graphics.nakedGraphic = originalNakedGraphic;
            graphics.ResolveAllGraphics();
            usingFallbackZombieColor = false;
        }

        private string GetRottingTexPath(string basePath)
        {
            if (string.IsNullOrEmpty(basePath)) return basePath;
            if (basePath.EndsWith("_rotting")) return basePath;
            int ext = basePath.LastIndexOf('.');
            if (ext > 0)
                return basePath.Substring(0, ext) + "_rotting" + basePath.Substring(ext);
            else
                return basePath + "_rotting";
        }

        // Вызывается при удалении хедиффа (например, после смерти зомби)
        public override void PostRemoved()
        {
            base.PostRemoved();
            
            // ВОССТАНАВЛИВАЕМ ТОЛЬКО ЕСЛИ ЭТО ТРУП!
            if (pawn.Dead && originalFaction != null)
            {
                // Возвращаем фракцию
                pawn.SetFaction(originalFaction);
                
                // Восстанавливаем оригинальные инструменты
                RestoreOriginalTools();
                
                // Добавляем хедиффы на труп
                AddCorpseHediffs();
                
                // НЕ восстанавливаем текстуры - они будут восстановлены
                // когда BlightCorpseTaint будет удален после экстракции слизи
            }
        }
        
        // Добавление хедиффов на труп после смерти зомби
        private void AddCorpseHediffs()
        {
            // Добавляем Taint для добычи слизи
            Hediff corpseTaint = HediffMaker.MakeHediff(BlightDefOf.Hediff_BlightCorpseTaint, pawn);
            pawn.health.AddHediff(corpseTaint);
            
            // Добавляем PostBlightWeakness для эффектов после воскрешения
            Hediff postWeakness = HediffMaker.MakeHediff(BlightDefOf.Hediff_PostBlightWeakness, pawn);
            pawn.health.AddHediff(postWeakness);
        }
        
        // Вызывается при смерти зомби
        public override void Notify_PawnKilled()
        {
            base.Notify_PawnKilled();
            
            // Дополнительные действия при смерти, например создание большого количества слизи
            if (pawn.Spawned)
            {
                // Создаем слизь на клетке смерти и вокруг
                FilthMaker.TryMakeFilth(pawn.Position, pawn.Map, BlightDefOf.Filth_BlightSlime, 5);
                
                // Дополнительно можно разбросать слизь по соседним клеткам
                for (int i = 0; i < 3; i++)
                {
                    IntVec3 cell = pawn.Position + GenRadial.RadialPattern[Rand.Range(1, 9)];
                    if (cell.InBounds(pawn.Map))
                    {
                        FilthMaker.TryMakeFilth(cell, pawn.Map, BlightDefOf.Filth_BlightSlime);
                    }
                }
            }
        }
        
        // Проверка состояния гибернации для AI
        public bool IsHibernating => isHibernating;

        public void SetHealingInterval(int interval)
        {
            healingInterval = interval;
        }
    }
}
