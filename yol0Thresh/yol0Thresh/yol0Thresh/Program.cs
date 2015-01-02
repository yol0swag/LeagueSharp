using System;
using System.Collections.Generic;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;
using Color = System.Drawing.Color;

namespace yol0Thresh
{
    internal class Program
    {
        private const string Revision = "1.0.0.2";
        private static readonly Obj_AI_Hero Player = ObjectManager.Player;

        private static readonly Spell Q = new Spell(SpellSlot.Q, 1075);
        private static readonly Spell W = new Spell(SpellSlot.W, 950);
        private static readonly Spell E = new Spell(SpellSlot.E, 500);
        private static readonly Spell R = new Spell(SpellSlot.R, 400);

        private static Menu _config;

        private static int _qTick;
        private static int _hookTick;
        private static Obj_AI_Base _hookedUnit;


        //  private static List<Vector3> _escapeSpots = new List<Vector3>();
        private static readonly List<GameObject> SoulList = new List<GameObject>();

        private static Obj_AI_Hero CurrentTarget
        {
            get
            {
                if (Hud.SelectedUnit != null && Hud.SelectedUnit is Obj_AI_Hero && Hud.SelectedUnit.Team != Player.Team)
                    return (Obj_AI_Hero) Hud.SelectedUnit;
                if (TargetSelector.GetSelectedTarget() != null)
                    return TargetSelector.GetSelectedTarget();
                return TargetSelector.GetTarget(QRange + 175, TargetSelector.DamageType.Physical);
            }
        }

        private static float QRange
        {
            get { return _config.SubMenu("Misc").Item("qRange").GetValue<Slider>().Value; }
        }

        public static void Main(string[] args)
        {
            CustomEvents.Game.OnGameLoad += OnLoad;
        }

