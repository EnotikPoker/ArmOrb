using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ArmOrb
{
    public sealed partial class GameInterface
    {
        void CharacterPreview(VisualElement parent,string skin,string classes)
        {
            var disk=Element(parent,"character-preview "+classes);disk.style.backgroundColor=game.ParticipantColor(0);
            var sprite=CosmeticCatalog.Sprite(skin);
            if(sprite!=null){var image=new Image{sprite=sprite,scaleMode=ScaleMode.ScaleToFit,pickingMode=PickingMode.Ignore};image.AddToClassList("character-image");image.tintColor=game.ParticipantColor(0);disk.Add(image);}
            Text(disk,"300","character-preview-hp");
        }
        void RunSetup(VisualElement content)
        {
            if(game.IsRestoring){Text(content,"Восстановление забега…","section-name");return;}
            var data=game.Runs?.Pending;
            if(data==null)
            {
                if(!string.IsNullOrEmpty(game.Runs?.Error))Text(content,game.Runs.Error,"error");
                Text(content,"Пять боёв до чемпиона","section-name");
                Text(content,"HP и навыки сохраняются между этапами. Разгон оружия начинается заново. Награды остаются даже после поражения.","rule");
                CharacterPreview(content,game.Collection.Data.skin,"run-character");
                Text(content,"Оружие","section-name");var grid=Element(content,"weapon-grid");
                for(int i=0;i<game.weapons.Length;i++)
                {int id=i;var b=Button(grid,"",()=>{game.RunWeapon=id;game.ConfigureRun();},"weapon-tile");b.name="run-weapon-"+i;WeaponImage(b,i,"weapon-tile-image");Text(b,game.weapons[i].displayName,"weapon-tile-name");Selected(b,i==game.RunWeapon);IgnoreChildren(b);}
                var initial=new CombatantState(300,game.weapons[game.RunWeapon].CreateState());initial.Weapon.MeasureSpeed(new Vector2(6.4f,0));
                Text(content,InterfacePresentation.WeaponStats(initial),"weapon-base-stats");
                Text(content,"Активная способность","section-name");var abilities=Element(content,"row ability-options");
                for(int i=0;i<2;i++)
                {int id=i;var b=Button(abilities,"",()=>{game.RunAbility=(BattleAbility)id;game.ConfigureRun();},"ability-option");b.name="run-ability-"+i;b.Add(new InterfaceIcon(i==0?UiSymbol.Heal:UiSymbol.Shield));Text(b,i==0?"Лечение":"Барьер","card-title");Text(b,i==0?"100 HP · 20 с":"120 урона · 20 с","small");Selected(b,i==(int)game.RunAbility);IgnoreChildren(b);}
                Text(content,"300 HP · до 2 минут на этап · без сужения","condition-line");
                Button(content,"Начать забег",game.StartRun,"primary").name="start-run";
                Text(content,"За полное прохождение — 200 монет и облик рыцаря. Вход бесплатный.","small muted setup-note");
                RunRecords(content);return;
            }
            var stages=Element(content,"row run-progress");
            for(int i=0;i<5;i++){var badge=Text(stages,(i+1).ToString(),"run-step");badge.EnableInClassList("done",i<data.completed);badge.EnableInClassList("current",i==data.stage&&data.phase!=RunPhase.Finished);}
            Text(content,$"Пройдено {data.completed}/5 · заработано {data.earned} монет","run-summary");
            if(data.phase==RunPhase.Finished)
            {
                Text(content,data.outcome==1?"Забег пройден":data.outcome==2?"Ничья":"Попытка завершена","section-name");
                Text(content,$"Время боя: {(int)data.totalSeconds/60:00}:{(int)data.totalSeconds%60:00}","rule");
                if(data.outcome==1){CharacterPreview(content,"knight","run-character");Text(content,"Рыцарь открыт в коллекции","card-title");}
                var icons=Element(content,"build-icons");RenderSavedBuild(icons,data.player);
                Button(content,"Новый забег",game.StartRun,"primary").name="new-run";
                Button(content,"Коллекция",()=>game.SelectMode(GameMode.Collection),"secondary");RunRecords(content);return;
            }
            if(data.phase==RunPhase.Battle)
            {
                Text(content,$"Этап {data.stage+1} сохранён","section-name");Text(content,$"Время: {data.battle.ticks/data.battle.frequency:0} с · {game.weapons[data.weapon].displayName}","rule");
                Button(content,"Продолжить бой",game.ResumeRun,"primary").name="continue-run";AbandonRunButton(content);return;
            }
            if(data.phase==RunPhase.RewardPending)
            {Text(content,"Награда ожидает сохранения","section-name");Button(content,"Завершить начисление",game.ResumeRun,"primary");return;}
            Text(content,$"{game.weapons[data.weapon].displayName} · {data.player.hp:0}/{data.player.Maximum:0} HP","card-title");
            var build=Element(content,"build-icons");RenderSavedBuild(build,data.player);
            if(data.player.resurrectionUsed)Text(content,"Возрождение уже использовано","small muted");
            if(data.player.secondWindUsed)Text(content,"Второе дыхание уже использовано","small muted");
            if(data.phase==RunPhase.RewardChoice)
            {
                Text(content,"Лечение или улучшение","section-name");Text(content,"Один выбор. Время не ограничено.","small muted");
                for(int i=0;i<data.rewards.Length;i++)
                {
                    int slot=i;var reward=data.rewards[i];var button=Button(content,"",()=>game.ChooseRunReward(slot),"run-reward");button.name="run-reward-"+i;
                    button.Add(reward.heal?new InterfaceIcon(UiSymbol.Heal):new InterfaceIcon(reward.skill));
                    var body=Element(button,"run-reward-body");
                    Text(body,reward.heal?"Восстановить здоровье":BuildSkills.Name(reward.skill),"card-title");
                    Text(body,reward.heal?$"{data.player.hp:0} → {Mathf.Min(data.player.Maximum,data.player.hp+data.player.Maximum*.4f):0} HP":InterfacePresentation.SkillSummary(reward.skill,data.player.levels[(int)reward.skill]+1,300,(WeaponKind)data.weapon),"small");IgnoreChildren(button);
                }
                if(data.rewards.Length==0)Button(content,"Дальше",game.SkipRunReward,"primary");
                AbandonRunButton(content);return;
            }
            Text(content,$"Этап {data.stage+1} · "+(data.stage==4?"Чемпион":data.stage==2?"Элитный противник":"Арена"),"section-name");
            var fight=RunController.StageFight(data.weapon,data.seed,data.stage);var preview=Element(content,"conditions");
            Text(preview,$"{fight.Count} участника · арена {ArmOrbGame.SizeNames[(int)fight.arena]} · 120 с","condition-title");
            for(int i=1;i<fight.Count;i++)
            {
                var row=Element(preview,"row stage-enemy");WeaponImage(row,fight.weapons[i],"preview-weapon");
                var enemy=Element(row,"stage-enemy-body");Text(enemy,game.weapons[fight.weapons[i]].displayName+$" · {fight.health[i]} HP","card-title");
                Text(enemy,EnemyBuild(fight.weapons[i],data.stage),"small muted");
            }
            Text(content,"В конце времени сравнивается доля HP. Ничья завершает попытку.","small muted setup-note");
            Button(content,"Начать этап",game.StartRunStage,"primary").name="start-run-stage";AbandonRunButton(content);
        }
        static string EnemyBuild(int weapon,int stage)
        {
            int ranks=new[]{0,1,3,3,5}[stage];if(ranks==0)return "Без начальных навыков";
            bool shield=weapon==4;return ranks==1?(shield?"Броня I":"Мощь I"):
                (shield?"Броня":"Мощь")+(ranks==5?" II":" I")+" · "+(shield?"Барьер":"Броня")+(ranks==5?" II":" I")+" · "+BuildSkills.Name(BuildSkills.Special((WeaponKind)weapon));
        }
        void AbandonRunButton(VisualElement content)
        {
            Button(content,"Завершить попытку",()=>
            {
                var card=Dialog("Завершить забег?","Заработанные монеты останутся, продолжить эту попытку будет нельзя.");
                Button(card,"Продолжить забег",CloseOverlay,"primary");Button(card,"Завершить",game.AbandonRun,"secondary");
            },"text-button").name="abandon-run";
        }
        void RenderSavedBuild(VisualElement parent,RunFighterState state)
        {
            for(int i=0;i<BuildSkills.Count;i++)if(state.levels[i]>0)
            {var skill=(BuildSkill)i;int level=state.levels[i];var button=Button(parent,"",()=>{var card=Dialog(BuildSkills.Name(skill),BuildSkills.Description(skill,level,300));Button(card,"Закрыть",CloseOverlay,"primary");},"build-icon");button.Add(new InterfaceIcon(skill));Text(button,level.ToString(),"build-icon-level");IgnoreChildren(button);}
        }
        void RunRecords(VisualElement parent)
        {
            var records=game.Collection.Data.runs;Text(parent,"Рекорды","section-name");
            Text(parent,$"Попыток {records.Count} · побед {records.Count(r=>r.won)}","small muted");
            for(int i=0;i<game.weapons.Length;i++)
            {
                int weapon=i;var entries=records.Where(r=>r.weapon==weapon).ToArray();if(entries.Length==0)continue;
                int best=entries.Max(r=>r.stage);var wins=entries.Where(r=>r.won).ToArray();float seconds=wins.Length==0?0:wins.Min(r=>r.seconds);
                Text(parent,game.weapons[i].displayName+$" · {best}/5"+(wins.Length>0?$" · {(int)seconds/60:00}:{(int)seconds%60:00}":""),"record-line");
            }
        }
        public void ShowRunStageResult()
        {
            RefreshFight();var d=game.Run.Data;bool won=d.completed==d.stage+1;
            var card=Dialog(won?(d.stage==4?"Забег пройден":"Этап пройден"):d.outcome==2?"Ничья":"Поражение");
            Text(card,$"Этап {d.stage+1}/5 · время {game.Simulation.Elapsed:0.0} с","rule");
            Text(card,$"Заработано за забег: {d.earned} монет","card-title");
            if(won)Text(card,$"Награда этапа: +{CollectionProfile.StageCoins(d.stage)} монет"+(d.stage==4?" · рыцарь открыт":""),"rule");
            else Text(card,"Награды пройденных этапов сохранены.","rule");
            Button(card,won&&d.stage<4?"Выбрать награду":"Итоги",game.BackToSetup,"primary");
        }
        void CollectionSetup(VisualElement content)
        {
            Text(content,$"{game.Collection.Data.coins:N0} монет","collection-balance");
            Text(content,"Монеты зарабатываются в забегах. Оформление не меняет силу и хитбоксы.","small muted setup-note");
            if(!string.IsNullOrEmpty(game.Collection.Error))Text(content,game.Collection.Error,"error");
            CharacterPreview(content,game.Collection.Data.skin,"collection-current");
            Text(content,"Персонажи","section-name");var skins=Element(content,"collection-grid");
            foreach(var item in CosmeticCatalog.Items.Where(i=>!i.Trail))CosmeticCard(skins,item);
            Text(content,"Следы","section-name");var trails=Element(content,"collection-grid");
            foreach(var item in CosmeticCatalog.Items.Where(i=>i.Trail))CosmeticCard(trails,item);
        }
        void CosmeticCard(VisualElement parent,CosmeticItem item)
        {
            var card=Element(parent,"cosmetic-card");bool owned=game.Collection.Owns(item.Id),equipped=(item.Trail?game.Collection.Data.trail:game.Collection.Data.skin)==item.Id;
            if(item.Trail){var preview=new CosmeticTrailPreview(item.Id);card.Add(preview);}else CharacterPreview(card,item.Id,"");
            Text(card,item.Name,"cosmetic-name");
            Text(card,owned?"Открыт":item.Price<0?"Первое полное прохождение":$"{item.Price} монет","cosmetic-price");
            if(owned){var button=Button(card,equipped?"Выбрано":"Выбрать",()=>game.EquipCosmetic(item.Id),"cosmetic-action");button.name="equip-"+item.Id;button.SetEnabled(!equipped);Selected(card,equipped);}
            else if(item.Price>0)
            {
                var button=Button(card,"Купить",()=>
                {
                    var dialog=Dialog(item.Name,$"Купить за {item.Price} монет?");
                    if(!item.Trail)CharacterPreview(dialog,item.Id,"");
                    Button(dialog,"Купить",()=>game.PurchaseCosmetic(item.Id),"primary").name="confirm-buy-"+item.Id;
                    Button(dialog,"Отмена",CloseOverlay,"secondary");
                },"cosmetic-action");button.name="buy-"+item.Id;button.SetEnabled(game.Collection.Data.coins>=item.Price);
            }
        }
    }
    public sealed class CosmeticTrailPreview : VisualElement
    {
        readonly string kind;
        public CosmeticTrailPreview(string kind){this.kind=kind;AddToClassList("cosmetic-trail-preview");generateVisualContent+=Paint;}
        void Paint(MeshGenerationContext context)
        {
            var p=context.painter2D;float cx=contentRect.width*.72f,cy=contentRect.height*.5f;
            if(kind!="none")for(int i=0;i<6;i++)
            {
                var center=new Vector2(cx-18-i*13,cy);float r=kind=="dots"?5:7;
                p.BeginPath();if(kind=="dots")p.Arc(center,r,0,360);else
                {for(int n=0;n<8;n++){float a=n*Mathf.PI/4,rad=n%2==0?r:kind=="stars"?2:4;var v=center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*rad;if(n==0)p.MoveTo(v);else p.LineTo(v);}p.ClosePath();}
                p.fillColor=new Color(.45f,.7f,1,Mathf.Lerp(.6f,.1f,i/5f));p.Fill();
            }
            p.BeginPath();p.Arc(new Vector2(cx,cy),13,0,360);p.fillColor=new Color32(255,98,106,255);p.Fill();
        }
    }
}
