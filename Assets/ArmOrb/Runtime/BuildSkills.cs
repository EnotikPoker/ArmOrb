using System;
using UnityEngine;

namespace ArmOrb
{
    // These IDs are persisted in the v0.8 command journal. Append new IDs; never reorder.
    public enum BuildSkill
    {
        Vitality, Regeneration, SecondWind, Barrier, Vampirism, Armor, Adaptation, Resurrection,
        Power, Critical, Rage, Execution, Growth, Overload, Rhythm, Hunter, Burn, Bleed, Suppression, Pierce,
        SwordHeavy, SpearLunge, BladeMomentum, ScytheHarvest, ShieldStorage, UnarmedRam,
        BowDouble, CrossbowPierce, StaffEcho, ShurikenRicochet
    }
    public enum BattleVariant { Builds, Ability, Sparks }
    public enum BattleAbility { Heal, Barrier }
    public enum BuildCommandKind { Upgrade, Ability, Spark, OfferOpen, OfferClose }

    public static class BuildSkills
    {
        public const int Count = 30;
        public static readonly string[] VariantNames = { "Только улучшения", "Способность", "Искры" };
        public static readonly string[] AbilityNames = { "Лечение · 100 HP", "Барьер · 120" };
        static readonly string[] names = {
            "Живучесть", "Регенерация", "Второе дыхание", "Барьер", "Вампиризм", "Броня", "Адаптация", "Возрождение",
            "Мощь", "Критический удар", "Ярость", "Добивание", "Разгон оружия", "Перегрузка", "Боевой ритм", "Охотник",
            "Ожог", "Кровотечение", "Подавление", "Пробой", "Тяжёлый удар", "Выпад", "Раскрутка", "Жатва", "Накопитель", "Таран",
            "Двойная очередь", "Сквозной залп", "Повторный взрыв", "Рикошет"
        };
        static readonly string[] icons = { "HP", "+", "II", "Б", "V", "A", "AD", "R", "DM", "!", "Я", "X", "↑", "OV", "4", "H", "F", "B", "−", "P", "M", "K", "КЛ", "Ж", "Щ", "Т", "Л", "АР", "П", "С" };
        public static string Name(BuildSkill skill) => names[(int)skill];
        public static string Icon(BuildSkill skill) => icons[(int)skill];
        public static int MaxLevel(BuildSkill skill) => skill is BuildSkill.Vitality or BuildSkill.Regeneration or BuildSkill.Barrier or BuildSkill.Vampirism or BuildSkill.Armor or BuildSkill.Power or BuildSkill.Critical or BuildSkill.Growth ? 3 : 1;
        public static bool Defensive(BuildSkill skill) => (int)skill <= 7;
        public static float Value(int level, float first, float second, float third) => level <= 0 ? 0 : level == 1 ? first : level == 2 ? second : third;
        public static bool Applicable(BuildSkill skill, WeaponKind weapon)
        {
            if ((int)skill >= 20) return skill == Special(weapon);
            if (weapon != WeaponKind.Shield) return true;
            return (Defensive(skill) && skill != BuildSkill.Vampirism) || skill == BuildSkill.Hunter;
        }
        public static BuildSkill Special(WeaponKind weapon) => weapon switch {
            WeaponKind.Sword => BuildSkill.SwordHeavy, WeaponKind.Spear => BuildSkill.SpearLunge,
            WeaponKind.Blade => BuildSkill.BladeMomentum, WeaponKind.Scythe => BuildSkill.ScytheHarvest,
            WeaponKind.Shield => BuildSkill.ShieldStorage, WeaponKind.Unarmed => BuildSkill.UnarmedRam,
            WeaponKind.Bow => BuildSkill.BowDouble, WeaponKind.Crossbow => BuildSkill.CrossbowPierce,
            WeaponKind.Staff => BuildSkill.StaffEcho, _ => BuildSkill.ShurikenRicochet };
        public static string Description(BuildSkill skill, int level, float initialHP = 300, WeaponKind weapon = WeaponKind.Sword)
        {
            level = Mathf.Clamp(level, 1, MaxLevel(skill));
            float previous = Value(level - 1, 150, 250, 350), extra = Value(level, 150, 250, 350);
            return skill switch {
                BuildSkill.Vitality => $"Максимальное HP: {initialHP + previous:0} → {initialHP + extra:0}. Текущее +{extra-previous:0}.",
                BuildSkill.Regeneration => $"Восстановление {Value(level,4,6,8):0} HP/с.",
                BuildSkill.SecondWind => "Один раз лечит на 120, когда остаётся не больше 25% HP. Не спасает от смертельного удара.",
                BuildSkill.Barrier => $"Поглощает {Value(level,80,120,160):0} урона. Восстанавливается через 15 с после разрушения.",
                BuildSkill.Vampirism => $"Лечение на {Value(level,15,20,25):0}% прямого урона по HP. Без яда и отражений.",
                BuildSkill.Armor => $"На {Value(level,25,35,45):0}% меньше входящего урона.",
                BuildSkill.Adaptation => "Полученный прямой удар даёт 5% защиты на 3 с. До 6 слоёв.",
                BuildSkill.Resurrection => "Один раз возвращает с 35% максимального HP и неуязвимостью на 1 с.",
                BuildSkill.Power => $"+{Value(level,40,70,100):0}% прямого урона.",
                BuildSkill.Critical => $"{Value(level,25,35,45):0}% шанс двойного прямого урона.",
                BuildSkill.Rage => "До +60% прямого урона по мере потери HP.",
                BuildSkill.Execution => "+60% прямого урона по врагу с HP ниже 30%.",
                BuildSkill.Growth => $"+{Value(level,50,75,100):0}% к приросту характеристики оружия за попадание.",
                BuildSkill.Overload => "+70% прямого урона. Получаемый урон +25%.",
                BuildSkill.Rhythm => "После 3 успешных прямых попаданий 4-е наносит двойной урон.",
                BuildSkill.Hunter => weapon == WeaponKind.Shield ? "За каждое убийство восстанавливает 100 HP." : "За каждое убийство: лечение на 100 и +20% прямого урона до конца боя.",
                BuildSkill.Burn => "Прямой удар: ожог 5 HP/с на 4 с. Повтор обновляет время.",
                BuildSkill.Bleed => "Прямой удар: кровотечение 2 HP/с на 5 с. До 5 слоёв.",
                BuildSkill.Suppression => "Прямой удар на 5 с снижает лечение врага на 60%.",
                BuildSkill.Pierce => "Прямые атаки игнорируют половину процентной защиты. Не пробивают щит и барьеры.",
                BuildSkill.SwordHeavy => "Каждое 4-е попадание мечом наносит тройной урон.",
                BuildSkill.SpearLunge => "Каждые 4 с: +50% длины на 1 с. Первое попадание выпада — двойное.",
                BuildSkill.BladeMomentum => "Полный оборот без попадания: +10% урона следующему удару. До +100%.",
                BuildSkill.ScytheHarvest => "Попадание дополнительно наносит урон за 3 с накопленного яда косы. Яд остаётся.",
                BuildSkill.ShieldStorage => "30% реально нанесённого отражением урона превращается в барьер. Запас до 150.",
                BuildSkill.UnarmedRam => "+50% урона столкновений. После столкновения: 40% защиты на 1 с.",
                BuildSkill.BowDouble => "Вдвое больше стрел в каждой следующей очереди.",
                BuildSkill.CrossbowPierce => "Стрела пробивает одного врага и может поразить второго. Щиты продолжают отражать её.",
                BuildSkill.StaffEcho => "Через 0,4 с — вторая волна того же радиуса с 60% урона. Без цепных повторов.",
                _ => "Отскок от стены: +25% исходного урона летящего сюрикена. До +150%."
            };
        }
    }
    [Serializable] public sealed class BuildCard { public BuildSkill skill; }
    [Serializable] public sealed class BuildOffer
    {
        public bool open;
        public int id, deadlineTick, actionTick;
        public BuildCard[] cards = Array.Empty<BuildCard>();
    }
    [Serializable] public sealed class BuildCommand
    {
        public int tick, actor, argument;
        public BuildCommandKind kind;
    }
    public struct BuildAttackSnapshot
    {
        public bool Captured;
        public uint Skills;
        public float CriticalChance, Vampirism;
        public bool Has(BuildSkill skill) => (Skills & (1u << (int)skill)) != 0;
    }
    public sealed class BuildSpark
    {
        public bool Active;
        public int Id, Kind;
        public Vector2 Position;
        public float SpawnTime, NextTime = 10;
        public uint Random;
    }
    public sealed class BuildFighter
    {
        public readonly int[] Levels = new int[BuildSkills.Count];
        public readonly float[] Adaptation = new float[6], Bleeds = new float[5], ScythePoison = new float[5];
        public readonly int[] BleedOwners = new int[5];
        public float InitialHP, PassiveBarrier, BarrierReload, ActiveBarrier, ActiveBarrierTime, StorageBarrier;
        public float AbilityCooldown, BurnTime, SuppressionTime, RamTime, Invulnerable, SparkBoost, MomentumTravel, HealFlash, CriticalFlash, DotFeedback;
        public int BurnOwner = -1, DirectHits, SwordHits, MomentumStacks, HunterKills, LungeCycle = -1;
        public bool SecondWindUsed, ResurrectionUsed, LungeUsed, OfferOpen, DeathCredited;
        public BattleAbility Ability;
        public uint Random;
        public readonly BuildSpark Spark = new BuildSpark();
        public int Level(BuildSkill skill) => Levels[(int)skill];
        public bool Has(BuildSkill skill) => Level(skill) > 0;
        public float BarrierTotal => ActiveBarrier + StorageBarrier + PassiveBarrier;
        public float HealingFactor => SuppressionTime > 0 ? .4f : 1;
    }
}