        private static void OnLoad(EventArgs args)
        {
            if (Player.ChampionName != "Thresh")
                return;

            Q.SetSkillshot(0.5f, 70, 1900, true, SkillshotType.SkillshotLine);
            W.SetSkillshot(0f, 200, 1750, false, SkillshotType.SkillshotCircle);
            E.SetSkillshot(0.3f, 60, float.MaxValue, false, SkillshotType.SkillshotLine);

            _config = new Menu("yol0 Thresh", "Thresh", true);
            _config.AddSubMenu(new Menu("Orbwalking", "Orbwalking"));
            xSLxOrbwalker.AddToMenu(_config.SubMenu("Orbwalking"));

            _config.AddSubMenu(new Menu("Target Selector", "Target Selector"));
            TargetSelector.AddToMenu(_config.SubMenu("Target Selector"));

            _config.AddSubMenu(new Menu("Combo Settings", "Combo"));
            _config.SubMenu("Combo").AddItem(new MenuItem("useQ1", "Use Q1").SetValue(true));
            _config.SubMenu("Combo").AddItem(new MenuItem("useQ2", "Use Q2").SetValue(true));
            _config.SubMenu("Combo").AddItem(new MenuItem("useE", "Use Flay").SetValue(true));
            _config.SubMenu("Combo").AddItem(new MenuItem("useW", "Throw Lantern to Ally").SetValue(true));


            _config.AddSubMenu(new Menu("Harass Settings", "Harass"));
            _config.SubMenu("Harass").AddItem(new MenuItem("useQ1", "Use Q1").SetValue(true));
            _config.SubMenu("Harass").AddItem(new MenuItem("useE", "Use Flay").SetValue(true));
            _config.SubMenu("Harass").AddItem(new MenuItem("manaPercent", "Mana %").SetValue(new Slider(40, 1)));

            _config.AddSubMenu(new Menu("Flay Settings", "Flay"));
            _config.SubMenu("Flay")
                .AddItem(new MenuItem("pullEnemy", "Pull Enemy").SetValue(new KeyBind(90, KeyBindType.Press)));
            _config.SubMenu("Flay")
                .AddItem(new MenuItem("pushEnemy", "Push Enemy").SetValue(new KeyBind(88, KeyBindType.Press)));
            _config.SubMenu("Flay").AddSubMenu(new Menu("Per-Enemy Settings", "ActionToTake"));
            foreach (Obj_AI_Hero enemy in ObjectManager.Get<Obj_AI_Hero>().Where(unit => unit.Team != Player.Team))
            {
                _config.SubMenu("Flay")
                    .SubMenu("ActionToTake")
                    .AddItem(
                        new MenuItem(enemy.ChampionName, enemy.ChampionName).SetValue(
                            new StringList(new[] {"Pull", "Push"})));
            }

            _config.AddSubMenu(new Menu("Lantern Settings", "Lantern"));
            _config.SubMenu("Lantern").AddItem(new MenuItem("useW", "Throw to Ally").SetValue(true));
            _config.SubMenu("Lantern")
                .AddItem(new MenuItem("numEnemies", "Throw if # Enemies").SetValue(new Slider(2, 1, 5)));
            _config.SubMenu("Lantern").AddItem(new MenuItem("useWCC", "Throw to CC'd Ally").SetValue(true));

            _config.AddSubMenu(new Menu("Box Settings", "Box"));
            _config.SubMenu("Box").AddItem(new MenuItem("useR", "Auto Use Box").SetValue(true));
            _config.SubMenu("Box").AddItem(new MenuItem("minEnemies", "Minimum Enemies").SetValue(new Slider(3, 1, 5)));

            _config.AddSubMenu(new Menu("Misc Settings", "Misc"));
            _config.SubMenu("Misc").AddItem(new MenuItem("qRange", "Q Range").SetValue(new Slider(1075, 700, 1075)));
            _config.SubMenu("Misc")
                .AddItem(
                    new MenuItem("qHitChance", "Q HitChance").SetValue(
                        new StringList(new[] {"Very High", "High", "Medium", "Low"}, 1)));
            _config.SubMenu("Misc").AddItem(new MenuItem("dashes", "Flay Dash Gapclosers").SetValue(true));
            _config.SubMenu("Misc").AddItem(new MenuItem("packetCasting", "Use Packet Casting").SetValue(false));

            _config.SubMenu("Misc").AddSubMenu(new Menu("Gapclosers", "Gapclosers"));
            if (ObjectManager.Get<Obj_AI_Hero>().Any(unit => unit.Team != Player.Team && unit.ChampionName == "Rengar"))
            {
                _config.SubMenu("Misc")
                    .SubMenu("Gapclosers")
                    .AddItem(new MenuItem("rengarleap", "Rengar - Unseen Predator").SetValue(true));
            }
            foreach (Gapcloser spell in AntiGapcloser.Spells)
            {
                foreach (Obj_AI_Hero enemy in ObjectManager.Get<Obj_AI_Hero>().Where(unit => unit.Team != Player.Team))
                {
                    if (spell.ChampionName == enemy.ChampionName)
                    {
                        _config.SubMenu("Misc")
                            .SubMenu("Gapclosers")
                            .AddItem(
                                new MenuItem(spell.SpellName, spell.ChampionName + " - " + spell.SpellName).SetValue(
                                    true));
                    }
                }
            }

            _config.SubMenu("Misc").AddSubMenu(new Menu("Interruptble Spells", "InterruptSpells"));
            foreach (InterruptableSpell spell in Interrupter.Spells)
            {
                foreach (Obj_AI_Hero enemy in ObjectManager.Get<Obj_AI_Hero>().Where(unit => unit.Team != Player.Team))
                {
                    if (spell.ChampionName == enemy.ChampionName)
                    {
                        _config.SubMenu("Misc")
                            .SubMenu("InterruptSpells")
                            .AddSubMenu(new Menu(enemy.ChampionName + " - " + spell.SpellName, spell.SpellName));
                        _config.SubMenu("Misc")
                            .SubMenu("InterruptSpells")
                            .SubMenu(spell.SpellName)
                            .AddItem(new MenuItem("enabled", "Enabled").SetValue(true));
                        _config.SubMenu("Misc")
                            .SubMenu("InterruptSpells")
                            .SubMenu(spell.SpellName)
                            .AddItem(new MenuItem("useE", "Interrupt with Flay").SetValue(true));
                        _config.SubMenu("Misc")
                            .SubMenu("InterruptSpells")
                            .SubMenu(spell.SpellName)
                            .AddItem(new MenuItem("useQ", "Interrupt with Hook").SetValue(true));
                    }
                }
            }

            _config.AddSubMenu(new Menu("KS Settings", "KS"));
            _config.SubMenu("KS").AddItem(new MenuItem("ksQ", "KS with Q").SetValue(false));
            _config.SubMenu("KS").AddItem(new MenuItem("ksE", "KS with E").SetValue(false));

            _config.AddSubMenu(new Menu("Draw Settings", "Draw"));
            _config.SubMenu("Draw")
                .AddItem(new MenuItem("drawQMax", "Draw Q Max Range").SetValue(new Circle(true, Color.Red)));
            _config.SubMenu("Draw")
                .AddItem(new MenuItem("drawQEffective", "Draw Q Effective").SetValue(new Circle(true, Color.Blue)));
            _config.SubMenu("Draw")
                .AddItem(new MenuItem("drawW", "Draw W Range").SetValue(new Circle(false, Color.Green)));
            _config.SubMenu("Draw")
                .AddItem(new MenuItem("drawE", "Draw E Range").SetValue(new Circle(false, Color.Aqua)));
            _config.SubMenu("Draw").AddItem(new MenuItem("drawQCol", "Draw Q Line").SetValue(true));
            _config.SubMenu("Draw").AddItem(new MenuItem("drawTargetC", "Draw Target (Circle)").SetValue(true));
            _config.SubMenu("Draw").AddItem(new MenuItem("drawTargetT", "Draw Target (Text)").SetValue(true));
            _config.SubMenu("Draw")
                .AddItem(new MenuItem("drawSouls", "Draw Circle on Souls").SetValue(new Circle(true, Color.DeepSkyBlue)));
            _config.AddToMainMenu();
            Obj_AI_Base.OnProcessSpellCast += OnProcessSpell;
            Obj_AI_Base.OnPlayAnimation += OnAnimation;
            Drawing.OnDraw += OnDraw;
            Game.OnGameUpdate += OnGameUpdate;
            GameObject.OnCreate += OnCreateObj;
            AntiGapcloser.OnEnemyGapcloser += OnEnemyGapCloser;
            Interrupter.OnPossibleToInterrupt += OnPossibleToInterrupt;

            Game.PrintChat("<font color=\"#FF0F0\">yol0 Thresh v" + Revision + " loaded!</font>");
        }

