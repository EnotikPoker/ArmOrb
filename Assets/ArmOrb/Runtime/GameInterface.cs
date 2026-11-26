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
        bool quoteRefreshPending;
        public GameInterface(ArmOrbGame game, VisualElement target = null)
        { this.game=game;root=target??game.document.rootVisualElement;root.styleSheets.Add(game.styleSheet);root.style.unityFont=game.uiFont; }
        VisualElement Element(VisualElement parent,string classes=null)
        {var e=new VisualElement();Classes(e,classes);parent.Add(e);return e;}
        Label Text(VisualElement parent,string text,string classes=null)
        {var e=new Label(text);Classes(e,classes);parent.Add(e);return e;}
        Button Button(VisualElement parent,string text,Action action,string classes=null)
        {var e=new Button(action){text=text};Classes(e,classes);parent.Add(e);return e;}
        static void Classes(VisualElement e,string classes){if(classes!=null)foreach(string c in classes.Split(' '))e.AddToClassList(c);}
        static void Selected(VisualElement e,bool selected)=>e.EnableInClassList("selected",selected);
        void NewPage()
        {quoteRefreshPending=false;ResetBuildUi();root.Clear();overlay=null;calculation=null;timer=null;danger=null;sound=null;healthFills.Clear();barrierFills.Clear();speeds.Clear();app=Element(root,"app");previousSize=Vector2Int.zero;UpdateSafeArea();}
        public void ShowSetup()=>ShowHome();
        public void ShowHome()
        {
            NewPage();
            if(game.AtMainMenu){ShowMainMenu();return;}
            var header=Element(app,"row header");
            IconButton(header,UiSymbol.Back,"Назад",game.OpenMainMenu,"back-main");
            Text(header,game.Mode==GameMode.Bots?"Битва с ботами":game.Mode==GameMode.Bets?"Ставки":game.Mode==GameMode.Runs?"Забег":game.Mode==GameMode.Collection?"Коллекция":"Тест","page-title");
            IconButton(header,UiSymbol.Settings,"",ShowSettings,"settings");
            var scroll=new ScrollView(ScrollViewMode.Vertical);scroll.AddToClassList("setup-scroll");app.Add(scroll);
            var content=Element(scroll.contentContainer,"home-content");
            if(!string.IsNullOrEmpty(game.Error))Text(content,game.Error,"error");
            if(game.Mode==GameMode.Bets)
            {
                CoinBalance(content);
                if(!string.IsNullOrEmpty(game.Wallet.RecoveryMessage))Text(content,game.Wallet.RecoveryMessage,"small muted");
            }
            if(game.ShowTest)TestSetup(content);else if(game.Mode==GameMode.Bots)BotSetup(content);else if(game.Mode==GameMode.Runs)RunSetup(content);else if(game.Mode==GameMode.Collection)CollectionSetup(content);else BetSetup(content);
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
                ChoiceField(row,"",names,game.TestFight.weapons[i],selected=>{game.TestFight.weapons[id]=selected;ShowHome();},"weapon-"+i,true);
                NumberField(row,"HP",()=>game.TestFight.health[id],1,99999,n=>{game.TestFight.health[id]=(int)n;ShowHome();},"health-"+i,"compact-hp");
                Text(card,game.weapons[game.TestFight.weapons[i]].description,"small muted");
            }
            var options=Element(content,"setup-options");Text(options,"Арена","setting-label");
            ChoiceField(options,"",ArmOrbGame.SizeNames,(int)game.Options.arena,selected=>{game.Options.arena=(ArenaSize)selected;game.Options.Save();ShowHome();},"arena-size");
            Toggle(options,"Гравитация",game.Options.gravity,v=>game.Options.gravity=v,"gravity");
            Toggle(options,"Сближение после затишья",game.Options.activeMotion,v=>game.Options.activeMotion=v,"active-motion");
            Toggle(options,"Опасная зона с 90-й секунды",game.Options.danger,v=>game.Options.danger=v,"danger-toggle");
            Text(options,"Скорость","setting-label");AddSpeedControl(options);
            Button(content,"Начать",()=>game.StartFight(),"primary").name="start-fight";
        }
        void Toggle(VisualElement parent,string label,bool value,Action<bool> action,string name)
        {
            var button=Button(parent,"",()=>{},"option-toggle");button.name=name;
            Text(button,label,"toggle-label");var state=Text(button,value?"Вкл":"Выкл","toggle-state");Selected(button,value);IgnoreChildren(button);
            button.clicked+=()=>{value=!value;action(value);game.Options.Save();state.text=value?"Вкл":"Выкл";Selected(button,value);};
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
            if(game.Wallet.Data.balance<10)Button(content,"Получить 1000 жетонов",game.Refill,"secondary").name="refill-coins";
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
            Action refreshNumber=()=>{};
            var input=NumberField(content,"Ставка",()=>stake,0,1000000000000L,n=>{stake=n;refreshNumber();},"stake-amount","stake-input");
            var payment=Text(content,"","payout");
            Button confirm=null;
            void RefreshPayout()
            {
                bool valid=game.Quote!=null&&stake>=10&&stake%10==0&&stake<=game.Wallet.Data.balance;
                try
                {
                    payment.text=game.Quote==null?"Расчёт коэффициентов…":$"Выплата при победе: {WalletStore.Payout(Math.Max(0,stake),game.Quote.Hundredths[betSelection]):N0} жетонов";
                }
                catch(OverflowException){valid=false;payment.text="Уменьши сумму ставки.";}
                confirm?.SetEnabled(valid);
            }
            refreshNumber=RefreshPayout;
            var amounts=Element(content,"row stake-buttons");
            foreach(long amount in new long[]{10,50,100,0})
            {
                long selected=amount;Button(amounts,amount==0?"Все":amount.ToString(),()=>{stake=selected==0?game.Wallet.Data.balance/10*10:selected;input.text=stake.ToString("N0");RefreshPayout();},"stake-button");
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
            NewPage();app.AddToClassList("standard-fight");hp.Clear();stats.Clear();reload.Clear();
            var header=Element(app,"row header");IconButton(header,UiSymbol.Back,"",game.BackToSetup,"fight-menu");timer=Text(header,"00:00","timer");IconButton(header,UiSymbol.Pause,"",game.TogglePause,"pause");
            var participants=Element(app,"fight-participants");
            for(int i=0;i<game.CurrentFight.Count;i++)AddParticipant(participants,i,false);
            danger=Text(app,"","danger-label");
            var holder=Element(app,"arena-holder");var field=new Image{image=game.ArenaTexture,scaleMode=ScaleMode.StretchToFill,pickingMode=PickingMode.Ignore,name="arena"};field.AddToClassList("arena");holder.Add(field);
            holder.RegisterCallback<GeometryChangedEvent>(e=>{float size=Mathf.Max(1,Mathf.Min(e.newRect.width,e.newRect.height));field.style.width=size;field.style.height=size;});
            var statList=Element(app,"fight-stats");for(int i=0;i<game.CurrentFight.Count;i++){var s=Text(statList,"","fighter-stat");s.style.color=game.ParticipantColor(i)*new Color(1,1,1,1);stats.Add(s);}
            AddSpeedControl(app);var footer=Element(app,"row footer");
            Text(footer,game.IsBetFight?$"Ставка {game.LastStake} · #{game.LastBetSelection+1}":"Тест","small muted");IconButton(footer,UiSymbol.Settings,"",ShowSettings,"fight-settings");RefreshFight();
        }
        public void RefreshFight()
        {
            if(game.Simulation==null||timer==null||hp.Count!=game.Simulation.Orbs.Length)return;
            RefreshBuildFight();
            var sim=game.Simulation;SetLabel(timer,$"{(int)sim.Elapsed/60:00}:{(int)sim.Elapsed%60:00}");
            for(int i=0;i<hp.Count;i++)
            {
                var orb=sim.Orbs[i];SetLabel(hp[i],orb.Health.IsDead?"Выбыл":(game.IsBotFight?$"{Mathf.CeilToInt(orb.Health.Current)}/{orb.Health.Maximum:0}"+(sim.Builds[i].BarrierTotal>0?$" · Б {sim.Builds[i].BarrierTotal:0}":""):Mathf.CeilToInt(orb.Health.Current)+" HP"));
                RefreshParticipant(i,orb);
                SetLabel(stats[i],orb.Health.IsDead?$"#{i+1} — выбыл":$"#{i+1} · {orb.Weapon.Stat}"+(orb.Health.PoisonPerSecond>0?$" · Яд −{orb.Health.PoisonPerSecond:0.#}/с":""));
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
        public void RefreshQuote()
        {if(overlay!=null){quoteRefreshPending=true;return;}ShowHome();}
        void CloseOverlay()
        {
            overlay?.RemoveFromHierarchy();overlay=null;
            if(quoteRefreshPending)root.schedule.Execute(()=>
            {
                if(!quoteRefreshPending||overlay!=null)return;
                quoteRefreshPending=false;
                if(!game.IsInFight&&!game.AtMainMenu&&game.Mode==GameMode.Bets)ShowHome();
            });
        }
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
                Text(card,winner<0?$"Возврат: {game.LastPayment:N0} жетонов":game.LastPayment>0?$"Выплата: {game.LastPayment:N0} жетонов":"Ставка проиграла","rule");Text(card,$"Баланс: {game.Wallet.Data.balance:N0}","rule");
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
            string[] rules={"Ставка на одного победителя до начала боя. Выплата включает поставленные жетоны; при ничьей ставка возвращается.",
                "Коэффициенты оцениваются скрытыми боями того же состава. Показанный бой использует отдельный сид.",
                "В ставках и тесте после затишья шары плавно сближаются. При включённой опасной зоне с 90-й секунды сужается безопасный круг; снаружи наносится урон. С 120-й секунды опасна вся арена.",
                "Меч: +1 урон за попадание. Копьё: 2 урона, +0,5r к длине. Кинжал: 2 урона, плавный разгон без потолка.",
                "Коса: +1/с яда на цели за удар. Безоружный: урон от текущей скорости, за попадания растёт разгон.",
                "Щит возвращает 100% урона, сам не атакует. Снаряды меняют владельца. Успешная отражённая атака увеличивает ширину на 0,25r. Волны проходят сквозь щит.",
                "Лук выпускает очередь с интервалом 0,06 с, арбалет — одновременный веер. Попадание добавляет стрелу следующему выстрелу. Перезарядка 1 с; у сюрикена 0,8 с.",
                "Посох: огненный шар каждую секунду; волна наносит 3 урона каждому врагу один раз. Радиус растёт на 0,35 за попадание. Сюрикен: +1 отскок за попадание.",
                "Выбывшие больше не атакуют; уже выпущенные снаряды и наложенный яд остаются. Собственные снаряды не опасны. Усиления сбрасываются между боями.",
                "Битва с ботами: 1–4 противника, 300 HP. Стартовый навык и новый выбор каждые 20 с. Перед боем выбирается активка: лечение или барьер. На выбор навыка 8 с; бой продолжается. Без сужения. Через 5 минут победитель определяется по доле оставшегося HP. Улучшения действуют только в этом матче.",
                "При балансе меньше 10 жетонов и отсутствии незавершённой ставки можно бесплатно восстановить баланс до 1000 жетонов."};
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
