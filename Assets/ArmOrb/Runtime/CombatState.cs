using UnityEngine;

namespace ArmOrb
{
    public sealed class HealthState
    {
        public float Maximum { get; private set; }
        float current;
        double preciseHealth;
        bool precise;
        public float Current { get => precise ? (float)preciseHealth : current; private set { current = value; preciseHealth = value; } }
        public float PoisonPerSecond { get; private set; }
        public bool IsDead => precise ? preciseHealth <= 0 : Current <= 0;
        public void EnablePrecision() { preciseHealth = Current; precise = true; }
        public HealthState(float health) { Maximum = Current = Mathf.Max(1, health); }
        public void Damage(float amount) { if (precise) preciseHealth = System.Math.Max(0, preciseHealth - Mathf.Max(0, amount) * DamageMultiplier); else Current = Mathf.Max(0, Current - Mathf.Max(0, amount) * DamageMultiplier); }
        public void DangerDamage(float amount) => Current = Mathf.Max(0, Current - Mathf.Max(0, amount));
        public float DamageMultiplier = 1;
        public void Heal(float amount) { if (IsDead) return; if (precise) preciseHealth = System.Math.Min(Maximum, preciseHealth + Mathf.Max(0, amount)); else Current = Mathf.Min(Maximum, Current + Mathf.Max(0, amount)); }
        public void IncreaseMaximum(float amount) { amount = Mathf.Max(0, amount); Maximum += amount; if (!IsDead) { if (precise) preciseHealth += amount; else Current += amount; } }
        public void Revive(float fraction) { if (IsDead) Current = Maximum * Mathf.Clamp01(fraction); }
        public void AddPoison(float amount) => PoisonPerSecond += Mathf.Max(0, amount);
    }