        public static void OnGameUpdate(EventArgs args)
        {
            AutoBox();
            Ks();
            Lantern();
            UpdateSouls();
            UpdateBuffs();

            /*if (Config.SubMenu("Misc").Item("dashes").GetValue<bool>())
            {
                foreach (Obj_AI_Hero enemy in ObjectManager.Get<Obj_AI_Hero>().Where(unit => unit.Team != Player.Team))
                {
                    var pIn = new PredictionInput
                    {
                        Unit = enemy,
                        Delay = 300,
                        Aoe = false,
                        Collision = false,
                        Radius = 200,
                        Range = _E.Range,
                        Speed = 2000,
                        Type = SkillshotType.SkillshotLine,
                        RangeCheckFrom = Player.ServerPosition,
                    };
                    PredictionOutput pOut = Prediction.GetPrediction(pIn);
                    float pX = Player.Position.X + (Player.Position.X - pOut.CastPosition.X);
                    float pY = Player.Position.Y + (Player.Position.Y - pOut.CastPosition.Y);
                    if (pOut.Hitchance == HitChance.Dashing && Player.Distance(pOut.CastPosition) < 125 && _E.IsReady())
                    {
                        _E.Cast(new Vector2(pX, pY), PacketCasting());
                    }
                }
            }*/

            if (xSLxOrbwalker.CurrentMode == xSLxOrbwalker.Mode.Combo)
                Combo();

            if (xSLxOrbwalker.CurrentMode == xSLxOrbwalker.Mode.Harass)
                Harass();

            if (_config.SubMenu("Flay").Item("pullEnemy").GetValue<KeyBind>().Active)
            {
                Obj_AI_Hero target = TargetSelector.GetTarget(E.Range, TargetSelector.DamageType.Physical);
                if (target != null)
                    PullFlay(target);
            }

            if (_config.SubMenu("Flay").Item("pushEnemy").GetValue<KeyBind>().Active)
            {
                Obj_AI_Hero target = TargetSelector.GetTarget(E.Range, TargetSelector.DamageType.Physical);
                if (target != null)
                    PushFlay(target);
            }
        }

