using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ArmOrb
{
    public sealed partial class GameInterface
    {
        Label skillStatus,choiceCountdown,skillNotice,buildDetail,ownWeaponStats,inspectionTitle,inspectionStats,inspectionEffects,abilityTitle,abilityHint;
        VisualElement choiceArea,choiceRow,buildArea,buildIcons,choiceProgress,inspectionArea,inspectionIcons,ownWeaponPanel;
        BuildSparkView sparkView;
        Button abilityButton;
        InterfaceIcon abilityIcon;
        readonly List<Button> skillButtons=new List<Button>();
        readonly List<Label> autoLabels=new List<Label>();
        int visibleOfferId=-1,visibleBuildCount=-1,inspected=-1,inspectedBuildCount=-1;
        float previousGrowth=-1,statsFlashUntil,nextDetailsRefresh;
        public void CancelMiniGesture() { }
        void ResetBuildUi()
        {
            sparkView=null;abilityButton=null;choiceArea=null;buildArea=null;skillStatus=null;ownWeaponStats=null;
            visibleOfferId=-1;visibleBuildCount=-1;inspected=-1;inspectedBuildCount=-1;previousGrowth=-1;nextDetailsRefresh=0;
        }
        void BotSetup(VisualElement content)
        {
            if(game.IsRestoring){Text(content,"Восстановление битвы…","section-name");return;}
            if(!string.IsNullOrEmpty(game.BotStore.Error))Text(content,game.BotStore.Error,"rule");
            var pending=game.BotStore.Pending;
            if(pending!=null)
            {
                var saved=Element(content,"conditions saved-battle");Text(saved,"Битва сохранена","section-name");
                Text(saved,$"{pending.fight.Count-1} ботов · {BotMatchController.DifficultyNames[(int)pending.difficulty]} · {pending.ticks/pending.frequency/60:00}:{pending.ticks/pending.frequency%60:00}","rule");
                Button(content,"Продолжить битву",game.ResumeBots,"primary").name="continue-bots";
                Button(content,"Завершить попытку",game.DiscardBots,"secondary").name="discard-bots";return;
            }
            Text(content,"Твоё оружие","section-name");
            var grid=Element(content,"weapon-grid");
            for(int i=0;i<game.weapons.Length;i++)
            {
                int weapon=i;var button=Button(grid,"",()=>{game.BotWeapon=weapon;game.ConfigureBots();},"weapon-tile");button.name="bot-weapon-"+i;
                WeaponImage(button,i,"weapon-tile-image");Text(button,game.weapons[i].displayName,"weapon-tile-name");Selected(button,i==game.BotWeapon);IgnoreChildren(button);
            }
            var selected=game.weapons[game.BotWeapon];var weaponInfo=Element(content,"weapon-info");
            Text(weaponInfo,selected.displayName,"card-title");
            var state=new CombatantState(300,selected.CreateState());state.Weapon.MeasureSpeed(new Vector2(6.4f,0));
            Text(weaponInfo,InterfacePresentation.WeaponStats(state),"weapon-base-stats");Text(weaponInfo,selected.description,"small muted");
            Text(content,"Активная способность","section-name");
            var abilities=Element(content,"row ability-options");
            for(int i=0;i<2;i++)
            {
                int id=i;var button=Button(abilities,"",()=>{game.BotAbility=(BattleAbility)id;game.ConfigureBots();},"ability-option");button.name="bot-ability-"+i;
                button.Add(new InterfaceIcon(i==0?UiSymbol.Heal:UiSymbol.Shield));Text(button,i==0?"Лечение":"Барьер","card-title");
                Text(button,i==0?"+100 HP":"120 урона · до 5 с","small");Text(button,"Перезарядка 20 с","small muted");Selected(button,i==(int)game.BotAbility);IgnoreChildren(button);
            }
            var controls=Element(content,"conditions bot-controls");var count=Element(controls,"row count-control");Text(count,"Противники","setting-label");
            Button(count,"−",()=>{game.BotCount--;game.ConfigureBots();},"count-button").SetEnabled(game.BotCount>1);
            Text(count,game.BotCount.ToString(),"timer");Button(count,"+",()=>{game.BotCount++;game.ConfigureBots();},"count-button").SetEnabled(game.BotCount<4);
            ChoiceField(controls,"Сложность",BotMatchController.DifficultyNames,(int)game.BotLevel,id=>{game.BotLevel=(BotDifficulty)id;game.ConfigureBots();},"bot-difficulty");
            Text(content,"Состав боя","section-name");var preview=Element(content,"preview-fighters");
            for(int i=0;i<game.BotOffer.Count;i++)
            {
                var fighter=Element(preview,"preview-fighter");fighter.style.borderLeftColor=game.ParticipantColor(i);WeaponImage(fighter,game.BotOffer.weapons[i],"preview-weapon");
                Text(fighter,game.weapons[game.BotOffer.weapons[i]].displayName+(i==0?" · ты":""),"preview-name");
            }
            Text(content,$"300 HP · арена {8+2*(game.BotCount-1)} · без сужения · до 5 мин","condition-line");
            Button(content,"Другой состав",()=>game.ConfigureBots(true),"secondary").name="reroll-bots";
            Button(content,"Начать бой",game.StartBots,"primary").name="start-bots";
            Text(content,"Первый навык до старта, следующие каждые 20 с. На выбор 8 с. Монеты не расходуются.","small muted setup-note");
        }
        void ShowBotFight()
        {
            NewPage();hp.Clear();stats.Clear();reload.Clear();app.AddToClassList("bot-fight");app.AddToClassList("build-fight");
            var header=Element(app,"row header");IconButton(header,UiSymbol.Back,"",game.BackToSetup,"fight-menu");
            timer=Text(header,"00:00","timer");
            if(game.IsRunFight)Text(header,$"{game.Run.Data.stage+1}/5","run-stage-badge");IconButton(header,UiSymbol.Pause,"",game.TogglePause,"pause");
            var participants=Element(app,"fight-participants");for(int i=0;i<game.CurrentFight.Count;i++)AddParticipant(participants,i,true);
            var holder=Element(app,"arena-holder bot-arena-holder");
            var field=new Image{image=game.ArenaTexture,scaleMode=ScaleMode.StretchToFill,pickingMode=PickingMode.Ignore,name="arena"};field.AddToClassList("arena");holder.Add(field);
            sparkView=new BuildSparkView(game);field.Add(sparkView);
            app.RegisterCallback<GeometryChangedEvent>(e=>
            {
                float available=e.newRect.height-app.resolvedStyle.paddingTop-app.resolvedStyle.paddingBottom;
                float size=InterfacePresentation.BotArenaSize(app.contentRect.width,available,game.CurrentFight.Count);
                if(Mathf.Abs(holder.resolvedStyle.height-size)>1)holder.style.height=size;
            });
            holder.RegisterCallback<GeometryChangedEvent>(e=>{float size=Mathf.Max(1,Mathf.Min(e.newRect.width,e.newRect.height));field.style.width=size;field.style.height=size;});
            ownWeaponPanel=Element(app,"row own-weapon-panel");WeaponImage(ownWeaponPanel,game.CurrentFight.weapons[0],"own-weapon-image");
            var own=Element(ownWeaponPanel,"own-weapon-body");Text(own,game.weapons[game.CurrentFight.weapons[0]].displayName+" · твоё оружие","own-weapon-title");
            ownWeaponStats=Text(own,"","own-weapon-stats");
            skillStatus=Text(app,"","build-status");danger=Text(app,"","danger-label");danger.style.display=DisplayStyle.None;
            for(int i=0;i<game.CurrentFight.Count;i++){var label=Text(app,"","fighter-stat");label.style.display=DisplayStyle.None;stats.Add(label);}
            skillNotice=Text(app,"","skill-notice");
            var panel=Element(app,"build-panel");
            choiceArea=Element(panel,"choice-area");var choiceHeader=Element(choiceArea,"row choice-heading");choiceCountdown=Text(choiceHeader,"","choice-countdown");choiceCountdown.name="skill-countdown";
            var track=Element(choiceArea,"choice-track");choiceProgress=Element(track,"choice-progress");choiceRow=Element(choiceArea,"choice-row");
            buildArea=Element(panel,"build-area");Text(buildArea,"Твой билд","mini-title");
            var scroll=new ScrollView(ScrollViewMode.Vertical);scroll.AddToClassList("build-scroll");buildArea.Add(scroll);buildIcons=Element(scroll,"build-icons");
            buildDetail=Text(buildArea,"Нажми на навык или карточку участника.","build-detail");
            inspectionArea=Element(panel,"inspection-area");var inspectHeader=Element(inspectionArea,"row");inspectionTitle=Text(inspectHeader,"","inspection-title");
            IconButton(inspectHeader,UiSymbol.Close,"",CloseInspection,"close-inspection");
            var inspectionScroll=new ScrollView(ScrollViewMode.Vertical);inspectionScroll.AddToClassList("inspection-scroll");inspectionArea.Add(inspectionScroll);
            inspectionStats=Text(inspectionScroll,"","inspection-stats");inspectionEffects=Text(inspectionScroll,"","inspection-effects");inspectionIcons=Element(inspectionScroll,"build-icons");
            if(game.BotMatch.Data.variant==BattleVariant.Ability)
            {
                abilityButton=Button(app,"",game.UseBotAbility,"ability-button");abilityButton.name="use-ability";
                abilityIcon=new InterfaceIcon(game.Simulation.Builds[0].Ability==BattleAbility.Heal?UiSymbol.Heal:UiSymbol.Shield);abilityButton.Add(abilityIcon);
                var body=Element(abilityButton,"ability-body");abilityTitle=Text(body,"","ability-title");abilityHint=Text(body,"","ability-hint");IgnoreChildren(abilityButton);
            }
            else Text(app,game.BotMatch.Data.variant==BattleVariant.Sparks?"Сохранённый бой · искры на арене":"Сохранённый бой · без активки","small muted build-footer");
            RefreshFight();
        }
        void BuildSkillCards()
        {
            choiceRow.Clear();skillButtons.Clear();autoLabels.Clear();var match=game.BotMatch;var offer=match.Data.offers[0];visibleOfferId=offer.id;var b=game.Simulation.Builds[0];
            for(int i=0;i<offer.cards.Length;i++)
            {
                int slot=i;var skill=offer.cards[i].skill;int level=b.Level(skill)+1;
                var button=Button(choiceRow,"",()=>game.ChooseBotSkill(slot),"skill-card");button.name="skill-card-"+i;skillButtons.Add(button);
                var art=Element(button,"skill-art");art.Add(new InterfaceIcon(skill));
                var content=Element(button,"skill-content");var top=Element(content,"row skill-top");Text(top,BuildSkills.Name(skill),"skill-title");
                Text(top,BuildSkills.MaxLevel(skill)>1?$"{level}/{BuildSkills.MaxLevel(skill)}":"★","skill-level");
                Text(content,InterfacePresentation.SkillSummary(skill,level,b.InitialHP,game.Simulation.Orbs[0].Weapon.Kind)+(game.IsRunFight&&(skill==BuildSkill.Resurrection||skill==BuildSkill.SecondWind)?" · раз за забег":""),"skill-description");
                autoLabels.Add(Text(button,"","skill-auto"));IgnoreChildren(button);
            }
        }
        void InspectParticipant(int participant)
        {
            if(game.BotMatch.Data.offers[0].open||!game.BotMatch.Active)return;
            inspected=participant;inspectedBuildCount=-1;nextDetailsRefresh=0;RefreshBuildFight();
        }
        void CloseInspection(){inspected=-1;RefreshBuildFight();}
        void AddBuildIcon(VisualElement parent,int actor,BuildSkill skill,int level)
        {
            var icon=Button(parent,"",()=>ShowSkillDetail(actor,skill,level),"build-icon");icon.name="build-icon-"+actor+"-"+(int)skill;
            icon.Add(new InterfaceIcon(skill));Text(icon,level.ToString(),"build-icon-level");icon.tooltip=BuildSkills.Name(skill);IgnoreChildren(icon);
        }
        void ShowSkillDetail(int actor,BuildSkill skill,int level)
        {
            var b=game.Simulation.Builds[actor];string text=BuildSkills.Name(skill)+" · "+level+"\n"+BuildSkills.Description(skill,level,b.InitialHP,game.Simulation.Orbs[actor].Weapon.Kind);
            if(game.IsRunFight&&(skill==BuildSkill.Resurrection||skill==BuildSkill.SecondWind))text+="\nОдно срабатывание на весь забег.";
            if(inspected>=0){SetLabel(inspectionEffects,text);nextDetailsRefresh=Time.unscaledTime+4;}
            else SetLabel(buildDetail,text);
        }
        void RefreshBuildFight()
        {
            if(!game.IsBotFight||choiceArea==null)return;
            var match=game.BotMatch;bool choosing=match.Data.offers[0].open;var b=game.Simulation.Builds[0];
            if(choosing)inspected=-1;
            choiceArea.style.display=choosing?DisplayStyle.Flex:DisplayStyle.None;
            buildArea.style.display=!choosing&&inspected<0?DisplayStyle.Flex:DisplayStyle.None;
            inspectionArea.style.display=!choosing&&inspected>=0?DisplayStyle.Flex:DisplayStyle.None;
            SetLabel(skillStatus,!match.Data.started?"Первый навык · затем начнётся бой":$"Следующий навык через {Mathf.Max(0,Mathf.CeilToInt(match.NextOfferSeconds-game.Simulation.Elapsed))} с");
            SetLabel(skillNotice,match.Notice);
            SetLabel(ownWeaponStats,InterfacePresentation.WeaponStats(game.Simulation.Orbs[0],b));
            float growth=InterfacePresentation.GrowthValue(game.Simulation.Orbs[0].Weapon,b);
            if(previousGrowth>=0&&growth>previousGrowth+.001f)statsFlashUntil=Time.unscaledTime+.5f;
            previousGrowth=growth;ownWeaponPanel.EnableInClassList("stat-increased",Time.unscaledTime<statsFlashUntil);
            int total=0;foreach(int level in b.Levels)total+=level;
            if(total!=visibleBuildCount)
            {
                visibleBuildCount=total;buildIcons.Clear();
                for(int i=0;i<BuildSkills.Count;i++)if(b.Levels[i]>0)AddBuildIcon(buildIcons,0,(BuildSkill)i,b.Levels[i]);
            }
            if(inspected>=0&&Time.unscaledTime>=nextDetailsRefresh)
            {
                nextDetailsRefresh=Time.unscaledTime+.2f;var orb=game.Simulation.Orbs[inspected];var other=game.Simulation.Builds[inspected];
                SetLabel(inspectionTitle,game.weapons[game.CurrentFight.weapons[inspected]].displayName+(inspected==0?" · ты":$" · #{inspected+1}")+(orb.Health.IsDead?" · выбыл":""));
                SetLabel(inspectionStats,InterfacePresentation.WeaponStats(orb,other));SetLabel(inspectionEffects,InterfacePresentation.CombatDetails(orb,other));
                int sum=0;foreach(int level in other.Levels)sum+=level;
                if(sum!=inspectedBuildCount){inspectedBuildCount=sum;inspectionIcons.Clear();for(int i=0;i<BuildSkills.Count;i++)if(other.Levels[i]>0)AddBuildIcon(inspectionIcons,inspected,(BuildSkill)i,other.Levels[i]);}
            }
            if(abilityButton!=null)
            {
                bool heal=b.Ability==BattleAbility.Heal,cooldown=b.AbilityCooldown>.00001f,full=heal&&game.Simulation.Orbs[0].Health.Current>=game.Simulation.Orbs[0].Health.Maximum;
                SetLabel(abilityTitle,heal?"Лечение · +100 HP":"Барьер · 120");
                SetLabel(abilityHint,game.IsPaused?"Пауза":!match.Data.started?"После старта":cooldown?$"Готово через {Mathf.CeilToInt(b.AbilityCooldown)} с":full?"Здоровье полное":"Готово · нажми");
                abilityIcon.SetProgress(cooldown?1-b.AbilityCooldown/20:1);abilityButton.EnableInClassList("ability-ready",!cooldown&&!full&&match.Data.started&&!game.IsPaused);
                abilityButton.SetEnabled(!game.IsPaused&&match.Active&&match.Data.started&&!cooldown&&!full);
            }
            sparkView?.MarkDirtyRepaint();if(!choosing)return;
            if(visibleOfferId!=match.Data.offers[0].id)BuildSkillCards();
            SetLabel(choiceCountdown,$"Выбери навык · {Mathf.CeilToInt(match.ChoiceRemaining)} с");
            choiceProgress.style.width=Length.Percent(100*Mathf.Clamp01(match.ChoiceRemaining/match.Data.choiceSeconds));
            int automatic=match.AutoChoice();
            for(int i=0;i<skillButtons.Count;i++)
            {skillButtons[i].SetEnabled(!game.IsPaused&&match.CanChoose(0,i));SetLabel(autoLabels[i],i==automatic?"АВТО":"");skillButtons[i].EnableInClassList("auto-skill",i==automatic);}
        }
        public void ShowBotResult()
        {
            RefreshFight();int outcome=game.BotMatch.Data.outcome;bool limit=game.Simulation.Elapsed>=299.99f;
            var card=Dialog(outcome==1?"Победа":outcome==2?"Ничья":"Поражение",limit?"Результат по доле оставшегося HP.":outcome==0?"Твой шар выбыл.":null);
            card.Insert(0,new InterfaceIcon(UiSymbol.Trophy));
            Text(card,$"Время: {game.Simulation.Elapsed/60:00}:{game.Simulation.Elapsed%60:00}","rule");
            var scroll=new ScrollView(ScrollViewMode.Vertical);scroll.style.maxHeight=200;card.Add(scroll);var icons=Element(scroll,"build-icons");var b=game.Simulation.Builds[0];
            for(int i=0;i<BuildSkills.Count;i++)if(b.Levels[i]>0){var cell=Element(icons,"result-skill");cell.Add(new InterfaceIcon((BuildSkill)i));Text(cell,b.Levels[i].ToString(),"skill-level");cell.tooltip=BuildSkills.Name((BuildSkill)i);}
            Button(card,"Новый бой",()=>{game.BackToSetup();game.ConfigureBots(true);},"primary").name="new-bot-battle";
            Button(card,"Главное меню",game.OpenMainMenu,"secondary");
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
