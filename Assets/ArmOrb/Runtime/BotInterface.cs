using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ArmOrb
{
    public sealed partial class GameInterface
    {
        Label skillStatus, choiceCountdown, skillNotice, buildDetail;
        VisualElement choiceArea, choiceRow, buildArea, buildIcons;
        BuildSparkView sparkView;
        Button abilityButton;
        readonly List<Button> skillButtons = new List<Button>();
        readonly List<Label> autoLabels = new List<Label>();
        int visibleOfferId=-1, visibleBuildCount=-1;
        public void CancelMiniGesture() { }
        void ResetBuildUi()
        {sparkView=null;abilityButton=null;choiceArea=null;buildArea=null;skillStatus=null;visibleOfferId=-1;visibleBuildCount=-1;}
        void BotSetup(VisualElement content)
        {
            if(game.IsRestoring){Text(content,"Восстановление битвы…","section-name");return;}
            if(!string.IsNullOrEmpty(game.BotStore.Error))Text(content,game.BotStore.Error,"rule");
            var pending=game.BotStore.Pending;
            if(pending!=null)
            {
                Text(content,"Незавершённая битва","section-name");
                Text(content,$"{BuildSkills.VariantNames[(int)pending.variant]} · {pending.fight.Count-1} ботов · {BotMatchController.DifficultyNames[(int)pending.difficulty]}","rule");
                Text(content,$"Время: {pending.ticks/(float)pending.frequency:0} с","rule");
                Button(content,"Продолжить битву",game.ResumeBots,"primary").name="continue-bots";
                Button(content,"Завершить попытку",game.DiscardBots,"secondary").name="discard-bots";return;
            }
            Text(content,"Битва с ботами","section-name");
            Text(content,"Собирай улучшения и следи за боем. Первый выбор — до старта, следующие — каждые 20 секунд.","rule");
            var names=new List<string>();foreach(var w in game.weapons)names.Add(w.displayName);
            var weapon=new DropdownField("Твоё оружие",names,game.BotWeapon);weapon.AddToClassList("wide-field");weapon.name="bot-player-weapon";content.Add(weapon);
            weapon.RegisterValueChangedCallback(_=>{game.BotWeapon=weapon.index;game.ConfigureBots();});
            var variant=new DropdownField("Вариант",new List<string>(BuildSkills.VariantNames),(int)game.BotVariant);variant.AddToClassList("wide-field");variant.name="bot-variant";content.Add(variant);
            variant.RegisterValueChangedCallback(_=>{game.BotVariant=(BattleVariant)variant.index;game.ConfigureBots();});
            if(game.BotVariant==BattleVariant.Ability)
            {
                var ability=new DropdownField("Способность",new List<string>(BuildSkills.AbilityNames),(int)game.BotAbility);ability.AddToClassList("wide-field");ability.name="bot-ability";content.Add(ability);
                ability.RegisterValueChangedCallback(_=>{game.BotAbility=(BattleAbility)ability.index;game.ConfigureBots();});
                Text(content,"Одна кнопка под ареной. Перезарядка 20 с. Барьер действует до 5 с.","small muted reward-description");
            }
            else Text(content,game.BotVariant==BattleVariant.Sparks?"Нажимай на искры в арене: зелёная +30 HP, оранжевая +40% урона на 6 с.":"Без дополнительных действий. Только бой и выбор улучшений.","small muted reward-description");
            var count=Element(content,"row count-control");Text(count,"Боты","setting-label");
            Button(count,"−",()=>{game.BotCount--;game.ConfigureBots();},"count-button").SetEnabled(game.BotCount>1);
            Text(count,game.BotCount.ToString(),"timer");Button(count,"+",()=>{game.BotCount++;game.ConfigureBots();},"count-button").SetEnabled(game.BotCount<4);
            var difficulty=new DropdownField("Сложность",new List<string>(BotMatchController.DifficultyNames),(int)game.BotLevel);difficulty.AddToClassList("wide-field");difficulty.name="bot-difficulty";content.Add(difficulty);
            difficulty.RegisterValueChangedCallback(_=>{game.BotLevel=(BotDifficulty)difficulty.index;game.ConfigureBots();});
            var preview=Element(content,"conditions");Text(preview,$"300 HP каждому · арена {8+2*(game.BotCount-1)} · ×1","condition-title");
            Text(preview,"Без сужения · до 5 минут · затем победа по доле HP","small muted");
            for(int i=0;i<game.BotOffer.Count;i++)Text(preview,$"#{i+1} {game.weapons[game.BotOffer.weapons[i]].displayName}"+(i==0?" · ты":""),"bot-preview-line").style.color=game.ParticipantColor(i)*new Color(.75f,.75f,.75f,1);
            Button(content,"Другой состав",()=>game.ConfigureBots(true),"secondary").name="reroll-bots";
            Button(content,"Начать битву",game.StartBots,"primary").name="start-bots";
            Text(content,"Боты получают улучшения на тех же этапах. Навыки действуют до конца матча. Монеты не расходуются.","small muted reward-description");
        }
        void ShowBotFight()
        {
            NewPage();hp.Clear();stats.Clear();reload.Clear();app.AddToClassList("bot-fight");app.AddToClassList("build-fight");
            var header=Element(app,"row header");Button(header,"Меню",game.BackToSetup,"header-button").name="fight-menu";
            timer=Text(header,"00:00","timer");Button(header,"Пауза",game.TogglePause,"header-button").name="pause";
            var participants=Element(app,"fight-participants");
            for(int i=0;i<game.CurrentFight.Count;i++)
            {
                var cell=Element(participants,"fight-participant");cell.style.borderLeftColor=game.ParticipantColor(i);
                if(i==0)cell.AddToClassList("focus-participant");
                Text(cell,$"#{i+1} {game.weapons[game.CurrentFight.weapons[i]].displayName}"+(i==0?" · ты":""),"fighter-label");
                hp.Add(Text(cell,"","fight-hp"));var fill=Element(Element(cell,"reload-track"),"reload-fill");fill.style.backgroundColor=game.ParticipantColor(i);reload.Add(fill);
            }
            var holder=Element(app,"arena-holder bot-arena-holder");
            var field=new Image{image=game.ArenaTexture,scaleMode=ScaleMode.StretchToFill,pickingMode=PickingMode.Ignore,name="arena"};field.AddToClassList("arena");holder.Add(field);
            sparkView=new BuildSparkView(game);field.Add(sparkView);
            app.RegisterCallback<GeometryChangedEvent>(e=>{float h=Mathf.Min(e.newRect.width,e.newRect.height*.49f);if(Mathf.Abs(holder.resolvedStyle.height-h)>1)holder.style.height=h;});
            holder.RegisterCallback<GeometryChangedEvent>(e=>{float size=Mathf.Max(1,Mathf.Min(e.newRect.width,e.newRect.height));field.style.width=size;field.style.height=size;});
            skillStatus=Text(app,"","build-status");
            danger=Text(app,"","danger-label");danger.style.display=DisplayStyle.None;
            for(int i=0;i<game.CurrentFight.Count;i++){var label=Text(app,"","fighter-stat");label.style.display=DisplayStyle.None;stats.Add(label);}
            skillNotice=Text(app,"","skill-notice");
            var panel=Element(app,"build-panel");
            choiceArea=Element(panel,"choice-area");choiceCountdown=Text(choiceArea,"","choice-countdown");choiceCountdown.name="skill-countdown";
            choiceRow=Element(choiceArea,"row choice-row");
            buildArea=Element(panel,"build-area");Text(buildArea,"Твои улучшения","mini-title");
            var scroll=new ScrollView(ScrollViewMode.Vertical);scroll.AddToClassList("build-scroll");buildArea.Add(scroll);
            buildIcons=Element(scroll,"build-icons");buildDetail=Text(buildArea,"Нажми на значок, чтобы прочитать эффект.","build-detail");
            if(game.BotMatch.Data.variant==BattleVariant.Ability)
            {abilityButton=Button(app,"",game.UseBotAbility,"ability-button");abilityButton.name="use-ability";}
            else Text(app,game.BotMatch.Data.variant==BattleVariant.Sparks?"Искры: зелёная — лечение · оранжевая — урон":"Улучшения остаются до конца боя","small muted build-footer");
            RefreshFight();
        }
        void BuildSkillCards()
        {
            choiceRow.Clear();skillButtons.Clear();autoLabels.Clear();
            var match=game.BotMatch;var offer=match.Data.offers[0];visibleOfferId=offer.id;var b=game.Simulation.Builds[0];
            for(int i=0;i<offer.cards.Length;i++)
            {
                int slot=i;var skill=offer.cards[i].skill;int level=b.Level(skill)+1;
                var button=Button(choiceRow,"",()=>game.ChooseBotSkill(slot),"skill-card");button.name="skill-card-"+i;skillButtons.Add(button);
                var content=Element(button,"skill-content");content.pickingMode=PickingMode.Ignore;
                Text(content,BuildSkills.Name(skill),"skill-title");
                Text(content,BuildSkills.Description(skill,level,b.InitialHP,game.Simulation.Orbs[0].Weapon.Kind),"skill-description");
                Text(content,BuildSkills.MaxLevel(skill)>1?$"Уровень {level}/{BuildSkills.MaxLevel(skill)}":"Особый навык","skill-level");
                autoLabels.Add(Text(content,"","skill-auto"));
                foreach(var child in content.Query<Label>().ToList())child.pickingMode=PickingMode.Ignore;
            }
        }
        void RefreshBuildFight()
        {
            if(!game.IsBotFight||choiceArea==null)return;
            var match=game.BotMatch;bool choosing=match.Data.offers[0].open;var b=game.Simulation.Builds[0];
            choiceArea.style.display=choosing?DisplayStyle.Flex:DisplayStyle.None;buildArea.style.display=choosing?DisplayStyle.None:DisplayStyle.Flex;
            skillStatus.text=!match.Data.started?"Первый навык · бой начнётся после выбора":$"Следующий выбор через {Mathf.Max(0,Mathf.CeilToInt(match.NextOfferSeconds-game.Simulation.Elapsed))} с · лимит 5:00";
            skillNotice.text=match.Notice;
            int total=0;foreach(int level in b.Levels)total+=level;
            if(total!=visibleBuildCount)
            {
                visibleBuildCount=total;buildIcons.Clear();
                for(int i=0;i<BuildSkills.Count;i++)if(b.Levels[i]>0)
                {
                    var skill=(BuildSkill)i;int level=b.Levels[i];
                    var icon=Button(buildIcons,$"{BuildSkills.Icon(skill)} {level}",()=>{buildDetail.text=BuildSkills.Name(skill)+": "+BuildSkills.Description(skill,level,b.InitialHP,game.Simulation.Orbs[0].Weapon.Kind);},"build-icon");
                    icon.tooltip=BuildSkills.Name(skill);icon.name="build-icon-"+i;
                }
            }
            if(abilityButton!=null)
            {
                string name=b.Ability==BattleAbility.Heal?"Лечение · +100 HP":"Барьер · 120";
                abilityButton.text=b.AbilityCooldown>.01f?$"{name} · {Mathf.CeilToInt(b.AbilityCooldown)} с":name+" · готово";
                abilityButton.SetEnabled(!game.IsPaused&&match.Active&&match.Data.started&&b.AbilityCooldown<=.00001f&&
                    (b.Ability==BattleAbility.Barrier||game.Simulation.Orbs[0].Health.Current<game.Simulation.Orbs[0].Health.Maximum));
            }
            sparkView?.MarkDirtyRepaint();
            if(!choosing)return;
            if(visibleOfferId!=match.Data.offers[0].id)BuildSkillCards();
            choiceCountdown.text=$"Выбери улучшение · {Mathf.CeilToInt(match.ChoiceRemaining)} с";
            int automatic=match.AutoChoice();
            for(int i=0;i<skillButtons.Count;i++)
            {
                skillButtons[i].SetEnabled(!game.IsPaused&&match.CanChoose(0,i));
                autoLabels[i].text=i==automatic?"Автовыбор":"";skillButtons[i].EnableInClassList("auto-skill",i==automatic);
            }
        }
        public void ShowBotResult()
        {
            RefreshFight();int outcome=game.BotMatch.Data.outcome;bool limit=game.Simulation.Elapsed>=299.99f;
            var card=Dialog(outcome==1?"Победа":outcome==2?"Ничья":"Поражение",limit?"Пять минут. Результат по доле оставшегося HP.":outcome==0?"Твой шар выбыл.":null);
            Text(card,$"Время: {game.Simulation.Elapsed:0.0} с · {BuildSkills.VariantNames[(int)game.BotMatch.Data.variant]}","rule");
            Button(card,"Новая битва",()=>{game.BackToSetup();game.ConfigureBots(true);},"primary").name="new-bot-battle";
            Button(card,"В меню",game.BackToSetup,"secondary");
        }
    }
    public sealed class BuildSparkView : VisualElement
    {
        readonly ArmOrbGame game;
        public BuildSparkView(ArmOrbGame game)
        {
            this.game=game;name="spark-layer";style.position=Position.Absolute;style.left=style.right=style.top=style.bottom=0;
            generateVisualContent+=Paint;RegisterCallback<PointerDownEvent>(Down);
        }
        Vector2 Point(BuildSpark spark) => new Vector2((.5f+spark.Position.x/(game.Simulation.Size*1.05f))*contentRect.width,(.5f-spark.Position.y/(game.Simulation.Size*1.05f))*contentRect.height);
        void Down(PointerDownEvent e)
        {
            if(game.IsPaused||!game.IsBotFight||!game.BotMatch.Data.started||game.BotMatch.Data.variant!=BattleVariant.Sparks)return;
            var spark=game.Simulation.Builds[0].Spark;
            // 80 panel units keeps the tap target comfortable on the portrait reference layout.
            if(spark.Active&&Vector2.Distance(e.localPosition,Point(spark))<=40){game.CollectBotSpark(spark.Id);e.StopPropagation();}
        }
        void Paint(MeshGenerationContext context)
        {
            if(!game.IsBotFight||game.Simulation?.Builds==null||game.BotMatch.Data.variant!=BattleVariant.Sparks)return;
            var spark=game.Simulation.Builds[0].Spark;if(!spark.Active)return;
            var painter=context.painter2D;var point=Point(spark);Color color=spark.Kind==0?new Color32(20,160,100,255):new Color32(230,128,25,255);
            painter.BeginPath();painter.Arc(point,18,0,360);painter.fillColor=new Color(1,1,1,.9f);painter.Fill();painter.strokeColor=color;painter.lineWidth=3;painter.Stroke();
            painter.BeginPath();painter.MoveTo(point+new Vector2(0,-10));painter.LineTo(point+new Vector2(8,0));painter.LineTo(point+new Vector2(0,10));painter.LineTo(point+new Vector2(-8,0));painter.ClosePath();painter.fillColor=color;painter.Fill();
            float fraction=Mathf.Clamp01(1-(game.Simulation.Elapsed-spark.SpawnTime)/4);
            painter.BeginPath();painter.Arc(point,25,-90,-90+360*fraction);painter.strokeColor=color;painter.lineWidth=2;painter.Stroke();
        }
    }
}