        public static void OnDraw(EventArgs args)
        {
            if (_config.SubMenu("Draw").Item("drawQMax").GetValue<Circle>().Active && !Player.IsDead)
            {
                Utility.DrawCircle(Player.Position, Q.Range,
                    _config.SubMenu("Draw").Item("drawQMax").GetValue<Circle>().Color);
            }

            if (_config.SubMenu("Draw").Item("drawQEffective").GetValue<Circle>().Active && !Player.IsDead)
            {
                Utility.DrawCircle(Player.Position, QRange,
                    _config.SubMenu("Draw").Item("drawQEffective").GetValue<Circle>().Color);
            }

            if (_config.SubMenu("Draw").Item("drawW").GetValue<Circle>().Active && !Player.IsDead)
            {
                Utility.DrawCircle(Player.Position, W.Range,
                    _config.SubMenu("Draw").Item("drawW").GetValue<Circle>().Color);
            }

            if (_config.SubMenu("Draw").Item("drawE").GetValue<Circle>().Active && !Player.IsDead)
            {
                Utility.DrawCircle(Player.Position, E.Range,
                    _config.SubMenu("Draw").Item("drawE").GetValue<Circle>().Color);
            }

            if (_config.SubMenu("Draw").Item("drawQCol").GetValue<bool>() && !Player.IsDead)
            {
                if (Player.Distance(CurrentTarget) < QRange + 200)
                {
                    Vector2 playerPos = Drawing.WorldToScreen(Player.Position);
                    Vector2 targetPos = Drawing.WorldToScreen(CurrentTarget.Position);
                    Drawing.DrawLine(playerPos, targetPos, 4,
                        Q.GetPrediction(CurrentTarget, overrideRange: QRange).Hitchance < GetSelectedHitChance()
                            ? Color.Red
                            : Color.Green);
                }
            }

            if (_config.SubMenu("Draw").Item("drawTargetC").GetValue<bool>() && CurrentTarget.IsVisible &&
                !CurrentTarget.IsDead)
            {
                Utility.DrawCircle(CurrentTarget.Position, CurrentTarget.BoundingRadius + 10, Color.Red);
                Utility.DrawCircle(CurrentTarget.Position, CurrentTarget.BoundingRadius + 25, Color.Red);
                Utility.DrawCircle(CurrentTarget.Position, CurrentTarget.BoundingRadius + 45, Color.Red);
            }

            if (_config.SubMenu("Draw").Item("drawTargetT").GetValue<bool>() && !CurrentTarget.IsDead)
            {
                Drawing.DrawText(100, 150, Color.Red, "Current Target: " + CurrentTarget.ChampionName);
            }

            if (_config.SubMenu("Draw").Item("drawSouls").GetValue<Circle>().Active && !Player.IsDead)
            {
                foreach (GameObject soul in SoulList.Where(s => s.IsValid))
                {
                    Utility.DrawCircle(soul.Position, 50,
                        _config.SubMenu("Draw").Item("drawSouls").GetValue<Circle>().Color);
                }
            }
        }

        public static void OnAnimation(GameObject unit, GameObjectPlayAnimationEventArgs args)
        {
            var aiHero = unit as Obj_AI_Hero;
            if (aiHero != null)
            {
                Obj_AI_Hero hero = aiHero;
                if (hero.Team != Player.Team)
                {
                    if (hero.ChampionName == "Rengar" && args.Animation == "Spell5" && Player.Distance(hero) <= 725)
                    {
                        if (E.IsReady() &&
                            _config.SubMenu("Misc").SubMenu("Gapclosers").Item("rengarleap").GetValue<bool>())
                        {
                            E.Cast(aiHero.Position, PacketCasting());
                        }
                    }
                }
            }
        }

        public static void OnProcessSpell(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (sender.IsMe)
            {
                if (args.SData.Name == "ThreshQ")
                {
                    _qTick = Environment.TickCount + 500;
                }
            }
        }

        public static void OnCreateObj(GameObject obj, EventArgs args)
        {
            if (obj.Name.Contains("ChaosMinion") && obj.Team == Player.Team)
            {
                SoulList.Add(obj);
            }
        }

