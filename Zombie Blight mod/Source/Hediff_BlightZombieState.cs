using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;


namespace ZombieBlight
{
    // Хедифф, представляющий активное состояние зомби
    public class Hediff_BlightZombieState : HediffWithComps // Наследует от HediffWithComps
    {
        // Константы и настройки
        private const int DefaultZombieLevel = 1; // Уровень зомби по умолчанию
        private const float AutoHealAmount = 0.5f; // Количество авто-лечения за тик
        private const int AutoHealInterval = 60; // Интервал авто-лечения (тики)
        private const int CheckIntervalTicks = 250; // Интервал для менее частых проверок (температура, слизь, жертвы)
        private const float MinHibernationTemperature = -10f; // Температура ниже которой зомби впадают в спячку
        private const float MaxHibernationTemperature = 0f; // Температура выше которой зомби выходят из спячки
        private const float SlimeCreationChancePerTick = 0.0005f; // Шанс создания слизи за тик при движении
        private const float VictimSearchRadius = 30f; // Радиус поиска потенциальных жертв
        private const float ActivateNearbyRadius = 15f; // Радиус активации других зомби при нахождении жертвы
        private const float StarvationPenaltyFactor = 0.02f; // Фактор штрафа от голодания на лечение

        // Переменные состояния
        private Faction originalFaction; // Оригинальная фракция пешки
        private bool isHibernating = false; // В спячке ли зомби
        private int ticksToNextAutoHeal; // Таймер авто-лечения
        private int ticksToNextCheck; // Таймер менее частых проверок
        private int zombieLevel = DefaultZombieLevel; // Уровень зомби

        // Кэш для GetZombieLevel()
        private int? cachedZombieLevel = null;

        // Хранение оригинальных инструментов (если требуется восстанавливать)
        private List<Tool> originalTools;

        // Свойства
        public int GetZombieLevel()
        {
            if (!cachedZombieLevel.HasValue)
            {
                cachedZombieLevel = zombieLevel;
            }
            return cachedZombieLevel.Value;
        }

        // Методы

        // Вызывается при добавлении хедиффа к пешке
        public override void PostMake()
        {
            base.PostMake();

            // Сохраняем оригинальную фракцию, если пешка не мертва
            if (!pawn.Dead)
            {
                originalFaction = pawn.Faction;
            }

            // Устанавливаем начальные таймеры
            ticksToNextAutoHeal = AutoHealInterval;
            ticksToNextCheck = CheckIntervalTicks;

            // Устанавливаем начальный уровень зомби
            zombieLevel = DefaultZombieLevel;
            cachedZombieLevel = null;

            // Очищаем ненужные хедиффы
            CleanupBodyHediffs();
            // Методы AddZombieToolsAndVerbs и RestoreOriginalTools удалены,
            // так как они не были реализованы и не вызывали ошибок компиляции.
            // Логика добавления атак должна быть реализована другим способом (например, HediffComp_VerbGiver).

            // Меняем фракцию на фракцию роя Блайта
            if (BlightDefOf.BlightSwarmFaction != null)
            {
                Faction blightFactionInstance = Find.FactionManager.FirstFactionOfDef(BlightDefOf.BlightSwarmFaction);
                if (blightFactionInstance != null)
                {
                    pawn.SetFaction(blightFactionInstance);
                }
                else
                {
                    Log.Error($"[Zombie Blight] Could not find BlightSwarmFaction instance on map for {pawn.LabelShort}. Pawn faction not changed.");
                }
            }
            else
            {
                Log.Error("[Zombie Blight] BlightDefOf.BlightSwarmFaction is null. Pawn faction not changed.");
            }

            pawn.health.hediffSet.DirtyCache();
        }

        // Вызывается при удалении хедиффа с пешки
        public override void PostRemoved()
        {
            base.PostRemoved();

            // Восстанавливаем оригинальную фракцию, если пешка мертва
            if (pawn.Dead)
            {
                RestoreOriginalFaction();
                // RestoreOriginalTools удален
                AddCorpseHediffs();
            }

            pawn.health.hediffSet.DirtyCache();
        }

        // Очистка ненужных хедиффов
        private void CleanupBodyHediffs()
        {
            if (pawn?.health?.hediffSet == null) return;

            List<HediffDef> hediffsToRemove = new List<HediffDef>
            {
                HediffDefOf.Malnutrition,
                HediffDefOf.BloodLoss,
                HediffDefOf.Hypothermia,
                HediffDefOf.Heatstroke,
                BlightDefOf.Hediff_BlightInfection,
                BlightDefOf.Hediff_BlightStarvation
            };

            List<Hediff> currentHediffs = new List<Hediff>(pawn.health.hediffSet.hediffs);

            foreach (Hediff hediff in currentHediffs)
            {
                if (hediffsToRemove.Contains(hediff.def))
                {
                    pawn.health.RemoveHediff(hediff);
                }
            }
        }

