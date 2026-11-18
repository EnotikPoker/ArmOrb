using System.Collections.Generic;
using UnityEngine;

namespace ArmOrb
{
    public sealed partial class ArenaSimulation
    {
        readonly List<ProjectileState> projectiles = new List<ProjectileState>();
        readonly List<WaveState> waves = new List<WaveState>();
        readonly Stack<ProjectileState> freeProjectiles = new Stack<ProjectileState>();
        readonly Stack<WaveState> freeWaves = new Stack<WaveState>();
        public IReadOnlyList<ProjectileState> Projectiles => projectiles;
        public IReadOnlyList<WaveState> Waves => waves;
        int nextEffectId;

        void Fire(int side, bool single = false)
        {
            var orb = Orbs[side]; var weapon = orb.Weapon;
            int count = weapon.Kind == WeaponKind.Crossbow ? weapon.ArrowCount : 1;
            for (int i = 0; i < count; i++)
            {
                float angle = (single ? weapon.BurstAngle : orb.Angle) + (count == 1 ? 0 : -30 + 60f * i / (count - 1));
                Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                var p = freeProjectiles.Count > 0 ? freeProjectiles.Pop() : new ProjectileState();
                p.Id = ++nextEffectId; p.Owner = side; p.Kind = weapon.Kind;
                p.Radius = (weapon.Kind == WeaponKind.Bow || weapon.Kind == WeaponKind.Crossbow) ? 0.06f : weapon.Kind == WeaponKind.Staff ? 0.16f : 0.18f;
                p.HalfShaft = (weapon.Kind == WeaponKind.Bow || weapon.Kind == WeaponKind.Crossbow) ? 0.14f : 0;
                Vector2 limit = Vector2.one*(Size*0.5f-p.Radius)-new Vector2(Mathf.Abs(direction.x),Mathf.Abs(direction.y))*p.HalfShaft;
                Vector2 muzzle = orb.Position + direction * (Radius + p.Radius + 0.04f);
                p.Position = p.PreviousPosition = new Vector2(Mathf.Clamp(muzzle.x, -limit.x, limit.x), Mathf.Clamp(muzzle.y, -limit.y, limit.y));
                p.Velocity = direction * weapon.ProjectileSpeed; p.Damage = weapon.Damage;
                p.Bounces = weapon.Bounces; p.BlastRadius = weapon.BlastRadius; p.WaveSpeed = weapon.WaveSpeed;
                p.Age = 0; p.Reflections = 0; p.Active = true; projectiles.Add(p);
            }
            Emit(CombatEventKind.Shot, orb.Position, side);
        }
        void Explode(ProjectileState p)
        {
            var w = freeWaves.Count > 0 ? freeWaves.Pop() : new WaveState();
            w.Id = ++nextEffectId; w.Owner = p.Owner; w.Position = p.Position;
            w.Radius = w.PreviousRadius = 0; w.MaximumRadius = p.BlastRadius;
            w.Speed = p.WaveSpeed; w.Damage = p.Damage; w.Hit = false; w.HitMask = 0; w.Active = true; waves.Add(w);
            Emit(CombatEventKind.Explosion, p.Position, p.Owner);
        }
        void TickProjectiles(float dt)
        {
            // Existing projectiles move before new shots: muzzle spawn is at this substep's end.
            for (int n = 0; n < projectiles.Count; n++)
            {
                var p = projectiles[n]; p.Age += dt;
                float remaining = dt, offset = 0; int contacts=0;
                // Each segment ends at the earliest body, shield or wall contact.
                while (p.Active && remaining > 0.00000001f && contacts++ < 8)
                {
                    Vector2 from = p.Position, to = from + p.Velocity * remaining;
                    Vector2 direction = p.Velocity.normalized;
                    Vector2 limit = Vector2.one*(Size*0.5f-p.Radius)-new Vector2(Mathf.Abs(direction.x),Mathf.Abs(direction.y))*p.HalfShaft;
                    float tx = WallTime(from.x, to.x, limit.x), ty = WallTime(from.y, to.y, limit.y);
                    float wall = Mathf.Min(tx, ty), body = float.PositiveInfinity, shield = float.PositiveInfinity;
                    int bodyTarget=-1,shieldTarget=-1;
                    for(int target=0;target<Orbs.Length;target++) if(target!=p.Owner && alive[target])
                    {
                        Vector2 bodyFrom = Vector2.Lerp(start[target], end[target], offset / dt);
                        float candidate = ProjectileCollision.Body(bodyFrom-from,end[target]-to,direction,p.HalfShaft,Radius+p.Radius);
                        if(candidate<body){body=candidate;bodyTarget=target;}
                        if(Orbs[target].Weapon.Kind!=WeaponKind.Shield)continue;
                        candidate=ProjectileCollision.Shield(from-direction*p.HalfShaft,to+direction*p.HalfShaft,p.Radius,geometry[target]);
                        if(candidate<=1 && p.HalfShaft>0)
                            candidate=Mathf.Clamp01((candidate*((to-from).magnitude+2*p.HalfShaft)-2*p.HalfShaft)/Mathf.Max(0.0000001f,(to-from).magnitude));
                        if(candidate<shield){shield=candidate;shieldTarget=target;}
                    }
                    float impact = Mathf.Min(wall, Mathf.Min(body, shield));
                    if (impact > 1) { p.Position = to; break; }
                    p.Position = Vector2.Lerp(from, to, impact);
                    if (shield <= body && shield <= wall)
                    {
                        // Transfer the projectile, retaining its kind, damage, blast radius and bounce budget.
                        int target=shieldTarget; var defender = Orbs[target]; defender.Weapon.Block(0);
                        float radians = defender.Angle * Mathf.Deg2Rad;
                        Vector2 normal = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                        Vector2 away = OrbMotion.Direction(p.Position-defender.Position,normal);
                        Vector2 outgoing = Vector2.Reflect(p.Velocity,normal);
                        if(Vector2.Dot(outgoing,away)<0) outgoing=Vector2.Reflect(p.Velocity,away);
                        p.Velocity=outgoing; p.Owner=target; p.Reflections++;
                        p.Position+=OrbMotion.Direction(outgoing,away)*0.0001f;
                        Emit(CombatEventKind.Block,p.Position,target);MarkContact(target,p.Owner);
                        offset+=remaining*impact; remaining*=1-impact;
                        continue;
                    }
                    else if (body <= wall)
                    {
                        if (p.Kind == WeaponKind.Staff) Explode(p);
                        else DamageHit(p.Owner, bodyTarget, p.Damage, p.Position);
                        p.Active = false;
                    }
                    else if (p.Kind == WeaponKind.Shuriken && p.Bounces > 0)
                    {
                        p.Bounces--;
                        if (Mathf.Abs(tx - wall) < 0.00001f) p.Velocity.x *= -1;
                        if (Mathf.Abs(ty - wall) < 0.00001f) p.Velocity.y *= -1;
                        // Move a tiny distance into the arena to avoid a zero-time wall repeat.
                        p.Position += p.Velocity.normalized * 0.000001f;
                        offset += remaining * impact; remaining *= 1 - impact;
                        continue;
                    }
                    else { if (p.Kind == WeaponKind.Staff) Explode(p); p.Active = false; }
                    break;
                }
            }
            for (int n = projectiles.Count - 1; n >= 0; n--)
                if (!projectiles[n].Active) { freeProjectiles.Push(projectiles[n]); projectiles.RemoveAt(n); }
            for (int n = 0; n < waves.Count; n++)
            {
                var w = waves[n]; float beforeRadius = w.Radius;
                w.Radius = Mathf.Min(w.MaximumRadius, w.Radius + w.Speed * dt);
                for(int target=0;target<Orbs.Length;target++) if(target!=w.Owner && alive[target] && (w.HitMask&(1<<target))==0 &&
                    ProjectileCollision.Ring(start[target]-w.Position,end[target]-w.Position,beforeRadius,w.Radius,Radius))
                { w.Hit=true;w.HitMask|=1<<target;DamageHit(w.Owner,target,w.Damage,Orbs[target].Position); }
                if (w.Radius >= w.MaximumRadius) w.Active = false;
            }
            for (int n = waves.Count - 1; n >= 0; n--)
                if (!waves[n].Active) { freeWaves.Push(waves[n]); waves.RemoveAt(n); }
            for (int side = 0; side < Orbs.Length; side++)
            {
                var weapon = Orbs[side].Weapon;
                if (!alive[side] || Orbs[side].Health.IsDead || !weapon.IsRanged) continue;
                if(weapon.IsBursting)
                {
                    weapon.BurstDelay-=dt;
                    if(weapon.BurstDelay<=0) ReleaseBowArrow(side);
                    continue;
                }
                weapon.ReloadRemaining -= dt;
                if (weapon.ReloadRemaining <= 0)
                {
                    if(weapon.Kind==WeaponKind.Bow)
                    {
                        weapon.BurstRemaining=weapon.ArrowCount; weapon.BurstAngle=Orbs[side].Angle;
                        weapon.BurstDelay=0;ReleaseBowArrow(side);
                    }
                    else { Fire(side); weapon.ReloadRemaining+=weapon.Cooldown; }
                }
            }
        }
        void ReleaseBowArrow(int side)
        {
            var weapon=Orbs[side].Weapon;Fire(side,true);weapon.BurstRemaining--;
            if(weapon.BurstRemaining==0) weapon.ReloadRemaining=weapon.Cooldown;
            else weapon.BurstDelay+=weapon.BurstInterval;
        }
        static float WallTime(float from, float to, float limit)
        {
            float d = to - from;
            if (d > 0 && to >= limit) return Mathf.Clamp01((limit - from) / d);
            if (d < 0 && to <= -limit) return Mathf.Clamp01((-limit - from) / d);
            return float.PositiveInfinity;
        }
        void DamageHit(int attacker, int targetId, float damage, Vector2 position)
        {
            var orb=Orbs[attacker];var target=Orbs[targetId];float before=target.Health.Current;
            target.Health.Damage(damage);
            if(target.Health.Current<before)
            {
                orb.Weapon.OnHit(target.Health);lastDamage=Elapsed;MarkContact(attacker,targetId);
                if(orb.Weapon.Kind==WeaponKind.Unarmed && Speed>0)
                {
                    float current=orb.Velocity.magnitude;
                    float wanted=Mathf.Min(current+orb.Weapon.Growth*orb.Weapon.SpeedUnit,orb.Weapon.UnarmedWorldLimit);
                    if(wanted>current)
                        orb.Velocity=OrbMotion.Direction(orb.Velocity,OrbMotion.Direction(incoming[attacker],orb.Position-target.Position))*wanted;
                }
            }
            Emit(CombatEventKind.Hit,position,targetId,Mathf.Min(before,damage),attacker);
        }
    }
}