        public static void OnPossibleToInterrupt(Obj_AI_Base unit, InterruptableSpell spell)
        {
            if (
                _config.SubMenu("Misc")
                    .SubMenu("InterruptSpells")
                    .SubMenu(spell.SpellName)
                    .Item("enabled")
                    .GetValue<bool>())
            {
                if (
                    _config.SubMenu("Misc")
                        .SubMenu("InterruptSpells")
                        .SubMenu(spell.SpellName)
                        .Item("useE")
                        .GetValue<bool>() && E.IsReady() &&
                    Player.Distance(unit) < E.Range)
                {
                    if (ShouldPull((Obj_AI_Hero) unit))
                        PullFlay(unit);
                    else
                        PushFlay(unit);
                }
                else if (
                    _config.SubMenu("Misc")
                        .SubMenu("InterruptSpells")
                        .SubMenu(spell.SpellName)
                        .Item("useQ")
                        .GetValue<bool>() && Q.IsReady() &&
                    !Q.GetPrediction(unit).CollisionObjects.Any())
                {
                    Q.Cast(unit, PacketCasting());
                }
            }
        }

        public static void OnEnemyGapCloser(ActiveGapcloser gapcloser)
        {
            if (E.IsReady() &&
                _config.SubMenu("Misc").SubMenu("Gapclosers").Item(gapcloser.SpellName.ToLower()).GetValue<bool>() &&
                Player.Distance(gapcloser.Sender) < E.Range + 100)
            {
                if (Player.Distance(gapcloser.Start) < Player.Distance(gapcloser.End))
                    PullFlay(gapcloser.Sender);
                else
                    PushFlay(gapcloser.Sender);
            }
        }

        private static void UpdateBuffs()
        {
            if (_hookedUnit == null)
            {
                foreach (Obj_AI_Base obj in ObjectManager.Get<Obj_AI_Base>().Where(unit => unit.Team != Player.Team))
                {
                    if (obj.HasBuff("threshqfakeknockup"))
                    {
                        _hookedUnit = obj;
                        _hookTick = Environment.TickCount + 1500;
                        return;
                    }
                }
            }
            _hookTick = 0;
            _hookedUnit = null;
        }

        private static void UpdateSouls()
        {
            foreach (GameObject soul in SoulList.Where(soul => !soul.IsValid))
            {
                SoulList.Remove(soul);
            }
        }

        private static bool ShouldPull(Obj_AI_Hero unit)
        {
            return
                _config.SubMenu("Flay")
                    .SubMenu("ActionToTake")
                    .Item(unit.ChampionName)
                    .GetValue<StringList>()
                    .SelectedIndex == 0;
        }

        private static bool IsFirstQ()
        {
            return Q.Instance.Name == "ThreshQ";
        }

        private static bool IsSecondQ()
        {
            return Q.Instance.Name == "threshqleap";
        }

        private static bool IsImmune(Obj_AI_Base unit)
        {
            return unit.HasBuff("BlackShield") || unit.HasBuff("SivirE") || unit.HasBuff("NocturneShroudofDarkness") ||
                   unit.HasBuff("deathdefiedbuff");
        }

        private static void Ks()
        {
            if (_config.SubMenu("KS").Item("ksE").GetValue<bool>())
            {
                foreach (
                    Obj_AI_Hero enemy in
                        from enemy in
                            ObjectManager.Get<Obj_AI_Hero>().Where(unit => unit.Team != Player.Team && !unit.IsDead)
                        let eDmg = Player.GetSpellDamage(enemy, SpellSlot.E)
                        where eDmg > enemy.Health && Player.Distance(enemy) <= E.Range && E.IsReady()
                        select enemy)
                {
                    PullFlay(enemy);
                    return;
                }
            }

            if (_config.SubMenu("KS").Item("ksQ").GetValue<bool>())
            {
                foreach (
                    Obj_AI_Hero enemy in
                        from enemy in
                            ObjectManager.Get<Obj_AI_Hero>().Where(unit => unit.Team != Player.Team && !unit.IsDead)
                        let qDmg = Player.GetSpellDamage(enemy, SpellSlot.Q)
                        where qDmg > enemy.Health && Player.Distance(enemy) <= QRange && IsFirstQ() && Q.IsReady() &&
                              Q.GetPrediction(enemy, overrideRange: QRange).Hitchance >= GetSelectedHitChance()
                        select enemy)
                {
                    Q.Cast(enemy);
                    return;
                }
            }
        }