    public sealed class WeaponState
    {
        public WeaponKind Kind { get; }
        float baseDamage;
        public float Damage { get => Kind==WeaponKind.Unarmed?Mathf.Max(1,Mathf.Floor(CurrentSpeed+0.00001f)):baseDamage; private set => baseDamage=value; }
        public float CurrentSpeed { get; private set; }
        readonly float initialUnarmedMaxSpeed;
        public float UnarmedMaxSpeed => initialUnarmedMaxSpeed+SpeedBonus;
        public float SpeedUnit { get; }
        float purchasedSpeedMultiplier = 1;
        public float UnarmedWorldLimit => UnarmedMaxSpeed*SpeedUnit*purchasedSpeedMultiplier;
        public void MeasureSpeed(Vector2 velocity) => CurrentSpeed=velocity.magnitude/(Kind==WeaponKind.Unarmed?SpeedUnit:1);
        float reach;
        public float BuildReachMultiplier = 1, GrowthMultiplier = 1;
        float growthCarry;
        public float Reach { get => reach * BuildReachMultiplier; private set => reach = value; }
        public float RotationSpeed { get; private set; }
        public float Reflection => 1;
        public float ShieldWidth { get; private set; } = 2.1f;
        public float Growth { get; private set; }
        public WeaponShapeData Shape { get; }
        readonly double rotationGrowthSquared;
        public bool IsRanged => Kind == WeaponKind.Bow || Kind == WeaponKind.Staff || Kind == WeaponKind.Shuriken || Kind == WeaponKind.Crossbow;
        public bool IsMelee => !IsRanged && Kind != WeaponKind.Unarmed;
        public float SpeedBonus { get; private set; }
        public int ArrowCount { get; private set; } = 1;
        public int Bounces { get; private set; } = 1;
        public float BlastRadius { get; private set; }
        public float Cooldown { get; }
        public float ProjectileSpeed { get; }
        public float WaveSpeed { get; }
        public float ReloadRemaining;
        public float BurstInterval { get; }
        public int BurstRemaining;
        public float BurstDelay;
        public bool IsBursting => Kind == WeaponKind.Bow && BurstRemaining > 0;
        public int Hits { get; private set; }
        public int Blocks { get; private set; }
        public WeaponState(WeaponKind kind, float damage = 1, float reach = 2.6f,
            float rotationSpeed = 300, float growth = 1, float reflection = 1, WeaponShapeDefinition shape = null,
            float cooldown = 1, float projectileSpeed = 14, float blastRadius = 1.1f, float waveSpeed = 6, float burstInterval = 0.06f, float unarmedMaxSpeed = 4, float unarmedSpeedUnit = 1.6f, WeaponShapeData geometry = null)
        {
            Kind = kind; Damage = kind == WeaponKind.Shield ? 0 : damage; Reach = reach;
            RotationSpeed = rotationSpeed; Growth = growth;
            Shape = geometry ?? new WeaponShapeData(shape ? shape : WeaponShapeDefinition.Get(kind));
            rotationGrowthSquared = 2.0 * rotationSpeed * growth + growth * growth;
            Cooldown = Mathf.Max(0.05f, cooldown); ProjectileSpeed = Mathf.Max(0.1f, projectileSpeed);
            BlastRadius = Mathf.Max(0.01f, blastRadius); WaveSpeed = Mathf.Max(0.1f, waveSpeed);
            initialUnarmedMaxSpeed=Mathf.Max(0.1f,unarmedMaxSpeed); SpeedUnit=Mathf.Max(0.1f,unarmedSpeedUnit);
            BurstInterval = Mathf.Max(0.01f, burstInterval);
            ReloadRemaining = IsRanged ? Cooldown : 0;
        }
        public void OnHit(HealthState target)
        {
            Hits++;
            switch (Kind)
            {
                case WeaponKind.Unarmed: SpeedBonus = Mathf.Min(12-initialUnarmedMaxSpeed, SpeedBonus + Growth * GrowthMultiplier); break;
                case WeaponKind.Bow:
                case WeaponKind.Crossbow:
                    if (GrowthMultiplier == 1) ArrowCount++; else { growthCarry += GrowthMultiplier; int gain = Mathf.FloorToInt(growthCarry); ArrowCount += gain; growthCarry -= gain; } break;
                case WeaponKind.Shield: ShieldWidth += Growth * GrowthMultiplier; break;
                case WeaponKind.Staff: BlastRadius += Growth * GrowthMultiplier; break;
                case WeaponKind.Shuriken:
                    if (GrowthMultiplier == 1) Bounces++; else { growthCarry += GrowthMultiplier; int gain = Mathf.FloorToInt(growthCarry); Bounces += gain; growthCarry -= gain; } break;
                case WeaponKind.Sword: Damage += Growth * GrowthMultiplier; break;
                case WeaponKind.Spear: reach += Growth * GrowthMultiplier; break;
                case WeaponKind.Blade:
                    float next = (float)System.Math.Sqrt((double)RotationSpeed * RotationSpeed + rotationGrowthSquared);
                    RotationSpeed = GrowthMultiplier == 1 ? next : RotationSpeed + (next - RotationSpeed) * GrowthMultiplier; break;
                case WeaponKind.Scythe: target.AddPoison(Growth * GrowthMultiplier); break;
            }
        }
        public void Upgrade()
        {
            switch (Kind)
            {
                case WeaponKind.Sword: Damage += 2; break;
                case WeaponKind.Spear: Reach += 0.75f; break;
                case WeaponKind.Blade: RotationSpeed *= 1.2f; break;
                case WeaponKind.Scythe: Growth += 1; break;
                case WeaponKind.Shield: ShieldWidth += 0.5f; break;
                case WeaponKind.Unarmed: purchasedSpeedMultiplier += 0.1f; break;
                case WeaponKind.Bow: case WeaponKind.Crossbow: ArrowCount += 2; break;
                case WeaponKind.Staff: BlastRadius += 0.5f; break;
                case WeaponKind.Shuriken: Bounces += 2; break;
            }
        }
        // A block alone never grows the shield. Growth is credited only after returned damage lands.
        public float Block(float incomingDamage) { Blocks++; return Mathf.Max(0, incomingDamage); }
        public string Stat => Kind switch
        {
            WeaponKind.Unarmed => $"СКОРОСТЬ {CurrentSpeed:0.#} · РАЗГОН {UnarmedMaxSpeed:0.#} · УРОН {Damage:0}",
            WeaponKind.Bow => $"ОЧЕРЕДЬ  {ArrowCount}",
            WeaponKind.Crossbow => $"ЗАЛП  {ArrowCount}",
            WeaponKind.Staff => $"РАДИУС  {BlastRadius:0.##}",
            WeaponKind.Shuriken => $"ОТСКОКИ  {Bounces}",
            WeaponKind.Sword => $"УРОН  {Damage:0.#}",
            WeaponKind.Spear => $"ДЛИНА  {Reach:0.#}r",
            WeaponKind.Blade => $"ВРАЩЕНИЕ  {RotationSpeed:0}°/с",
            WeaponKind.Scythe => $"ЯД  +{Growth:0.#}/с за удар",
            _ => $"ШИРИНА  {ShieldWidth:0.##}r"
        };
    }

    public sealed class CombatantState
    {
        public int ArmorRanks, WeaponRanks;
        public float VulnerableRemaining, ToxicRemaining;
        public Vector2 Position;
        public Vector2 Velocity;
        public float Angle;
        public float AngularTravel;
        public int RotationDirection;
        public HealthState Health { get; }
        public WeaponState Weapon { get; }
        public CombatantState(float health, WeaponState weapon) { Health = new HealthState(health); Weapon = weapon; }
    }

    public enum MatchResult { InProgress, Player, Enemy, Draw, Other }
    public enum CombatEventKind { Wall, Body, Hit, Clash, Block, Poison, Shot, Explosion, Finished, Healing, Critical, Barrier, Revive, Burning, Upgrade }
    public readonly struct CombatEvent
    {
        public readonly CombatEventKind Kind;
        public readonly Vector2 Position;
        public readonly int Side;
        public readonly int ActorId;
        public int TargetId => Side;
        public readonly float Amount;
        public CombatEvent(CombatEventKind kind, Vector2 position, int side = 0, float amount = 0, int actorId = -1)
        { Kind = kind; Position = position; Side = side; Amount = amount; ActorId = actorId; }
    }
}
