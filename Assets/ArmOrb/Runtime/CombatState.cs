using UnityEngine;

namespace ArmOrb
{
    public sealed class HealthState
    {
        public float Maximum { get; }
        public float Current { get; private set; }
        public float PoisonPerSecond { get; private set; }
        public bool IsDead => Current <= 0;
        public HealthState(float health) { Maximum = Current = Mathf.Max(1, health); }
        public void Damage(float amount) => Current = Mathf.Max(0, Current - Mathf.Max(0, amount));
        public void AddPoison(float amount) => PoisonPerSecond += Mathf.Max(0, amount);
    }

    public sealed class WeaponState
    {
        public WeaponKind Kind { get; }
        public float Damage { get; private set; }
        public float Reach { get; private set; }
        public float RotationSpeed { get; private set; }
        public float Reflection { get; private set; }
        public float Growth { get; }
        public WeaponShapeDefinition Shape { get; }
        public bool IsRanged => Kind == WeaponKind.Bow || Kind == WeaponKind.Staff || Kind == WeaponKind.Shuriken;
        public bool IsMelee => !IsRanged && Kind != WeaponKind.Unarmed;
        public float SpeedBonus { get; private set; }
        public int ArrowCount { get; private set; } = 1;
        public int Bounces { get; private set; } = 1;
        public float BlastRadius { get; private set; }
        public float Cooldown { get; }
        public float ProjectileSpeed { get; }
        public float WaveSpeed { get; }
        public float ReloadRemaining;
        public int Hits { get; private set; }
        public int Blocks { get; private set; }
        public WeaponState(WeaponKind kind, float damage = 1, float reach = 2.6f,
            float rotationSpeed = 300, float growth = 1, float reflection = 1, WeaponShapeDefinition shape = null,
            float cooldown = 1, float projectileSpeed = 14, float blastRadius = 1.1f, float waveSpeed = 6)
        {
            Kind = kind; Damage = damage; Reach = reach;
            RotationSpeed = rotationSpeed; Growth = growth; Reflection = reflection;
            Shape = shape ? shape : WeaponShapeDefinition.Get(kind);
            Cooldown = Mathf.Max(0.05f, cooldown); ProjectileSpeed = Mathf.Max(0.1f, projectileSpeed);
            BlastRadius = Mathf.Max(0.01f, blastRadius); WaveSpeed = Mathf.Max(0.1f, waveSpeed);
            ReloadRemaining = IsRanged ? Cooldown : 0;
        }
        public void OnHit(HealthState target)
        {
            Hits++;
            switch (Kind)
            {
                case WeaponKind.Unarmed: SpeedBonus += Growth; break;
                case WeaponKind.Bow: ArrowCount++; break;
                case WeaponKind.Staff: BlastRadius += Growth; break;
                case WeaponKind.Shuriken: Bounces++; break;
                case WeaponKind.Sword: Damage += Growth; break;
                case WeaponKind.Spear: Reach += Growth; break;
                case WeaponKind.Blade: RotationSpeed += Growth; break;
                case WeaponKind.Scythe: target.AddPoison(Growth); break;
            }
        }
        // Returned damage goes straight to health and cannot trigger another reflection.
        public float Block(float incomingDamage)
        {
            float returned = incomingDamage * Reflection;
            Blocks++; Reflection += Growth;
            return returned;
        }
        public string Stat => Kind switch
        {
            WeaponKind.Unarmed => $"СКОРОСТЬ  +{SpeedBonus:0.#}",
            WeaponKind.Bow => $"СТРЕЛЫ  {ArrowCount}",
            WeaponKind.Staff => $"РАДИУС  {BlastRadius:0.##}",
            WeaponKind.Shuriken => $"ОТСКОКИ  {Bounces}",
            WeaponKind.Sword => $"УРОН  {Damage:0.#}",
            WeaponKind.Spear => $"ДЛИНА  {Reach:0.#}r",
            WeaponKind.Blade => $"ВРАЩЕНИЕ  {RotationSpeed:0}°/с",
            WeaponKind.Scythe => $"ЯД  +{Growth:0.#}/с за удар",
            _ => $"ВОЗВРАТ  {Reflection * 100:0}%"
        };
    }

    public sealed class CombatantState
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Angle;
        public int RotationDirection;
        public HealthState Health { get; }
        public WeaponState Weapon { get; }
        public CombatantState(float health, WeaponState weapon) { Health = new HealthState(health); Weapon = weapon; }
    }

    public enum MatchResult { InProgress, Player, Enemy, Draw }
    public enum CombatEventKind { Wall, Body, Hit, Clash, Block, Poison, Shot, Explosion, Finished }
    public readonly struct CombatEvent
    {
        public readonly CombatEventKind Kind;
        public readonly Vector2 Position;
        public readonly int Side;
        public readonly float Amount;
        public CombatEvent(CombatEventKind kind, Vector2 position, int side = 0, float amount = 0)
        { Kind = kind; Position = position; Side = side; Amount = amount; }
    }
}
