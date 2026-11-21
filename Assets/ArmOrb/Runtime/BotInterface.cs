using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ArmOrb
{
    public sealed partial class GameInterface
    {
        MiniGameView miniView;
        Label essenceLabel, miniFeedback, skillStatus, restCountdown, restFeedback, choiceCountdown, skillNotice;
        VisualElement taskArea, restArea, choiceArea, gaugeFill, gaugeTrack, choiceRow;
        readonly List<Button> skillButtons = new List<Button>();
        readonly List<Label> autoLabels = new List<Label>(), targetLabels = new List<Label>();
        int visibleOfferId = -1;
        public void CancelMiniGesture() => miniView?.CancelGesture();
        void BotSetup(VisualElement content)
        {
            if(game.IsRestoring){Text(content,"Восстановление битвы…","section-name");return;}
            if(!string.IsNullOrEmpty(game.BotStore.Error))Text(content,game.BotStore.Error,"error");
            var pending=game.BotStore.Pending;
            if(pending!=null)
            {
                Text(content,"Незавершённая битва","section-name");
                Text(content,$"{BotMatchController.MiniNames[(int)pending.miniGame]} · {pending.fight.Count-1} ботов · {BotMatchController.DifficultyNames[(int)pending.difficulty]} · {BotMatchController.RewardNames[(int)pending.rewardMode]}","rule");
                Text(content,$"Эссенция: {(int)pending.essence[0]} · время: {pending.ticks/120f:0} с","rule");
                Button(content,"Продолжить битву",game.ResumeBots,"primary").name="continue-bots";
                Button(content,"Завершить попытку",game.DiscardBots,"secondary").name="discard-bots";return;
            }
            Text(content,"Битва с ботами","section-name");
            Text(content,"Выполняй задания, пока твой шар сражается. Выбирай навыки внизу экрана — бой продолжается.","rule");
            var names=new List<string>();foreach(var w in game.weapons)names.Add(w.displayName);
            var weapon=new DropdownField("Твоё оружие",names,game.BotWeapon);weapon.AddToClassList("wide-field");weapon.name="bot-player-weapon";content.Add(weapon);
            weapon.RegisterValueChangedCallback(_=>{game.BotWeapon=weapon.index;game.ConfigureBots();});
            var types=Element(content,"row segments");
            for(int i=0;i<3;i++)
            {
                int selected=i;var b=Button(types,BotMatchController.MiniNames[i],()=>{game.BotMini=(MiniGameKind)selected;game.ConfigureBots();},"segment");
                Selected(b,(int)game.BotMini==i);b.name="mini-type-"+i;
            }
            string[] descriptions={"Повторяй знак справа от образца. Точный рисунок даёт больше эссенции.","Соединяй одинаковые плитки в цепочки от трёх. Двигаться можно вверх, вниз и в стороны.","Нажимай при прохождении бегунка через зелёную область. Центр даёт больше эссенции."};
            Text(content,descriptions[(int)game.BotMini],"rule mini-description");
            var modes=Element(content,"row reward-options");
            for(int i=0;i<2;i++)
            {
                int selected=i;var button=Button(modes,BotMatchController.RewardNames[i],()=>{game.BotReward=(RewardMode)selected;game.ConfigureBots();},"segment reward-segment");
                Selected(button,(int)game.BotReward==i);button.name=i==0?"reward-gauge":"reward-timed";
            }
            Text(content,game.BotReward==RewardMode.Gauge?"60 эссенции → один из трёх навыков. На выбор 7 секунд.":"Каждые 30 секунд — покупка одного из трёх навыков. На выбор 7 секунд.","small muted reward-description");
            var count=Element(content,"row count-control");Text(count,"Боты","setting-label");
            Button(count,"−",()=>{game.BotCount--;game.ConfigureBots();},"count-button").SetEnabled(game.BotCount>1);
            Text(count,game.BotCount.ToString(),"timer");Button(count,"+",()=>{game.BotCount++;game.ConfigureBots();},"count-button").SetEnabled(game.BotCount<4);
            var difficulty=new DropdownField("Сложность",new List<string>(BotMatchController.DifficultyNames),(int)game.BotLevel);difficulty.AddToClassList("wide-field");difficulty.name="bot-difficulty";content.Add(difficulty);
            difficulty.RegisterValueChangedCallback(_=>{game.BotLevel=(BotDifficulty)difficulty.index;game.ConfigureBots();});
            Text(content,"Доход ботов: легко 1,4/с · обычно 2/с · сложно 2,6/с. Боты тоже получают по одному навыку за предложение.","small muted");
            var preview=Element(content,"conditions");Text(preview,$"300 HP каждому · арена {8+2*(game.BotCount-1)} · ×1","condition-title");
            for(int i=0;i<game.BotOffer.Count;i++)Text(preview,$"#{i+1} {game.weapons[game.BotOffer.weapons[i]].displayName}"+(i==0?" · ты":""),"bot-preview-line").style.color=game.ParticipantColor(i)*new Color(.75f,.75f,.75f,1);
            Button(content,"Другой состав",()=>game.ConfigureBots(true),"secondary").name="reroll-bots";
            Button(content,"Начать битву",game.StartBots,"primary").name="start-bots";
            Text(content,"Эссенция и усиления сбрасываются после матча. Монеты не расходуются.","small muted");
        }
        void ShowBotFight()
        {
            NewPage();hp.Clear();stats.Clear();reload.Clear();app.AddToClassList("bot-fight");
            var header=Element(app,"row header");Button(header,"Меню",game.BackToSetup,"header-button").name="fight-menu";
            timer=Text(header,"00:00","timer");Button(header,"Пауза",game.TogglePause,"header-button").name="pause";
            var participants=Element(app,"fight-participants");
            for(int i=0;i<game.CurrentFight.Count;i++)
            {
                var cell=Element(participants,"fight-participant");cell.style.borderLeftColor=game.ParticipantColor(i);
                if(i==0)cell.AddToClassList("focus-participant");
                var row=Element(cell,"row");Text(row,$"#{i+1} {game.weapons[game.CurrentFight.weapons[i]].displayName}"+(i==0?" · ты":""),"fighter-label");
                hp.Add(Text(row,"","fight-hp"));var fill=Element(Element(cell,"reload-track"),"reload-fill");fill.style.backgroundColor=game.ParticipantColor(i);reload.Add(fill);
            }
            var holder=Element(app,"arena-holder bot-arena-holder");
            var field=new Image{image=game.ArenaTexture,scaleMode=ScaleMode.StretchToFill,pickingMode=PickingMode.Ignore,name="arena"};field.AddToClassList("arena");holder.Add(field);
            app.RegisterCallback<GeometryChangedEvent>(e=>{float h=Mathf.Min(e.newRect.width,e.newRect.height*.47f);if(Mathf.Abs(holder.resolvedStyle.height-h)>1)holder.style.height=h;});
            holder.RegisterCallback<GeometryChangedEvent>(e=>{float size=Mathf.Max(1,Mathf.Min(e.newRect.width,e.newRect.height));field.style.width=size;field.style.height=size;});
            var resource=Element(app,"row essence-row");essenceLabel=Text(resource,"","essence");skillStatus=Text(resource,"","small");
            gaugeTrack=Element(app,"gauge-track");gaugeFill=Element(gaugeTrack,"gauge-fill");
            gaugeTrack.style.display=game.BotMatch.Data.rewardMode==RewardMode.Gauge?DisplayStyle.Flex:DisplayStyle.None;
            danger=Text(app,"","danger-label");danger.style.display=DisplayStyle.None;
            for(int i=0;i<game.CurrentFight.Count;i++)
            {
                var label=Text(app,"","fighter-stat");if(i>0)label.style.display=DisplayStyle.None;stats.Add(label);
            }
            skillNotice=Text(app,"","skill-notice");
            var panel=Element(app,"mini-panel");taskArea=Element(panel,"task-area");var title=Element(taskArea,"row");
            string[] instructions={"Повтори знак в правом поле","Соедини от трёх одинаковых","Нажми в зелёной области"};
            Text(title,BotMatchController.MiniNames[(int)game.BotMatch.Data.miniGame],"mini-title");
            if(game.BotMatch.Data.miniGame==MiniGameKind.Drawing)Button(title,"Другой знак",()=>{if(!game.IsPaused&&game.BotMatch.TasksReady){CancelMiniGesture();game.BotMatch.MiniGame.SkipDrawing();}},"text-button");
            Text(taskArea,instructions[(int)game.BotMatch.Data.miniGame],"small muted");
            miniView=new MiniGameView(game.BotMatch.MiniGame,()=>game.IsInFight&&!game.IsPaused&&game.BotMatch.TasksReady,game.EarnEssence);taskArea.Add(miniView);
            miniFeedback=Text(taskArea,"","mini-feedback");
            restArea=Element(panel,"rest-area");restArea.name="task-rest";restFeedback=Text(restArea,"","rest-feedback");restCountdown=Text(restArea,"","rest-countdown");
            choiceArea=Element(panel,"choice-area");choiceCountdown=Text(choiceArea,"","choice-countdown");choiceCountdown.name="skill-countdown";
            choiceRow=Element(choiceArea,"row choice-row");visibleOfferId=-1;RefreshFight();
        }
        void BuildSkillCards()
        {
            choiceRow.Clear();skillButtons.Clear();autoLabels.Clear();targetLabels.Clear();
            var match=game.BotMatch;var offer=match.Data.offers[0];visibleOfferId=offer.id;
            for(int i=0;i<offer.cards.Length;i++)
            {
                int slot=i;var skill=offer.cards[i];
                var button=Button(choiceRow,"",()=>game.ChooseBotSkill(slot),"skill-card");button.name="skill-card-"+i;skillButtons.Add(button);
                var content=Element(button,"skill-content");content.pickingMode=PickingMode.Ignore;
                Text(content,BotMatchController.Title(skill.kind),"skill-title");
                Text(content,BotMatchController.Description(skill.kind,match.Simulation.Orbs[0].Weapon.Kind),"skill-description");
                var target=Text(content,"","skill-target");targetLabels.Add(target);
                if(skill.target>=0)target.style.color=game.ParticipantColor(skill.target)*new Color(.65f,.65f,.65f,1);
                Text(content,match.Data.rewardMode==RewardMode.Timed?$"{BotMatchController.Cost(skill.kind)} эсс.":"", "skill-price");
                autoLabels.Add(Text(content,"","skill-auto"));
                foreach(var child in content.Query<Label>().ToList())child.pickingMode=PickingMode.Ignore;
            }
        }
        void RefreshMiniGame()
        {
            if(!game.IsBotFight||taskArea==null)return;
            var match=game.BotMatch;bool choosing=match.Data.offers[0].open;bool resting=!choosing&&!match.MiniGame.Ready;
            taskArea.style.display=choosing||resting?DisplayStyle.None:DisplayStyle.Flex;
            restArea.style.display=resting?DisplayStyle.Flex:DisplayStyle.None;
            choiceArea.style.display=choosing?DisplayStyle.Flex:DisplayStyle.None;
            miniView.Refresh();
            essenceLabel.text=match.Data.rewardMode==RewardMode.Gauge?$"Эссенция: {match.Balance}/60":$"Эссенция: {match.Balance}";
            gaugeFill.style.width=Length.Percent(100*Mathf.Clamp01(match.Balance/(float)BotMatchController.GaugeCost));
            skillStatus.text=choosing?"Выбор навыка":match.Data.rewardMode==RewardMode.Timed?$"Выбор через {Mathf.Max(0,Mathf.CeilToInt(match.NextOfferSeconds-game.Simulation.Elapsed))} с":"Навык за 60 эссенции";
            skillNotice.text=match.Notice;
            restFeedback.text=match.MiniGame.Feedback;restCountdown.text=$"Следующее задание через {Mathf.CeilToInt(match.Data.puzzle.restRemaining)} с";
            miniFeedback.text=match.MiniGame.Feedback;
            if(match.Data.miniGame==MiniGameKind.Drawing&&string.IsNullOrEmpty(miniFeedback.text))miniFeedback.text=MiniGameModel.DrawingNames[match.Data.puzzle.drawingTarget];
            if(!choosing)return;
            if(visibleOfferId!=match.Data.offers[0].id)BuildSkillCards();
            choiceCountdown.text=$"Выбери навык · {Mathf.CeilToInt(match.ChoiceRemaining)} с";
            int automatic=match.AutoChoice();
            for(int i=0;i<skillButtons.Count;i++)
            {
                var skill=match.Data.offers[0].cards[i];skillButtons[i].SetEnabled(!game.IsPaused&&match.CanChoose(0,i));
                autoLabels[i].text=i==automatic?"Автовыбор":"";skillButtons[i].EnableInClassList("auto-skill",i==automatic);
                targetLabels[i].text=skill.target<0?"Твой шар":$"#{skill.target+1} {game.weapons[game.CurrentFight.weapons[skill.target]].displayName}"+(match.Simulation.Orbs[skill.target].Health.IsDead?" · выбыл":"");
            }
        }
        public void ShowBotResult()
        {
            RefreshFight();int outcome=game.BotMatch.Data.outcome;
            var card=Dialog(outcome==1?"Победа":outcome==2?"Ничья":"Поражение",outcome==0?"Твой шар выбыл.":null);
            Text(card,$"Время: {game.Simulation.Elapsed:0.0} с · {BotMatchController.MiniNames[(int)game.BotMatch.Data.miniGame]}","rule");
            Button(card,"Новая битва",()=>{game.BackToSetup();game.ConfigureBots(true);},"primary").name="new-bot-battle";
            Button(card,"В меню",game.BackToSetup,"secondary");
        }
    }
}
