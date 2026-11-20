using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ArmOrb
{
    public sealed partial class GameInterface
    {
        readonly ArmOrbGame game;
        readonly VisualElement root;
        VisualElement app,overlay;
        Button sound;
        Label timer,calculation,danger;
        readonly List<Label> hp=new List<Label>(),stats=new List<Label>();
        readonly List<VisualElement> reload=new List<VisualElement>();
        readonly List<(Button button,float speed)> speeds=new List<(Button,float)>();
        Rect previousSafe;
        Vector2Int previousSize;
        int betSelection;
        long stake=100;
        public GameInterface(ArmOrbGame game)
        { this.game=game;root=game.document.rootVisualElement;root.styleSheets.Add(game.styleSheet);root.style.unityFont=game.uiFont; }
        VisualElement Element(VisualElement parent,string classes=null)
        {var e=new VisualElement();Classes(e,classes);parent.Add(e);return e;}
        Label Text(VisualElement parent,string text,string classes=null)
        {var e=new Label(text);Classes(e,classes);parent.Add(e);return e;}
        Button Button(VisualElement parent,string text,Action action,string classes=null)
        {var e=new Button(action){text=text};Classes(e,classes);parent.Add(e);return e;}
        static void Classes(VisualElement e,string classes){if(classes!=null)foreach(string c in classes.Split(' '))e.AddToClassList(c);}
        static void Selected(VisualElement e,bool selected)=>e.EnableInClassList("selected",selected);
        void NewPage()
        {CancelMiniGesture();miniView=null;essenceLabel=null;miniFeedback=null;root.Clear();overlay=null;calculation=null;speeds.Clear();app=Element(root,"app");previousSize=Vector2Int.zero;UpdateSafeArea();}
        public void ShowSetup()=>ShowHome();
        public void ShowHome()
        {
            NewPage();
            var header=Element(app,"row header");Text(header,"ARMORB","logo");Text(header,$"{game.Wallet.Data.balance:N0} монет","balance");
            var tabs=Element(app,"row mode-tabs");
            var bets=Button(tabs,"Ставки",()=>game.SelectMode(GameMode.Bets),"mode-tab");Selected(bets,game.Mode==GameMode.Bets);bets.name="mode-bets";
            var test=Button(tabs,"Тест",()=>game.SelectMode(GameMode.Test),"mode-tab");Selected(test,game.ShowTest);test.name="mode-test";
            var bots=Button(tabs,"С ботами",()=>game.SelectMode(GameMode.Bots),"mode-tab");Selected(bots,game.Mode==GameMode.Bots);bots.name="mode-bots";
            var scroll=new ScrollView(ScrollViewMode.Vertical);scroll.AddToClassList("setup-scroll");app.Add(scroll);
            var content=Element(scroll.contentContainer,"home-content");
            if(!string.IsNullOrEmpty(game.Error))Text(content,game.Error,"error");
            if(!string.IsNullOrEmpty(game.Wallet.RecoveryMessage))Text(content,game.Wallet.RecoveryMessage,"small muted");
            if(game.ShowTest)TestSetup(content);else if(game.Mode==GameMode.Bots)BotSetup(content);else BetSetup(content);
            var footer=Element(app,"row footer");Button(footer,"Правила · v0.6",ShowRules,"text-button");sound=Button(footer,"",game.ToggleSound,"text-button");RefreshSound();
        }
        void TestSetup(VisualElement content)
        {
            var count=Element(content,"row count-control");Text(count,"Участники","setting-label");
            Button(count,"−",()=>game.ResizeTest(game.TestFight.Count-1),"count-button").SetEnabled(game.TestFight.Count>2);
            Text(count,game.TestFight.Count.ToString(),"timer");Button(count,"+",()=>game.ResizeTest(game.TestFight.Count+1),"count-button").SetEnabled(game.TestFight.Count<5);
            var names=new List<string>();foreach(var w in game.weapons)names.Add(w.displayName);
            for(int i=0;i<game.TestFight.Count;i++)
            {
                int id=i;var card=Element(content,"test-card");card.style.borderLeftColor=game.ParticipantColor(i);
                var row=Element(card,"row");Text(row,"#"+(i+1),"participant-number").style.color=game.ParticipantColor(i);
                var weapon=new DropdownField(names,game.TestFight.weapons[i]);weapon.AddToClassList("weapon-dropdown");weapon.name="weapon-"+i;
                weapon.RegisterValueChangedCallback(e=>game.TestFight.weapons[id]=names.IndexOf(e.newValue));row.Add(weapon);
                var health=new IntegerField("HP"){value=game.TestFight.health[i],isDelayed=true,keyboardType=TouchScreenKeyboardType.NumberPad};health.AddToClassList("compact-hp");health.name="health-"+i;
                health.RegisterValueChangedCallback(e=>{game.TestFight.health[id]=Mathf.Clamp(e.newValue,1,99999);health.SetValueWithoutNotify(game.TestFight.health[id]);});row.Add(health);
                Text(card,game.weapons[game.TestFight.weapons[i]].description,"small muted");
                weapon.RegisterValueChangedCallback(e=>card.Q<Label>(className:"muted").text=game.weapons[game.TestFight.weapons[id]].description);
            }
            var options=Element(content,"setup-options");Text(options,"Арена","setting-label");
            var arena=new DropdownField(new List<string>(ArmOrbGame.SizeNames),(int)game.Options.arena);arena.AddToClassList("wide-field");arena.name="arena-size";
            arena.RegisterValueChangedCallback(e=>{game.Options.arena=(ArenaSize)arena.index;game.Options.Save();});options.Add(arena);
            Toggle(options,"Гравитация",game.Options.gravity,v=>game.Options.gravity=v,"gravity");
            Toggle(options,"Сближение после затишья",game.Options.activeMotion,v=>game.Options.activeMotion=v,"active-motion");
            Toggle(options,"Опасная зона с 90-й секунды",game.Options.danger,v=>game.Options.danger=v,"danger-toggle");
            Text(options,"Скорость","setting-label");AddSpeedControl(options);
            Button(content,"Начать",()=>game.StartFight(),"primary").name="start-fight";
        }
        void Toggle(VisualElement parent,string label,bool value,Action<bool> action,string name)
        {
            var toggle=new Toggle(label){value=value,name=name};toggle.AddToClassList("option-toggle");
            toggle.RegisterValueChangedCallback(e=>{action(e.newValue);game.Options.Save();});parent.Add(toggle);
        }
        void BetSetup(VisualElement content)
        {
            if(game.IsRestoring){Text(content,"Восстановление боя…","section-name");return;}
            var pending=game.Wallet.Data.pending;
            if(pending!=null)
            {
                Text(content,"Незавершённый бой","section-name");Text(content,$"Ставка {pending.stake} на #{pending.selection+1} · ×{pending.oddsHundredths/100.0:0.00}","rule");
                Conditions(content,pending.fight);
                for(int i=0;i<pending.fight.Count;i++)Text(content,$"#{i+1} {game.weapons[pending.fight.weapons[i]].displayName} · {pending.fight.health[i]} HP"+(i==pending.selection?" ★":""),"rule");
                Button(content,"Продолжить бой",game.ResumeBet,"primary").name="continue-bet";return;
            }
            if(game.Wallet.Data.balance<10)Button(content,"Получить 1000 монет",game.Refill,"secondary").name="refill-coins";
            if(game.Offer==null){Text(content,"Новый бой","section-name");Button(content,"Подобрать бой",game.NewOffer,"primary").name="new-offer";return;}
            var f=game.Offer;betSelection=Mathf.Clamp(betSelection,0,f.Count-1);
            Conditions(content,f);
            if(game.Calculator!=null)
            {
                calculation=Text(content,"","calculation");RefreshCalculation();
                Text(content,"Коэффициенты по скрытым боям этого состава. Минимум 16 прогонов; время зависит от телефона.","small muted");
            }
            var choices=new Button[f.Count];
            for(int i=0;i<f.Count;i++)
            {
                int id=i;var choice=Button(content,"",()=>{betSelection=id;ShowHome();},"bet-choice");choices[i]=choice;choice.name="bet-on-"+i;
                choice.style.borderLeftColor=game.ParticipantColor(i);Selected(choice,betSelection==i);
                var row=Element(choice,"row");row.pickingMode=PickingMode.Ignore;
                var icon=new Image{sprite=game.weapons[f.weapons[i]].sprite,scaleMode=ScaleMode.ScaleToFit,pickingMode=PickingMode.Ignore};icon.AddToClassList("offer-icon");row.Add(icon);
                Text(row,$"#{i+1}  {game.weapons[f.weapons[i]].displayName}","offer-name");
                Text(row,$"{f.health[i]} HP","offer-health");Text(row,game.Quote==null?"…":$"×{game.Quote.Hundredths[i]/100.0:0.00}","offer-odds");
            }
            stake=Math.Max(10,Math.Min(stake,game.Wallet.Data.balance/10*10));
            var input=new LongField("Ставка"){value=stake,isDelayed=true,keyboardType=TouchScreenKeyboardType.NumberPad};input.AddToClassList("stake-input");input.name="stake-amount";content.Add(input);
            var payment=Text(content,"","payout");
            Button confirm=null;
            void RefreshPayout()
            {
                bool valid=game.Quote!=null&&stake>=10&&stake%10==0&&stake<=game.Wallet.Data.balance;
                try
                {
                    payment.text=game.Quote==null?"Расчёт коэффициентов…":$"Выплата при победе: {WalletStore.Payout(Math.Max(0,stake),game.Quote.Hundredths[betSelection]):N0} монет";
                }
                catch(OverflowException){valid=false;payment.text="Уменьши сумму ставки.";}
                confirm?.SetEnabled(valid);
            }
            input.RegisterValueChangedCallback(e=>{stake=Math.Max(0,Math.Min(e.newValue,1000000000000L));input.SetValueWithoutNotify(stake);RefreshPayout();});
            var amounts=Element(content,"row stake-buttons");
            foreach(long amount in new long[]{10,50,100,0})
            {
                long selected=amount;Button(amounts,amount==0?"Все":amount.ToString(),()=>{stake=selected==0?game.Wallet.Data.balance/10*10:selected;input.SetValueWithoutNotify(stake);RefreshPayout();},"stake-button");
            }
            confirm=Button(content,"Поставить и смотреть",()=>game.BeginBet(betSelection,stake),"primary");confirm.name="place-bet";RefreshPayout();
            var actions=Element(content,"row offer-actions");
            Button(actions,"Другой бой",game.NewOffer,"secondary").name="reroll-offer";
            var watch=Button(actions,"Без ставки",game.WatchOffer,"secondary");watch.SetEnabled(game.Quote!=null);watch.name="watch-offer";
            if(game.Quote!=null)Text(content,$"Оценка по {game.Quote.Samples} боям · ничья возвращает ставку","small muted");
        }
        void Conditions(VisualElement parent,FightDefinition fight)
        {
            var box=Element(parent,"conditions");Text(box,$"Участников: {fight.Count} · {ArmOrbGame.SizeNames[(int)fight.arena]}","condition-title");
            Text(box,(fight.gravity?"С гравитацией":"Без гравитации")+" · сближение после затишья","small");
            Text(box,"С 90 с безопасный круг сужается; снаружи — урон.","small muted");
        }
        public void RefreshCalculation(){if(calculation!=null&&game.Calculator!=null)calculation.text=$"Расчёт коэффициентов… {game.Calculator.Completed} боёв";}
        void AddSpeedControl(VisualElement parent)
        {
            var row=Element(parent,"row segments speed-control");foreach(float value in new[]{0.5f,1,2})
            {
                float v=value;var b=Button(row,value==0.5f?"x0.5":value==1?"x1":"x2",()=>game.SetPlaybackSpeed(v),"segment");b.name=value==0.5f?"speed-half":value==1?"speed-normal":"speed-double";speeds.Add((b,value));
            }RefreshSpeed();
        }
        public void RefreshSpeed(){foreach(var pair in speeds)Selected(pair.button,Mathf.Approximately(pair.speed,game.Options.playbackSpeed));}
        public void ShowFight()
        {
            if(game.IsBotFight){ShowBotFight();return;}
            NewPage();hp.Clear();stats.Clear();reload.Clear();
            var header=Element(app,"row header");Button(header,"Меню",game.BackToSetup,"header-button").name="fight-menu";timer=Text(header,"00:00","timer");Button(header,"Пауза",game.TogglePause,"header-button").name="pause";
            var participants=Element(app,"fight-participants");
            for(int i=0;i<game.CurrentFight.Count;i++)
            {
                var cell=Element(participants,"fight-participant");cell.style.borderLeftColor=game.ParticipantColor(i);
                var row=Element(cell,"row");Text(row,$"#{i+1} {game.weapons[game.CurrentFight.weapons[i]].displayName}"+(game.IsBetFight&&game.LastBetSelection==i?" ★":""),"fighter-label");hp.Add(Text(row,"","fight-hp"));
                var track=Element(cell,"reload-track");var fill=Element(track,"reload-fill");fill.style.backgroundColor=game.ParticipantColor(i);reload.Add(fill);
            }
            danger=Text(app,"","danger-label");
            var holder=Element(app,"arena-holder");var field=new Image{image=game.ArenaTexture,scaleMode=ScaleMode.StretchToFill,pickingMode=PickingMode.Ignore,name="arena"};field.AddToClassList("arena");holder.Add(field);
            holder.RegisterCallback<GeometryChangedEvent>(e=>{float size=Mathf.Max(1,Mathf.Min(e.newRect.width,e.newRect.height));field.style.width=size;field.style.height=size;});
            var statList=Element(app,"fight-stats");for(int i=0;i<game.CurrentFight.Count;i++){var s=Text(statList,"","fighter-stat");s.style.color=game.ParticipantColor(i)*new Color(0.7f,0.7f,0.7f,1);stats.Add(s);}
            AddSpeedControl(app);var footer=Element(app,"row footer");
            Text(footer,game.IsBetFight?$"Ставка {game.LastStake} · #{game.LastBetSelection+1}":"Тест","small muted");sound=Button(footer,"",game.ToggleSound,"text-button");RefreshSound();RefreshFight();
        }
        public void RefreshFight()
        {
            if(game.Simulation==null||timer==null||hp.Count!=game.Simulation.Orbs.Length)return;
            RefreshMiniGame();
            var sim=game.Simulation;timer.text=$"{(int)sim.Elapsed/60:00}:{(int)sim.Elapsed%60:00}";
            for(int i=0;i<hp.Count;i++)
            {
                var orb=sim.Orbs[i];hp[i].text=orb.Health.IsDead?"Выбыл":Mathf.CeilToInt(orb.Health.Current)+" HP";
                stats[i].text=orb.Health.IsDead?$"#{i+1} — выбыл":$"#{i+1} · {orb.Weapon.Stat}"+(orb.Health.PoisonPerSecond>0?$" · Яд −{orb.Health.PoisonPerSecond:0.#}/с":"");
                reload[i].parent.style.visibility=orb.Weapon.IsRanged&&!orb.Health.IsDead?Visibility.Visible:Visibility.Hidden;
                reload[i].style.width=Length.Percent(orb.Weapon.IsBursting?0:100*(1-Mathf.Clamp01(orb.Weapon.ReloadRemaining/orb.Weapon.Cooldown)));
            }
            danger.text=!sim.Danger?"":sim.Elapsed>=90?"ОПАСНАЯ ЗОНА":$"Сужение через {Mathf.CeilToInt(90-sim.Elapsed)} с";
        }
        public void RefreshSound(){if(sound!=null)sound.text=game.feedback.Muted?"Звук: выкл":"Звук: вкл";}
        VisualElement Dialog(string title,string description=null)
        {
            CloseOverlay();overlay=Element(root,"overlay");var card=Element(overlay,"dialog");Text(card,title,"dialog-title");if(!string.IsNullOrEmpty(description))Text(card,description,"dialog-description");return card;
        }
        void CloseOverlay(){overlay?.RemoveFromHierarchy();overlay=null;}
        public void ShowPause(bool paused)
        {
            if(!paused){CloseOverlay();return;}
            var card=Dialog("Пауза",game.IsBetFight?"Ставка сохранена. Бой можно продолжить позже.":null);
            Button(card,"Продолжить",game.TogglePause,"primary").name="resume";Button(card,"В меню",game.BackToSetup,"secondary");
        }
        public void ShowResult()
        {
            RefreshFight();int winner=game.Simulation.WinnerIndex;
            var card=Dialog(winner<0?"Ничья":$"Победитель: #{winner+1}",winner<0?"Все участники выбыли.":game.weapons[game.CurrentFight.weapons[winner]].displayName);
            Text(card,$"Время: {game.Simulation.Elapsed:0.0} с","small");
            if(game.IsBetFight)
            {
                Text(card,winner<0?$"Возврат: {game.LastPayment:N0} монет":game.LastPayment>0?$"Выплата: {game.LastPayment:N0} монет":"Ставка проиграла","rule");Text(card,$"Баланс: {game.Wallet.Data.balance:N0}","rule");
                Button(card,"Следующий бой",()=>{game.BackToSetup();game.NewOffer();},"primary").name="next-bet";
            }
            else Button(card,game.ShowTest?"Новый бой":"К ставкам",()=>{if(game.ShowTest)game.StartFight();else game.BackToSetup();},"primary").name="rematch";
            Button(card,"В меню",game.BackToSetup,"secondary");
        }
        public void ShowError(string error){var card=Dialog("Не удалось сохранить",error);Button(card,"Закрыть",CloseOverlay,"primary");}
        public void ShowSettlementError(string error,Action retry){var card=Dialog("Не удалось сохранить",error);Button(card,"Повторить",retry,"primary");}
        void ShowRules()
        {
            var card=Dialog("Правила");var scroll=new ScrollView();scroll.style.maxHeight=550;card.Add(scroll);
            string[] rules={"Ставка на одного победителя до начала боя. Выплата включает поставленные монеты; при ничьей ставка возвращается.",
                "Коэффициенты оцениваются скрытыми боями того же состава. Показанный бой использует отдельный сид.",
                "После затишья шары плавно сближаются. С 90-й секунды сужается безопасный круг; снаружи наносится урон. С 120-й секунды опасна вся арена.",
                "Меч: +1 урон за попадание. Копьё: 2 урона, +0,5r к длине. Кинжал: 2 урона, плавный разгон без потолка.",
                "Коса: +1/с яда на цели за удар. Безоружный: урон от текущей скорости, за попадания растёт разгон.",
                "Щит возвращает 100% урона, сам не атакует. Снаряды меняют владельца. Успешная отражённая атака увеличивает ширину на 0,25r. Волны проходят сквозь щит.",
                "Лук выпускает очередь с интервалом 0,06 с, арбалет — одновременный веер. Попадание добавляет стрелу следующему выстрелу. Перезарядка 1 с; у сюрикена 0,8 с.",
                "Посох: огненный шар каждую секунду; волна наносит 3 урона каждому врагу один раз. Радиус растёт на 0,35 за попадание. Сюрикен: +1 отскок за попадание.",
                "Выбывшие больше не атакуют; уже выпущенные снаряды и наложенный яд остаются. Собственные снаряды не опасны. Усиления сбрасываются между боями.",
                "Битва с ботами: 1–4 противника, 300 HP. Внизу — знаки, плитки или ритм для заработка эссенции. На 20/40/60/80 с магазин; с 90 с сужение. Эссенция и покупки действуют только в этом матче.",
                "При балансе меньше 10 монет и отсутствии незавершённой ставки можно бесплатно восстановить баланс до 1000."};
            foreach(string rule in rules)Text(scroll,rule,"rule");Button(card,"Закрыть",CloseOverlay,"primary");
        }
        public void UpdateSafeArea()
        {
            Rect safe=Screen.safeArea;var size=new Vector2Int(Screen.width,Screen.height);if(safe==previousSafe&&size==previousSize)return;
            previousSafe=safe;previousSize=size;float scale=720f/Mathf.Max(1,Screen.width);if(app==null)return;
            app.style.paddingTop=14+(Screen.height-safe.yMax)*scale;app.style.paddingBottom=10+safe.yMin*scale;
            app.style.paddingLeft=22+safe.xMin*scale;app.style.paddingRight=22+(Screen.width-safe.xMax)*scale;
        }
    }
}