        private static bool PacketCasting()
        {
            return _config.SubMenu("Misc").Item("packetCasting").GetValue<bool>();
        }

        private static HitChance GetSelectedHitChance()
        {
            switch (_config.SubMenu("Misc").Item("qHitChance").GetValue<StringList>().SelectedIndex)
            {
                case 0:
                    return HitChance.VeryHigh;
                case 1:
                    return HitChance.High;
                case 2:
                    return HitChance.Medium;
                case 3:
                    return HitChance.Low;
            }
            return HitChance.Medium;
        }

        private static void AutoBox()
        {
            if (_config.SubMenu("Box").Item("useR").GetValue<bool>() && R.IsReady() &&
                ObjectManager.Get<Obj_AI_Hero>()
                    .Count(unit => unit.Team != Player.Team && Player.Distance(unit) <= R.Range) >=
                _config.SubMenu("Box").Item("minEnemies").GetValue<Slider>().Value)
            {
                R.Cast(PacketCasting());
            }
        }

        private static void Combo()
        {
            if (_config.SubMenu("Combo").Item("useE").GetValue<bool>() && E.IsReady() &&
                Player.Distance(CurrentTarget) < E.Range &&
                (!Q.IsReady() && Environment.TickCount > _qTick || Q.IsReady() && IsFirstQ()))
            {
                Flay(CurrentTarget);
            }
            else if (_config.SubMenu("Combo").Item("useQ2").GetValue<bool>() && Player.Distance(CurrentTarget) > E.Range &&
                     Q.IsReady() &&
                     Environment.TickCount >= _hookTick - 500 && IsSecondQ() &&
                     ObjectManager.Get<Obj_AI_Hero>().FirstOrDefault(unit => unit.HasBuff("ThreshQ")) != null)
            {
                Q.Cast(PacketCasting());
            }
            else if (_config.SubMenu("Combo").Item("useQ2").GetValue<bool>() &&
                     _config.SubMenu("Combo").Item("useE").GetValue<bool>() && Q.IsReady() &&
                     E.IsReady() &&
                     ObjectManager.Get<Obj_AI_Minion>()
                         .FirstOrDefault(unit => unit.HasBuff("ThreshQ") && unit.Distance(CurrentTarget) <= E.Range) !=
                     null && IsSecondQ())
            {
                Q.Cast(PacketCasting());
            }

            if (_config.SubMenu("Combo").Item("useQ1").GetValue<bool>() && Q.IsReady() && IsFirstQ() &&
                !IsImmune(CurrentTarget))
            {
                Q.CastIfHitchanceEquals(CurrentTarget, GetSelectedHitChance(), PacketCasting());
                /*if (_Q.GetPrediction(currentTarget, false, qRange).Hitchance >= GetSelectedHitChance())
                {
                    if (currentTarget.HasBuffOfType(BuffType.Slow))
                        _Q.Cast(currentTarget.ServerPosition, PacketCasting());
                    else
                        _Q.Cast(currentTarget, PacketCasting());
                }*/
            }

            if (_config.SubMenu("Lantern").Item("useW").GetValue<bool>() && W.IsReady() &&
                ObjectManager.Get<Obj_AI_Hero>().FirstOrDefault(unit => unit.HasBuff("ThreshQ")) != null)
            {
                Obj_AI_Hero nearAlly = GetNearAlly();
                if (nearAlly != null)
                {
                    W.Cast(nearAlly, PacketCasting());
                }
            }
        }

        private static void Harass()
        {
            float percentManaAfterQ = 100*((Player.Mana - Q.Instance.ManaCost)/Player.MaxMana);
            float percentManaAfterE = 100*((Player.Mana - E.Instance.ManaCost)/Player.MaxMana);
            int minPercentMana = _config.SubMenu("Harass").Item("manaPercent").GetValue<Slider>().Value;

            if (_config.SubMenu("Harass").Item("useQ1").GetValue<bool>() && Q.IsReady() && IsFirstQ() &&
                !IsImmune(CurrentTarget) && percentManaAfterQ >= minPercentMana)
            {
                if (Q.GetPrediction(CurrentTarget, false, QRange).Hitchance >= GetSelectedHitChance())
                {
                    Q.Cast(CurrentTarget, PacketCasting());
                }
            }
            else if (_config.SubMenu("Harass").Item("useE").GetValue<bool>() && !IsImmune(CurrentTarget) && E.IsReady() &&
                     Player.Distance(CurrentTarget) < E.Range && percentManaAfterE >= minPercentMana)
            {
                Flay(CurrentTarget);
            }
        }

