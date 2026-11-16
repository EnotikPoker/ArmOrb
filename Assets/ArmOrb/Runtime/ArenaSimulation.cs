using System;
using UnityEngine;

namespace ArmOrb
{
    // No GameObjects or frame-time dependency: the same seed and fixed steps reproduce a fight.
    public sealed class ArenaSimulation
    {
        public CombatantState[] Orbs { get; }
        public float Elapsed { get; private set; }
        public MatchResult Result { get; private set; }
        public readonly float Size, Radius, Speed;
        public readonly int Seed;
        public event Action<CombatEvent> Event;
        readonly WeaponSegment[] first = new WeaponSegment[3], second = new WeaponSegment[3];
        readonly bool[] bodyContact = new bool[2];
        bool weaponContact, orbContact;
        float poisonClock;

        public ArenaSimulation(WeaponState player, WeaponState enemy, float playerHP = 100, float enemyHP = 100,
            int seed = 1, float size = 10, float radius = 0.55f, float speed = 3.8f)
        {
            Size = Mathf.Max(size, radius * 8); Radius = radius; Speed = speed; Seed = seed;
            Orbs = new[] { new CombatantState(playerHP, player), new CombatantState(enemyHP, enemy) };
            var random = new System.Random(seed);
            for (int i = 0; i < 2; i++)
            {
                float angle = (float)(random.NextDouble() * Math.PI * 2);
                Orbs[i].Position = new Vector2((i == 0 ? -1 : 1) * Size * 0.23f, (float)(random.NextDouble() - 0.5) * Size * 0.25f);
                Orbs[i].Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Speed;
                Orbs[i].Angle = (float)random.NextDouble() * 360;
                Orbs[i].RotationDirection = i == 0 ? 1 : -1;
            }
        }

        public void Step(float dt)
        {
            if (Result != MatchResult.InProgress || dt <= 0) return;
            // Sample tip travel at less than 0.3 orb radii, including very fast blades/long spears.
            // This is geometric substepping, independent of the rendered frame rate; no stat caps.
            float fastestTip = 0;
            for (int i = 0; i < 2; i++)
                fastestTip = Mathf.Max(fastestTip, Orbs[i].Weapon.RotationSpeed * Mathf.Deg2Rad * Radius * (Orbs[i].Weapon.Reach + 1.5f));
            int steps = Mathf.Max(1, Mathf.CeilToInt((fastestTip + Speed * 2) * dt / (Radius * 0.3f)));
            float h = dt / steps;
            for (int s = 0; s < steps && Result == MatchResult.InProgress; s++) Substep(h);
        }

        void Substep(float dt)
        {
            Elapsed += dt;
            for (int i = 0; i < 2; i++)
            {
                var orb = Orbs[i];
                if (OrbMotion.Move(orb, dt, Size * 0.5f, Radius)) Emit(CombatEventKind.Wall, orb.Position, i);
                orb.Angle = Mathf.Repeat(orb.Angle + orb.RotationDirection * orb.Weapon.RotationSpeed * dt, 360);
            }
            bool touches = OrbMotion.Resolve(Orbs[0], Orbs[1], Radius, Speed);
            if (touches && !orbContact) Emit(CombatEventKind.Body, (Orbs[0].Position + Orbs[1].Position) / 2);
            orbContact = touches;
            // Corrections can push a body against a wall; resolve the bounds immediately.
            for (int i = 0; i < 2; i++) OrbMotion.Move(Orbs[i], 0, Size * 0.5f, Radius);
            int ac = WeaponGeometry.GetSegments(Orbs[0], Radius, first);
            int bc = WeaponGeometry.GetSegments(Orbs[1], Radius, second);
            bool clash = WeaponGeometry.Clash(first, ac, second, bc);
            if (clash && !weaponContact)
            {
                bool shieldA = Orbs[0].Weapon.Kind == WeaponKind.Shield;
                bool shieldB = Orbs[1].Weapon.Kind == WeaponKind.Shield;
                if (shieldA != shieldB)
                {
                    int defender = shieldA ? 0 : 1, attacker = 1 - defender;
                    float returned = Orbs[defender].Weapon.Block(Orbs[attacker].Weapon.Damage);
                    Orbs[attacker].Health.Damage(returned);
                    Emit(CombatEventKind.Block, Orbs[attacker].Position, attacker, returned);
                }
                else Emit(CombatEventKind.Clash, (Orbs[0].Position + Orbs[1].Position) * 0.5f);
                for (int i = 0; i < 2; i++) Orbs[i].RotationDirection *= -1;
                Vector2 n = OrbMotion.Direction(Orbs[1].Position - Orbs[0].Position, Vector2.right);
                Orbs[0].Velocity = OrbMotion.Direction(Orbs[0].Velocity - n * Speed * 0.45f, -n) * Speed;
                Orbs[1].Velocity = OrbMotion.Direction(Orbs[1].Velocity + n * Speed * 0.45f, n) * Speed;
            }
            weaponContact = clash;
            bool hitA = WeaponGeometry.TouchesBody(first, ac, Orbs[1].Position, Radius);
            bool hitB = WeaponGeometry.TouchesBody(second, bc, Orbs[0].Position, Radius);
            // Both hits resolve before death detection, so simultaneous knockouts are draws.
            if (!clash && hitA && !bodyContact[0]) Hit(0);
            if (!clash && hitB && !bodyContact[1]) Hit(1);
            bodyContact[0] = hitA; bodyContact[1] = hitB;
            poisonClock += dt;
            if (poisonClock >= 1)
            {
                poisonClock -= 1;
                for (int i = 0; i < 2; i++)
                    if (Orbs[i].Health.PoisonPerSecond > 0)
                    {
                        Orbs[i].Health.Damage(Orbs[i].Health.PoisonPerSecond);
                        Emit(CombatEventKind.Poison, Orbs[i].Position, i, Orbs[i].Health.PoisonPerSecond);
                    }
            }
            if (Orbs[0].Health.IsDead || Orbs[1].Health.IsDead)
            {
                Result = Orbs[0].Health.IsDead && Orbs[1].Health.IsDead ? MatchResult.Draw
                    : Orbs[1].Health.IsDead ? MatchResult.Player : MatchResult.Enemy;
                Emit(CombatEventKind.Finished, Vector2.zero);
            }
        }
        void Hit(int attacker)
        {
            var target = Orbs[1 - attacker];
            float damage = Orbs[attacker].Weapon.Damage;
            target.Health.Damage(damage);
            Orbs[attacker].Weapon.OnHit(target.Health);
            Emit(CombatEventKind.Hit, target.Position, 1 - attacker, damage);
        }
        void Emit(CombatEventKind kind, Vector2 position, int side = 0, float amount = 0)
            => Event?.Invoke(new CombatEvent(kind, position, side, amount));
    }
}
