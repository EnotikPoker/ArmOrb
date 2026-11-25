using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ArmOrb
{
    public sealed partial class GameInterface
    {
        readonly List<VisualElement> healthFills=new List<VisualElement>(),barrierFills=new List<VisualElement>();
        static void SetLabel(Label label,string value){if(label!=null&&label.text!=value)label.text=value;}
        Image WeaponImage(VisualElement parent,int weapon,string classes)
        {var icon=new Image{sprite=game.weapons[weapon].sprite,scaleMode=ScaleMode.ScaleToFit,pickingMode=PickingMode.Ignore};Classes(icon,classes);parent.Add(icon);return icon;}
        Button IconButton(VisualElement parent,UiSymbol symbol,string text,Action action,string name)
        {
            var button=Button(parent,"",action,"icon-button");button.name=name;button.tooltip=string.IsNullOrEmpty(text)?symbol.ToString():text;
            button.Add(new InterfaceIcon(symbol));if(!string.IsNullOrEmpty(text))Text(button,text,"icon-button-label").pickingMode=PickingMode.Ignore;return button;
        }
        Button ChoiceField(VisualElement parent,string label,IList<string> options,int selected,Action<int> changed,string name,bool weaponIcons=false)
        {
            var row=Element(parent,"row choice-field");if(!string.IsNullOrEmpty(label))Text(row,label,"choice-field-label");
            var button=Button(row,"",()=>
            {
                var card=Dialog(string.IsNullOrEmpty(label)?"Оружие":label);var scroll=new ScrollView(ScrollViewMode.Vertical);scroll.AddToClassList("selection-scroll");card.Add(scroll);
                for(int i=0;i<options.Count;i++)
                {
                    int id=i;var option=Button(scroll,"",()=>{CloseOverlay();changed(id);},"selection-option");
                    if(weaponIcons)WeaponImage(option,id,"selection-weapon");Text(option,options[i],"selection-label");Selected(option,id==selected);IgnoreChildren(option);
                }
                Button(card,"Закрыть",CloseOverlay,"secondary");
            },"choice-field-value");button.name=name;
            if(weaponIcons)WeaponImage(button,selected,"selection-weapon");
            Text(button,options[selected],"choice-field-text");var arrow=new InterfaceIcon(UiSymbol.Play);arrow.AddToClassList("choice-field-arrow");button.Add(arrow);IgnoreChildren(button);return button;
        }
        Button NumberField(VisualElement parent,string label,Func<long> current,long minimum,long maximum,Action<long> changed,string name,string classes)
        {
            var row=Element(parent,"row number-field "+classes);Text(row,label,"number-field-label");Button value=null;
            value=Button(row,current().ToString("N0"),()=>ShowNumberEntry(label=="HP"?"Здоровье":"Сумма ставки",current(),minimum,maximum,n=>{changed(n);value.text=n.ToString("N0");}),"number-field-value");
            value.name=name;return value;
        }
        void ShowNumberEntry(string title,long current,long minimum,long maximum,Action<long> accepted)
        {
            var draft=new NumberEntry(current,minimum,maximum);var card=Dialog(title);card.AddToClassList("number-dialog");
            var display=Text(card,draft.Text,"number-display");display.name="number-entry-value";
            Text(card,$"От {minimum:N0} до {maximum:N0}","small muted number-range");
            var grid=Element(card,"number-grid");
            void Refresh()=>SetLabel(display,string.IsNullOrEmpty(draft.Text)?"0":draft.Text);
            for(int i=1;i<=9;i++){int digit=i;Button(grid,i.ToString(),()=>{draft.Digit(digit);Refresh();},"number-key").name="number-key-"+i;}
            Button(grid,"Сброс",()=>{draft.Clear();Refresh();},"number-key number-key-small").name="number-clear";
            Button(grid,"0",()=>{draft.Digit(0);Refresh();},"number-key").name="number-key-0";
            Button(grid,"⌫",()=>{draft.Backspace();Refresh();},"number-key").name="number-backspace";
            var actions=Element(card,"row number-actions");Button(actions,"Отмена",CloseOverlay,"secondary").name="number-cancel";
            Button(actions,"Готово",()=>{long number=draft.Value;CloseOverlay();accepted(number);},"primary").name="number-confirm";
        }
        void CoinBalance(VisualElement parent)
        {var row=Element(parent,"row coin-balance");row.Add(new InterfaceIcon(UiSymbol.Coins));Text(row,$"{game.Wallet.Data.balance:N0} монет","balance");}
        void ShowMainMenu()
        {
            app.AddToClassList("main-menu");
            var header=Element(app,"row header");Text(header,"ARMORB","logo");IconButton(header,UiSymbol.Settings,"",ShowSettings,"settings");
            var scroll=new ScrollView(ScrollViewMode.Vertical);scroll.AddToClassList("setup-scroll");app.Add(scroll);
            var content=Element(scroll,"main-content");
            var hero=Button(content,"",()=>game.SelectMode(GameMode.Bots),"mode-hero");hero.name="mode-bots";
            var artwork=Element(hero,"hero-art");artwork.pickingMode=PickingMode.Ignore;
            for(int i=0;i<3;i++)
            {
                var orb=Element(artwork,"hero-orb hero-orb-"+i);orb.style.backgroundColor=game.ParticipantColor(i);orb.pickingMode=PickingMode.Ignore;
                WeaponImage(orb,new[]{0,1,3}[i],"hero-weapon");
            }
            Text(hero,"Битва с ботами","hero-title").pickingMode=PickingMode.Ignore;
            Text(hero,"Улучшения и активная способность","hero-caption").pickingMode=PickingMode.Ignore;
            var arrow=Element(hero,"hero-go");arrow.Add(new InterfaceIcon(UiSymbol.Play));arrow.pickingMode=PickingMode.Ignore;
            if(game.BotStore.Pending!=null)
            {
                var pending=game.BotStore.Pending;
                var card=Button(content,"",game.ResumeBots,"continue-card");card.name="continue-bots";
                card.Add(new InterfaceIcon(UiSymbol.Play));var body=Element(card,"continue-body");body.pickingMode=PickingMode.Ignore;
                Text(body,"Продолжить битву","card-title");Text(body,$"{pending.fight.Count-1} ботов · {pending.ticks/pending.frequency/60:00}:{pending.ticks/pending.frequency%60:00}","small muted");IgnoreChildren(card);
            }
            ModeCard(content,UiSymbol.Coins,"Ставки",$"{game.Wallet.Data.balance:N0} монет",GameMode.Bets,"mode-bets");
            if(game.Wallet.Data.pending!=null)
            {
                var p=game.Wallet.Data.pending;var button=Button(content,$"Продолжить ставку · {p.stake:N0} монет · #{p.selection+1}",game.ResumeBet,"continue-bet");button.name="continue-bet";
            }
            ModeCard(content,UiSymbol.Test,"Тест","Оружие, здоровье и условия боя",GameMode.Test,"mode-test");
            if(!string.IsNullOrEmpty(game.Error))Text(content,game.Error,"error");
            var footer=Element(app,"row footer");Text(footer,"v0.9.0","version");Button(footer,"Правила",ShowRules,"text-button");
        }
        void ModeCard(VisualElement parent,UiSymbol symbol,string title,string caption,GameMode mode,string name)
        {
            var button=Button(parent,"",()=>game.SelectMode(mode),"menu-mode-card");button.name=name;button.Add(new InterfaceIcon(symbol));
            var body=Element(button,"menu-mode-body");Text(body,title,"card-title");Text(body,caption,"small muted");var arrow=new InterfaceIcon(UiSymbol.Play);arrow.AddToClassList("mode-arrow");button.Add(arrow);IgnoreChildren(button);
        }
        static void IgnoreChildren(VisualElement parent){foreach(var child in parent.Children()){child.pickingMode=PickingMode.Ignore;IgnoreChildren(child);}}
        void ShowSettings()
        {
            var card=Dialog("Настройки");
            sound=Button(card,"",()=>{game.ToggleSound();RefreshSound();},"secondary");RefreshSound();
            Button(card,"Правила",ShowRules,"secondary");Button(card,"Закрыть",CloseOverlay,"primary");
        }
        void AddParticipant(VisualElement parent,int id,bool inspectable)
        {
            VisualElement cell;
            if(inspectable){int participant=id;cell=Button(parent,"",()=>InspectParticipant(participant),"fight-participant");}
            else cell=Element(parent,"fight-participant");
            cell.name="participant-"+id;cell.style.borderLeftColor=game.ParticipantColor(id);
            if(OrbView.IsFocus(game.IsBotFight,game.IsBetFight,game.LastBetSelection,id))cell.AddToClassList("focus-participant");
            var row=Element(cell,"row participant-info");WeaponImage(row,game.CurrentFight.weapons[id],"participant-weapon");
            var body=Element(row,"participant-body");Text(body,game.weapons[game.CurrentFight.weapons[id]].displayName+(game.IsBotFight&&id==0?" · ты":""),"fighter-label");
            hp.Add(Text(body,"","fight-hp"));
            var fill=Element(Element(cell,"health-track"),"health-fill");fill.style.backgroundColor=game.ParticipantColor(id);healthFills.Add(fill);
            barrierFills.Add(Element(Element(cell,"barrier-track"),"barrier-fill"));
            var reloadFill=Element(Element(cell,"reload-track"),"reload-fill");reloadFill.style.backgroundColor=game.ParticipantColor(id);reload.Add(reloadFill);
            if(inspectable)IgnoreChildren(cell);
        }
        void RefreshParticipant(int id,CombatantState orb)
        {
            if(id>=healthFills.Count)return;
            healthFills[id].style.width=Length.Percent(100*Mathf.Clamp01(orb.Health.Current/orb.Health.Maximum));
            float barrier=game.IsBotFight?game.Simulation.Builds[id].BarrierTotal:0;
            barrierFills[id].parent.style.display=barrier>0?DisplayStyle.Flex:DisplayStyle.None;
            barrierFills[id].style.width=Length.Percent(100*Mathf.Clamp01(barrier/orb.Health.Maximum));
        }
    }
}
