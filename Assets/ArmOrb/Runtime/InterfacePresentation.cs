using UnityEngine;

namespace ArmOrb
{
    // Read-only presentation. Never rounds or feeds displayed values back into the simulation.
    public static class InterfacePresentation
    {
        public static float DamageMultiplier(CombatantState orb,BuildFighter build)
        {
            if(build==null)return 1;
            return 1+BuildSkills.Value(build.Level(BuildSkill.Power),.4f,.7f,1)+
                (build.Has(BuildSkill.Rage)?.6f*(1-orb.Health.Current/orb.Health.Maximum):0)+
                (build.Has(BuildSkill.Overload)?.7f:0)+.2f*build.HunterKills+(build.SparkBoost>0?.4f:0);
        }
        public static string WeaponStats(CombatantState orb,BuildFighter build=null)
        {
            var w=orb.Weapon;float damage=w.Damage*DamageMultiplier(orb,build);
            string hit=$"Урон {damage:0.#}";
            return w.Kind switch {
                WeaponKind.Sword=>hit,
                WeaponKind.Spear=>$"Длина {w.Reach:0.##}r · {hit}",
                WeaponKind.Blade=>$"Вращение {w.RotationSpeed:0}°/с · {hit}",
                WeaponKind.Scythe=>$"Яд +{w.Growth*w.GrowthMultiplier:0.##}/с за удар · {hit}",
                WeaponKind.Shield=>$"Ширина {w.ShieldWidth:0.##}r · Возврат 100%",
                WeaponKind.Unarmed=>$"Скорость {w.CurrentSpeed:0.#} · Разгон {w.UnarmedMaxSpeed:0.#} · {hit}",
                WeaponKind.Bow=>$"След. очередь {w.ArrowCount*(build!=null&&build.Has(BuildSkill.BowDouble)?2:1)} · Стрела {damage:0.#}",
                WeaponKind.Crossbow=>$"След. залп {w.ArrowCount} · Стрела {damage:0.#}",
                WeaponKind.Staff=>$"Радиус {w.BlastRadius:0.##} · Волна {damage:0.#}",
                _=>$"Отскоки {w.Bounces} · {hit}"
            };
        }
        public static float GrowthValue(WeaponState w,BuildFighter b=null) => w.Kind switch {
            WeaponKind.Sword=>w.Damage, WeaponKind.Spear=>w.Reach, WeaponKind.Blade=>w.RotationSpeed,
            WeaponKind.Scythe=>w.Growth*w.GrowthMultiplier,WeaponKind.Shield=>w.ShieldWidth,WeaponKind.Unarmed=>w.UnarmedMaxSpeed,
            WeaponKind.Bow=>w.ArrowCount*(b!=null&&b.Has(BuildSkill.BowDouble)?2:1),WeaponKind.Crossbow=>w.ArrowCount,
            WeaponKind.Staff=>w.BlastRadius,_=>w.Bounces };
        public static string CombatDetails(CombatantState orb,BuildFighter b)
        {
            if(b==null)return "Урон до защиты цели. Снаряды сохраняют параметры на момент выпуска.";
            var lines=new System.Text.StringBuilder("Урон выше — до защиты цели, без условных срабатываний.");
            if(b.Has(BuildSkill.Critical))lines.Append($"\nКрит: {BuildSkills.Value(b.Level(BuildSkill.Critical),25,35,45):0}% · ×2.");
            if(b.Has(BuildSkill.Execution))lines.Append("\nДобивание: +60% базового урона, когда у врага <30% HP.");
            if(b.Has(BuildSkill.Rhythm))lines.Append($"\nБоевой ритм: {(b.DirectHits%4)+1}/4 · четвёртый удар ×2.");
            if(b.Has(BuildSkill.SwordHeavy))lines.Append($"\nТяжёлый удар: {(b.SwordHits%4)+1}/4 · четвёртый удар ×3.");
            if(b.Has(BuildSkill.BladeMomentum))lines.Append($"\nРаскрутка: следующий удар +{b.MomentumStacks*10}%.");
            if(b.Has(BuildSkill.SpearLunge))lines.Append("\nВыпад: первое попадание во время удлинения ×2.");
            if(b.Has(BuildSkill.UnarmedRam))lines.Append("\nТаран: столкновение ×1,5.");
            if(b.Has(BuildSkill.ShurikenRicochet))lines.Append("\nРикошеты усиливают летящий сюрикен, до +150%.");
            if(b.Has(BuildSkill.StaffEcho))lines.Append("\nПовторная волна: 60% урона, через 0,4 с.");
            float reduction=BuildSkills.Value(b.Level(BuildSkill.Armor),.25f,.35f,.45f),factor=1-reduction;
            int layers=0;foreach(float timer in b.Adaptation)if(timer>0)layers++;
            factor*=1-layers*.05f;if(b.RamTime>0)factor*=.6f;
            lines.Append($"\nЗащита {Mathf.Min(.6f,1-factor)*100:0}%"+(b.Has(BuildSkill.Overload)?" · Перегрузка: входящий урон ×1,25":"")+".");
            if(b.BurnTime>0)lines.Append($"\nОжог: {b.BurnTime:0.#} с.");
            if(b.SuppressionTime>0)lines.Append($"\nЛечение −60%: {b.SuppressionTime:0.#} с.");
            int bleed=0;foreach(float time in b.Bleeds)if(time>0)bleed++;
            if(bleed>0)lines.Append($"\nКровотечение: {bleed*2} HP/с.");
            if(orb.Health.PoisonPerSecond>0)lines.Append($"\nЯд: {orb.Health.PoisonPerSecond:0.#} HP/с.");
            if(b.Has(BuildSkill.SecondWind))lines.Append(b.SecondWindUsed?"\nВторое дыхание использовано.":"\nВторое дыхание готово.");
            if(b.Has(BuildSkill.Resurrection))lines.Append(b.ResurrectionUsed?"\nВозрождение использовано.":"\nВозрождение готово.");
            lines.Append("\nВыпущенные снаряды сохраняют прежние параметры.");return lines.ToString();
        }
        static string Change(int level,float a,float b,float c,string suffix)
        {float next=BuildSkills.Value(level,a,b,c);return level>1?$"{BuildSkills.Value(level-1,a,b,c):0.##} → {next:0.##}{suffix}":$"{next:0.##}{suffix}";}
        public static string SkillSummary(BuildSkill skill,int level,float initialHP=300,WeaponKind weapon=WeaponKind.Sword)
        {
            return skill switch {
                BuildSkill.Vitality=>$"Макс. HP {initialHP+BuildSkills.Value(level-1,150,250,350):0} → {initialHP+BuildSkills.Value(level,150,250,350):0}",
                BuildSkill.Regeneration=>"Восстановление "+Change(level,4,6,8," HP/с"),
                BuildSkill.SecondWind=>"Один раз +120 HP при здоровье ≤25%",
                BuildSkill.Barrier=>"Барьер "+Change(level,80,120,160,"")+" · возврат через 15 с",
                BuildSkill.Vampirism=>"Лечение от прямого урона: "+Change(level,15,20,25,"%"),
                BuildSkill.Armor=>"Снижение урона: "+Change(level,25,35,45,"%"),
                BuildSkill.Adaptation=>"Удар по тебе: +5% защиты на 3 с · до 6 слоёв",
                BuildSkill.Resurrection=>"Одно возрождение с 35% HP и защитой на 1 с",
                BuildSkill.Power=>"Прямой урон +"+Change(level,40,70,100,"%"),
                BuildSkill.Critical=>"Шанс двойного урона: "+Change(level,25,35,45,"%"),
                BuildSkill.Rage=>"До +60% урона по мере потери HP",
                BuildSkill.Execution=>"+60% урона против врага с HP <30%",
                BuildSkill.Growth=>"Прирост за попадание +"+Change(level,50,75,100,"%"),
                BuildSkill.Overload=>"Урон +70% · получаемый урон +25%",
                BuildSkill.Rhythm=>"Каждое четвёртое прямое попадание: ×2",
                BuildSkill.Hunter=>weapon==WeaponKind.Shield?"Убийство: +100 HP":"Убийство: +100 HP и +20% урона до конца боя",
                BuildSkill.Burn=>"Удар: ожог 5 HP/с на 4 с",
                BuildSkill.Bleed=>"Удар: 2 HP/с на 5 с · до 5 слоёв",
                BuildSkill.Suppression=>"Удар: лечение врага −60% на 5 с",
                BuildSkill.Pierce=>"Игнорирует половину процентной защиты",
                BuildSkill.SwordHeavy=>"Каждое четвёртое попадание мечом: ×3",
                BuildSkill.SpearLunge=>"Каждые 4 с: длина +50% на 1 с, первый удар ×2",
                BuildSkill.BladeMomentum=>"Оборот без попадания: +10% следующему удару",
                BuildSkill.ScytheHarvest=>"Удар дополнительно наносит урон за 3 с яда",
                BuildSkill.ShieldStorage=>"30% отражённого урона в барьер · до 150",
                BuildSkill.UnarmedRam=>"Столкновение: урон +50%, защита 40% на 1 с",
                BuildSkill.BowDouble=>"Вдвое больше стрел в следующей очереди",
                BuildSkill.CrossbowPierce=>"Каждая стрела может поразить двух врагов",
                BuildSkill.StaffEcho=>"Вторая волна через 0,4 с · 60% урона",
                _=>"Отскок: урон +25%, до +150%"
            };
        }
        public static float BotArenaSize(float width,float height,int participants)
        {
            // Reserve the same space for a build, inspection and three skill rows.
            float header=participants>4?264:204;
            return Mathf.Max(100,Mathf.Min(width,height-header-658));
        }
    }
}