        private static void Lantern()
        {
            if (_config.SubMenu("Lantern").Item("useWCC").GetValue<bool>() && GetCcAlly() != null && W.IsReady())
            {
                W.Cast(GetCcAlly(), PacketCasting());
                return;
            }

            if (_config.SubMenu("Lantern").Item("useW").GetValue<bool>() && GetLowAlly() != null && W.IsReady())
            {
                if (GetLowAlly().Position.CountEnemysInRange(950) >=
                    _config.SubMenu("Lantern").Item("numEnemies").GetValue<Slider>().Value)
                {
                    W.Cast(GetLowAlly(), PacketCasting());
                }
            }
        }

        private static Obj_AI_Hero GetCcAlly()
        {
            return
                ObjectManager.Get<Obj_AI_Hero>()
                    .Where(
                        unit =>
                            !unit.IsMe && unit.Team == Player.Team && !unit.IsDead &&
                            Player.Distance(unit) <= W.Range + 200)
                    .FirstOrDefault(
                        ally =>
                            ally.HasBuffOfType(BuffType.Charm) || ally.HasBuffOfType(BuffType.CombatDehancer) ||
                            ally.HasBuffOfType(BuffType.Fear) || ally.HasBuffOfType(BuffType.Knockback) ||
                            ally.HasBuffOfType(BuffType.Knockup) || ally.HasBuffOfType(BuffType.Polymorph) ||
                            ally.HasBuffOfType(BuffType.Snare) || ally.HasBuffOfType(BuffType.Stun) ||
                            ally.HasBuffOfType(BuffType.Suppression) || ally.HasBuffOfType(BuffType.Taunt));
        }

        private static Obj_AI_Hero GetLowAlly()
        {
            Obj_AI_Hero lowAlly = null;
            foreach (
                Obj_AI_Hero ally in
                    ObjectManager.Get<Obj_AI_Hero>()
                        .Where(
                            unit => unit.Team == Player.Team && !unit.IsDead && Player.Distance(unit) <= W.Range + 200)
                )
            {
                if (lowAlly == null)
                    lowAlly = ally;
                else if (!lowAlly.IsDead && ally.Health/ally.MaxHealth < lowAlly.Health/lowAlly.MaxHealth)
                    lowAlly = ally;
            }
            return lowAlly;
        }

        private static Obj_AI_Hero GetNearAlly()
        {
            if (Hud.SelectedUnit != null && Hud.SelectedUnit is Obj_AI_Hero && Hud.SelectedUnit.Team == Player.Team &&
                Player.Distance(Hud.SelectedUnit.Position) <= W.Range + 200)
            {
                return (Obj_AI_Hero) Hud.SelectedUnit;
            }

            Obj_AI_Hero nearAlly = null;
            foreach (
                Obj_AI_Hero ally in
                    ObjectManager.Get<Obj_AI_Hero>()
                        .Where(
                            unit => unit.Team == Player.Team && !unit.IsDead && Player.Distance(unit) <= W.Range + 200)
                )
            {
                if (nearAlly == null)
                    nearAlly = ally;
                else if (!nearAlly.IsDead && Player.Distance(ally) < Player.Distance(nearAlly))
                    nearAlly = ally;
            }
            return nearAlly;
        }

        private static void PushFlay(Obj_AI_Base unit)
        {
            if (Player.Distance(unit) <= E.Range)
            {
                E.Cast(unit.ServerPosition, PacketCasting());
            }
        }

        private static void PullFlay(Obj_AI_Base unit)
        {
            if (Player.Distance(unit) <= E.Range)
            {
                float pX = Player.Position.X + (Player.Position.X - unit.Position.X);
                float pY = Player.Position.Y + (Player.Position.Y - unit.Position.Y);
                E.Cast(new Vector2(pX, pY), PacketCasting());
            }
        }

        private static void Flay(Obj_AI_Hero unit)
        {
            if (ShouldPull(unit))
            {
                PullFlay(unit);
            }
            else
            {
                PushFlay(unit);
            }
        }
    }
}