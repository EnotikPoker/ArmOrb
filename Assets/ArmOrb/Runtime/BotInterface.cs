using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ArmOrb
{
    public sealed partial class GameInterface
    {
        MiniGameView miniView;
        Label essenceLabel, miniFeedback, shopCountdown;
        public void CancelMiniGesture() => miniView?.CancelGesture();
        void BotSetup(VisualElement content)
        {
            if(game.IsRestoring){Text(content,"Восстановление битвы…","section-name");return;}
            if(!string.IsNullOrEmpty(game.BotStore.Error))Text(content,game.BotStore.Error,"error");
            var pending=game.BotStore.Pending;
            if(pending!=null)
            {
                Text(content,"Незавершённая битва","section-name");
                Text(content,$"{BotMatchController.MiniNames[(int)pending.miniGame]} · {pending.fight.Count-1} ботов · {BotMatchController.DifficultyNames[(int)pending.difficulty]}","rule");
                Text(content,$"Эссенция: {(int)pending.essence[0]} · время: {pending.ticks/120f:0} с","rule");
                Button(content,"Продолжить битву",game.ResumeBots,"primary").name="continue-bots";
                Button(content,"Завершить попытку",game.DiscardBots,"secondary").name="discard-bots";return;
            }
            Text(content,"Битва с ботами","section-name");
            Text(content,"Развивай свой шар заданиями внизу экрана. Каждые 20 секунд — магазин. Останься последним.","rule");
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
            var count=Element(content,"row count-control");Text(count,"Боты","setting-label");
            Button(count,"−",()=>{game.BotCount--;game.ConfigureBots();},"count-button").SetEnabled(game.BotCount>1);
            Text(count,game.BotCount.ToString(),"timer");Button(count,"+",()=>{game.BotCount++;game.ConfigureBots();},"count-button").SetEnabled(game.BotCount<4);
            var difficulty=new DropdownField("Сложность",new List<string>(BotMatchController.DifficultyNames),(int)game.BotLevel);difficulty.AddToClassList("wide-field");difficulty.name="bot-difficulty";content.Add(difficulty);
            difficulty.RegisterValueChangedCallback(_=>{game.BotLevel=(BotDifficulty)difficulty.index;game.ConfigureBots();});
            Text(content,"Доход ботов: легко 1,4/с · обычно 2/с · сложно 2,6/с. Боты тратят эссенцию в магазине.","small muted");
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
                var row=Element(cell,"row");Text(row,$"#{i+1} {game.weapons[game.CurrentFight.weapons[i]].displayName}"+(i==0?" · ты":""),"fighter-label");
                hp.Add(Text(row,"","fight-hp"));var fill=Element(Element(cell,"reload-track"),"reload-fill");fill.style.backgroundColor=game.ParticipantColor(i);reload.Add(fill);
            }
            var holder=Element(app,"arena-holder bot-arena-holder");
            var field=new Image{image=game.ArenaTexture,scaleMode=ScaleMode.StretchToFill,pickingMode=PickingMode.Ignore,name="arena"};field.AddToClassList("arena");holder.Add(field);
            app.RegisterCallback<GeometryChangedEvent>(e=>{float h=Mathf.Min(e.newRect.width,e.newRect.height*.47f);if(Mathf.Abs(holder.resolvedStyle.height-h)>1)holder.style.height=h;});
            holder.RegisterCallback<GeometryChangedEvent>(e=>{float size=Mathf.Max(1,Mathf.Min(e.newRect.width,e.newRect.height));field.style.width=size;field.style.height=size;});
            var resource=Element(app,"row essence-row");essenceLabel=Text(resource,"","essence");shopCountdown=Text(resource,"","small");
            danger=Text(app,"","danger-label");danger.style.display=DisplayStyle.None;
            for(int i=0;i<game.CurrentFight.Count;i++)
            {
                var label=Text(app,"","fighter-stat");if(i>0)label.style.display=DisplayStyle.None;stats.Add(label);
            }
            var panel=Element(app,"mini-panel");var title=Element(panel,"row");
            string[] instructions={"Повтори знак в правом поле","Соедини от трёх одинаковых","Нажми в зелёной области"};
            Text(title,BotMatchController.MiniNames[(int)game.BotMatch.Data.miniGame],"mini-title");
            if(game.BotMatch.Data.miniGame==MiniGameKind.Drawing)Button(title,"Другой знак",()=>{if(!game.IsPaused){CancelMiniGesture();game.BotMatch.MiniGame.SkipDrawing();}},"text-button");
            Text(panel,instructions[(int)game.BotMatch.Data.miniGame],"small muted");
            miniView=new MiniGameView(game.BotMatch.MiniGame,()=>game.IsInFight&&!game.IsPaused&&game.BotMatch.Active,game.EarnEssence);panel.Add(miniView);
            miniFeedback=Text(panel,"","mini-feedback");RefreshFight();
        }
        void RefreshMiniGame()
        {
            if(!game.IsBotFight||miniView==null)return;
            miniView.Refresh();essenceLabel.text=$"Эссенция: {game.BotMatch.Balance}";
            shopCountdown.text=game.BotMatch.Data.visit<4?$"Магазин через {Mathf.Max(0,Mathf.CeilToInt(game.BotMatch.NextShop-game.Simulation.Elapsed))} с":game.Simulation.Elapsed<90?"Последний этап":"Опасная зона";
            miniFeedback.text=game.BotMatch.MiniGame.Feedback;
            if(game.BotMatch.Data.miniGame==MiniGameKind.Drawing&&string.IsNullOrEmpty(miniFeedback.text))miniFeedback.text=MiniGameModel.DrawingNames[game.BotMatch.Data.puzzle.drawingTarget];
        }
        public void ShowBotShop()
        {
            CancelMiniGesture();RefreshFight();
            var match=game.BotMatch;
            var card=Dialog($"Магазин · {match.Data.visit}/4",$"Эссенция: {match.Balance} · бой на паузе");
            var scroll=new ScrollView();scroll.AddToClassList("shop-scroll");scroll.style.maxHeight=Mathf.Max(200,root.resolvedStyle.height*.54f);card.Add(scroll);
            for(int i=0;i<match.Data.cards.Length;i++)
            {
                int slot=i;var item=match.Data.cards[i];var box=Element(scroll,"shop-item");
                Text(box,$"{BotMatchController.Title(item.kind)} · {BotMatchController.Cost(item.kind)}","condition-title");
                Text(box,BotMatchController.Description(item.kind,match.Simulation.Orbs[0].Weapon.Kind),"small");
                int target=-1;
                if((int)item.kind>=(int)UpgradeKind.Vulnerability)
                {
                    var ids=new List<int>();var names=new List<string>();
                    for(int id=1;id<match.Simulation.Orbs.Length;id++)if(!match.Simulation.Orbs[id].Health.IsDead){ids.Add(id);names.Add($"#{id+1} {game.weapons[game.CurrentFight.weapons[id]].displayName} · {Mathf.CeilToInt(match.Simulation.Orbs[id].Health.Current)} HP");}
                    if(ids.Count>0)
                    {
                        target=ids[0];var select=new DropdownField(names,0);select.AddToClassList("wide-field");select.name="shop-target-"+slot;box.Add(select);
                        select.RegisterValueChangedCallback(_=>target=ids[select.index]);
                    }
                }
                var buy=Button(box,item.sold?"Куплено":"Купить",()=>game.BuyBotUpgrade(slot,target),"shop-buy");buy.name="shop-buy-"+slot;
                buy.SetEnabled(!item.sold&&match.Balance>=BotMatchController.Cost(item.kind)&&match.Simulation.CanUpgrade(0,item.kind,target));
            }
            Button(card,"Продолжить бой",game.ContinueBotShop,"primary").name="continue-bot-shop";
            Button(card,"Сохранить и выйти",game.BackToSetup,"text-button");
        }
        public void ShowBotPurchases()
        {
            var card=Dialog("Покупки",$"Осталось эссенции: {game.BotMatch.Balance}");
            var scroll=new ScrollView();scroll.style.maxHeight=Mathf.Max(180,root.resolvedStyle.height*.48f);card.Add(scroll);
            Text(scroll,string.IsNullOrEmpty(game.BotMatch.Data.purchases)?"В этот раз никто ничего не купил.":game.BotMatch.Data.purchases.TrimEnd(),"rule");
            Button(card,"Продолжить",game.TogglePause,"primary").name="resume-bot-purchases";
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