        // Восстановление оригинальной фракции
        private void RestoreOriginalFaction()
        {
            if (pawn != null && originalFaction != null)
            {
                pawn.SetFaction(originalFaction);
            }
        }

        // Добавление хедиффов трупу после удаления зомби-состояния
        private void AddCorpseHediffs()
        {
            if (pawn?.health?.hediffSet == null) return;

            if (BlightDefOf.Hediff_BlightCorpseTaint != null)
            {
                Hediff taintHediff = HediffMaker.MakeHediff(BlightDefOf.Hediff_BlightCorpseTaint, pawn);
                pawn.health.AddHediff(taintHediff);
            }
            else
            {
                Log.ErrorOnce("[Zombie Blight] BlightDefOf.Hediff_BlightCorpseTaint is null. Corpse taint hediff not added.", 123456);
            }

            if (BlightDefOf.Hediff_PostBlightWeakness != null)
            {
                Hediff weaknessHediff = HediffMaker.MakeHediff(BlightDefOf.Hediff_PostBlightWeakness, pawn);
                pawn.health.AddHediff(weaknessHediff);
            }
            else
            {
                Log.ErrorOnce("[Zombie Blight] BlightDefOf.Hediff_PostBlightWeakness is null. Post-blight weakness hediff not added.", 123457);
            }
        }

        // Проверка температуры и управление спячкой
        private void CheckTemperatureAndHibernate()
        {
            if (!pawn.Spawned) return;

            float ambientTemp = pawn.Position.GetTemperature(pawn.Map);

            if (!isHibernating && ambientTemp < MinHibernationTemperature)
            {
                EnterHibernation();
            }
            else if (isHibernating && ambientTemp > MaxHibernationTemperature)
            {
                ExitHibernation();
            }
        }

        // Ввод зомби в спячку
        private void EnterHibernation()
        {
            if (isHibernating) return;

            isHibernating = true;
            if (pawn.jobs != null)
            {
                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
                pawn.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Wait), JobCondition.InterruptForced);
            }

            if (Prefs.DevMode)
            {
                Log.Message($"[Zombie Blight] {pawn.LabelShort} entered hibernation.");
            }
        }

        // Вывод зомби из спячки
        private void ExitHibernation()
        {
            if (!isHibernating) return;

            isHibernating = false;
            if (pawn.jobs != null && pawn.CurJob.def == JobDefOf.Wait)
            {
                pawn.jobs.EndCurrentJob(JobCondition.Succeeded);
            }

            if (Prefs.DevMode)
            {
                Log.Message($"[Zombie Blight] {pawn.LabelShort} exited hibernation.");
            }
        }

        // Проверка, находится ли зомби в спячке
        public bool IsHibernating()
        {
            return isHibernating;
        }

        // Авто-лечение повреждений
        private void AutoHeal()
        {
            if (pawn?.health?.hediffSet == null || pawn.Dead || isHibernating) return;

            List<Hediff_Injury> injuries = new List<Hediff_Injury>();
            pawn.health.hediffSet.GetHediffs<Hediff_Injury>(ref injuries, null);

            float starvationPenalty = GetStarvationPenalty();

            float effectiveHealAmount = AutoHealAmount * (1f - starvationPenalty);

            foreach (Hediff_Injury injury in injuries)
            {
                float healThisInjury = Mathf.Min(effectiveHealAmount, injury.Severity);
                injury.Heal(healThisInjury);
                effectiveHealAmount -= healThisInjury;

                if (effectiveHealAmount <= 0) break;
            }
        }

        // Расчет штрафа голодания на авто-лечение
        private float GetStarvationPenalty()
        {
            if (pawn?.health?.hediffSet == null) return 0f;

            Hediff_BlightStarvation starvationHediff = pawn.health.hediffSet.GetFirstHediffOfDef(BlightDefOf.Hediff_BlightStarvation) as Hediff_BlightStarvation;

            if (starvationHediff != null)
            {
                return starvationHediff.Severity * StarvationPenaltyFactor;
            }

            return 0f;
        }

        // Поиск потенциальных жертв и активация других зомби
        private void FindVictimsNearby()
        {
            if (!pawn.Spawned || isHibernating) return;

            // Ищем ближайшую достижимую пешку, которая не является зомби и враждебна.
            Pawn victim = (Pawn)GenClosest.ClosestThingReachable(pawn.Position, pawn.Map, ThingRequest.ForGroup(ThingRequestGroup.Pawn), PathEndMode.ClosestTouch, TraverseParms.For(pawn), VictimSearchRadius,
                (Thing t) => // Валидатор принимает Thing, затем приводим к Pawn
                {
                    Pawn targetPawn = t as Pawn;
                    // Цель должна быть пешкой, не мертвой, не без сознания, не в криптосне, не зомби и враждебной
                    return targetPawn != null && !targetPawn.Dead && !targetPawn.Downed && !targetPawn.InBed() && !targetPawn.health.hediffSet.HasHediff(BlightDefOf.Hediff_BlightZombieState) && targetPawn.HostileTo(pawn);
                });

            if (victim != null)
            {
                pawn.mindState.enemyTarget = victim;
                ActivateNearbyZombies(victim);
            }
        }

        // Активация других зомби поблизости
        private void ActivateNearbyZombies(Pawn victim)
        {
            if (!pawn.Spawned) return;

            // Ищем все уникальные объекты (Thing) в радиусе, включая текущую клетку
            // Исправлен вызов GenRadial.RadialDistinctThingsAround, добавлен аргумент useCenter
            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(pawn.Position, pawn.Map, ActivateNearbyRadius, true)) // useCenter = true
            {
                Pawn nearbyPawn = thing as Pawn;
                if (nearbyPawn != null && nearbyPawn != pawn && nearbyPawn.health.hediffSet.HasHediff(BlightDefOf.Hediff_BlightZombieState) && nearbyPawn.mindState.enemyTarget == null)
                {
                    nearbyPawn.mindState.enemyTarget = victim;
                }
            }
        }

        // Попытка создать слизь на земле
        private void TryCreateSlime()
        {
            // Создаем слизь только если заспавнен, не в спячке и движется
            if (!pawn.Spawned || isHibernating || !pawn.pather.Moving)
            {
                return;
            }

            if (Rand.Chance(SlimeCreationChancePerTick))
            {
                if (BlightDefOf.Filth_BlightSlime != null)
                {
                    FilthMaker.TryMakeFilth(pawn.Position, pawn.Map, BlightDefOf.Filth_BlightSlime, Rand.Range(1, 3));
                }
                else
                {
                    Log.ErrorOnce("[Zombie Blight] BlightDefOf.Filth_BlightSlime is null. Cannot create slime filth.", 123456);
                }
            }
        }

        // --- Переопределения из Hediff / HediffWithComps ---

        // Вызывается каждый тик
        public override void Tick()
        {
            base.Tick();

            if (!pawn.Spawned || pawn.Dead) return;

            ticksToNextAutoHeal--;
            if (ticksToNextAutoHeal <= 0)
            {
                AutoHeal();
                ticksToNextAutoHeal = AutoHealInterval;
            }

            ticksToNextCheck--;
            if (ticksToNextCheck <= 0)
            {
                CheckTemperatureAndHibernate();
                TryCreateSlime();
                if (!isHibernating)
                {
                    FindVictimsNearby(); // Поиск жертв
                }
                ticksToNextCheck = CheckIntervalTicks;
            }
        }

        // Определяет, должен ли хедифф быть удален
        public override bool ShouldRemove
        {
            get
            {
                return base.ShouldRemove;
            }
        }

        // Вызывается после получения урона пешкой
        public override void Notify_PawnPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.Notify_PawnPostApplyDamage(dinfo, totalDamageDealt);

            // Сюда можно добавить логику реакции зомби на получение урона.
        }

        // Определяет, видим ли хедифф в UI
        public override bool Visible
        {
            get
            {
                return true;
            }
        }

        // --- Дополнительные вспомогательные методы ---

        // Устанавливает уровень зомби
        public void SetZombieLevel(int level)
        {
            if (level > 0)
            {
                zombieLevel = level;
                cachedZombieLevel = null;
                pawn.health.Notify_HediffChanged(this);
            }
        }

        // Проверяет, принадлежит ли пешка к фракции роя Блайта
        public bool IsBlightSwarmFaction()
        {
            return BlightDefOf.BlightSwarmFaction != null && pawn.Faction?.def == BlightDefOf.BlightSwarmFaction;
        }

        // --- Сохранение и Загрузка ---

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref originalFaction, "originalFaction");
            Scribe_Values.Look(ref isHibernating, "isHibernating", false);
            Scribe_Values.Look(ref ticksToNextAutoHeal, "ticksToNextAutoHeal", 0);
            Scribe_Values.Look(ref ticksToNextCheck, "ticksToNextCheck", 0);
            Scribe_Values.Look(ref zombieLevel, "zombieLevel", DefaultZombieLevel);
        }
    }
}

