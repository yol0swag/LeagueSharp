using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;
using LX_Orbwalker;
using SharpDX;
using Color = System.Drawing.Color;

namespace yol0Draven
{
    internal class Program
    {

        private struct Reticle
        {
            public Reticle(GameObject obj, long create, long expire)
            {
                gameobj = obj;
                CreateTime = create;
                ExpireTime = expire;
            }
            public GameObject gameobj;
            public long CreateTime;
            public long ExpireTime;
        };

        private const string Revision = "1.0.0.1";
        private static Obj_AI_Hero Player = ObjectManager.Player;
        private static Orbwalking.Orbwalker orbwalker;

        private static bool UsingLXOrbwalker;
        private static Menu Config;

        private static Spell _Q = new Spell(SpellSlot.Q, 550);
        private static Spell _W = new Spell(SpellSlot.W, 400);
        private static Spell _E = new Spell(SpellSlot.E, 1050);
        private static Spell _R = new Spell(SpellSlot.R);

        private static List<Reticle> Reticles = new List<Reticle>();

        private static int NumAxes;
        private static bool AxesActive;

        private static bool RBuff;

        private static Vector3 _orbWalkPos;

        public static void Main(string[] args)
        {
            CustomEvents.Game.OnGameLoad += OnLoad;
        }

        private static void OnLoad(EventArgs args)
        {
            if (Player.ChampionName != "Draven")
                return;


            Config = new Menu("yol0 Draven", "Draven", true);
            Config.AddSubMenu(new Menu("Orbwalker", "Orbwalker"));
            Config.SubMenu("Orbwalker").AddItem(new MenuItem("SelectedOrbwalker", "Orbwalker").SetValue(new StringList(new[] {"Default", "LX-Orbwalker"})));
            if (Config.SubMenu("Orbwalker").Item("SelectedOrbwalker").GetValue<StringList>().SelectedIndex == 0)
            {
                orbwalker = new Orbwalking.Orbwalker(Config.SubMenu("Orbwalker"));
                UsingLXOrbwalker = false;
            }
            else if (Config.SubMenu("Orbwalker").Item("SelectedOrbwalker").GetValue<StringList>().SelectedIndex == 1)
            {
                orbwalker = null;
                LXOrbwalker.AddToMenu(Config.SubMenu("Orbwalker"));
                UsingLXOrbwalker = true;
            }

            var tsMenu = new Menu("Target Selector", "Target Selector");
            SimpleTs.AddToMenu(tsMenu);
            Config.AddSubMenu(tsMenu);
            Config.AddToMainMenu();

            Config.AddSubMenu(new Menu("Reticle Settings", "Reticle"));
            Config.AddSubMenu(new Menu("Combo Settings", "Combo"));
            Config.AddSubMenu(new Menu("Harass Settings", "Harass"));
            Config.AddSubMenu(new Menu("Farm Settings", "Farm"));
            Config.AddSubMenu(new Menu("LaneClear Settings", "LaneClear"));
            Config.AddSubMenu(new Menu("Misc Settings", "Misc"));
            Config.AddSubMenu(new Menu("KS Settings", "KS"));
            Config.AddSubMenu(new Menu("Draw Settings", "Draw"));

            Config.SubMenu("Reticle").AddItem(new MenuItem("Mode", "Mode").SetValue(new StringList(new[] {"Mouse", "Player"})));
            Config.SubMenu("Reticle").AddItem(new MenuItem("MouseRadius", "Mouse Radius").SetValue(new Slider(500, 100, 1000)));
            Config.SubMenu("Reticle").AddItem(new MenuItem("PlayerRadius", "Player Radius").SetValue(new Slider(50)));

            Config.SubMenu("Combo").AddItem(new MenuItem("UseQ", "Use Q").SetValue(true));
            Config.SubMenu("Combo").AddItem(new MenuItem("UseW", "Use W").SetValue(true));
            Config.SubMenu("Combo").AddItem(new MenuItem("UseE", "Use E").SetValue(true));
            Config.SubMenu("Combo").AddItem(new MenuItem("UseR", "Use R").SetValue(true));

            Config.SubMenu("Harass").AddItem(new MenuItem("UseQ", "Use Q").SetValue(true));
            Config.SubMenu("Harass").AddItem(new MenuItem("UseW", "Use W").SetValue(false));
            Config.SubMenu("Harass").AddItem(new MenuItem("UseE", "Use E").SetValue(true));

            Config.SubMenu("Farm").AddItem(new MenuItem("UseQ", "Use Q").SetValue(true));
            Config.SubMenu("Farm").AddItem(new MenuItem("UseW", "Use W").SetValue(false));

            Config.SubMenu("LaneClear").AddItem(new MenuItem("UseQ", "Use Q").SetValue(true));
            Config.SubMenu("LaneClear").AddItem(new MenuItem("UseW", "Use W").SetValue(false));

            Config.SubMenu("Misc").AddItem(new MenuItem("MinERange", "Min Range to use E").SetValue(new Slider(500, 0, 700)));
            Config.SubMenu("Misc").AddItem(new MenuItem("Interrupt", "Interrupt Important Spells").SetValue(true));
            Config.SubMenu("Misc").AddItem(new MenuItem("Gapcloser", "Auto E Gapclosers").SetValue(true));

            Config.SubMenu("KS").AddItem(new MenuItem("ksR", "KS with R").SetValue(true));
            Config.SubMenu("KS").AddItem(new MenuItem("minRange", "Min R KS Range").SetValue(new Slider(700, 0, 2000)));
            Config.SubMenu("KS").AddItem(new MenuItem("maxRange", "Max R KS Range").SetValue(new Slider(2000, 0, 3500)));

            Config.SubMenu("Draw").AddItem(new MenuItem("drawReticle", "Draw Reticles").SetValue(new Circle(false, Color.FromKnownColor(KnownColor.Green))));
            Config.SubMenu("Draw").AddItem(new MenuItem("drawCatchRadius", "Draw Catch Radius").SetValue(new Circle(true, Color.FromKnownColor(KnownColor.Red))));
            Config.SubMenu("Draw").AddItem(new MenuItem("drawOrbwalkPosition", "Draw Orbwalk Position").SetValue(new Circle(false, Color.FromKnownColor(KnownColor.Blue))));
            Config.SubMenu("Draw").AddItem(new MenuItem("drawERange", "Draw E Range").SetValue(new Circle(true, Color.FromKnownColor(KnownColor.Green))));
            Config.SubMenu("Draw").AddItem(new MenuItem("drawRKSRange", "Draw R Max KS Range").SetValue(new Circle(true, Color.FromKnownColor(KnownColor.Aqua))));

            _E.SetSkillshot(250f, 130f, 1600f, false, SkillshotType.SkillshotLine);
            _R.SetSkillshot(500f, 160f, 2000f, false, SkillshotType.SkillshotLine);

            AntiGapcloser.OnEnemyGapcloser += OnEnemyGapCloser;
            Interrupter.OnPossibleToInterrupt += OnPossibleToInterrupt;
            Game.OnGameUpdate += OnGameUpdate;
            Drawing.OnDraw += OnDraw;
            GameObject.OnCreate += OnCreateObj;
            if (UsingLXOrbwalker)
            {
                LXOrbwalker.BeforeAttack += LxBeforeAttack;
            }
            else
            {
                Orbwalking.BeforeAttack += BeforeAttack;
            }
            
            Game.PrintChat("<font color=\"#00FF00\">yol0 Draven v" + Revision + " loaded!");
        }

        private static void OnGameUpdate(EventArgs args)
        {
            UpdateBuffs();
            KillSteal();
            if (UsingLXOrbwalker)
            {
                if (LXOrbwalker.CurrentMode == LXOrbwalker.Mode.Combo &&
                    Config.SubMenu("Combo").Item("UseQ").GetValue<bool>())
                    OrbwalkToReticle(false, false);
                if (LXOrbwalker.CurrentMode == LXOrbwalker.Mode.Harass &&
                    Config.SubMenu("Harass").Item("UseQ").GetValue<bool>())
                    OrbwalkToReticle(false, false);
                if (LXOrbwalker.CurrentMode == LXOrbwalker.Mode.Lasthit &&
                    Config.SubMenu("Farm").Item("UseQ").GetValue<bool>())
                    OrbwalkToReticle(false, false);
                if (LXOrbwalker.CurrentMode == LXOrbwalker.Mode.LaneClear &&
                    Config.SubMenu("LaneClear").Item("UseQ").GetValue<bool>())
                    OrbwalkToReticle(false, false);
                else
                    LXOrbwalker.CustomOrbwalkMode = false;

                if (LXOrbwalker.CurrentMode == LXOrbwalker.Mode.Combo)
                {
                    var target = LXOrbwalker.GetPossibleTarget();
                    if (!target.IsValidTarget())
                        return;

                    if (_W.IsReady() && (Config.SubMenu("Combo").Item("UseW").GetValue<bool>()) &&
                        target.IsValidTarget(_W.Range))
                    {
                        _W.Cast();
                    }

                    if (_E.IsReady() && Config.SubMenu("Combo").Item("UseE").GetValue<bool>() &&
                        Player.Distance(target) > Config.SubMenu("Misc").Item("MinERange").GetValue<Slider>().Value)
                    {
                        _E.Cast(LXOrbwalker.GetPossibleTarget());
                    }
                }
                else if (LXOrbwalker.CurrentMode == LXOrbwalker.Mode.Harass)
                {
                    var target = LXOrbwalker.GetPossibleTarget();
                    if (!target.IsValidTarget())
                        return;

                    if (_W.IsReady() &&
                        (Config.SubMenu("Harass").Item("UseW").GetValue<bool>() && _W.InRange(target.Position)))
                    {
                        _W.Cast();
                    }

                    if (_E.IsReady() && Config.SubMenu("Harass").Item("UseE").GetValue<bool>() &&
                        Player.Distance(target) > Config.SubMenu("Misc").Item("MinERange").GetValue<Slider>().Value)
                    {
                        _E.Cast(target);
                    }
                }
                else if (LXOrbwalker.CurrentMode == LXOrbwalker.Mode.Lasthit)
                {
                    var target = LXOrbwalker.GetPossibleTarget();
                    if (!target.IsValidTarget())
                        return;
                    
                    if (_W.IsReady() && _W.InRange(target.Position) && Config.SubMenu("Farm").Item("UseW").GetValue<bool>())
                    {
                        _W.Cast();
                    }
                }
                else if (LXOrbwalker.CurrentMode == LXOrbwalker.Mode.LaneClear)
                {
                    var target = LXOrbwalker.GetPossibleTarget();
                    if (!target.IsValidTarget())
                        return;

                    if (_W.IsReady() && _W.InRange(target.Position) && Config.SubMenu("LaneClear").Item("UseW").GetValue<bool>())
                    {
                        _W.Cast();
                    }
                }
            }
            else if (orbwalker != null)
            {
                if (orbwalker.ActiveMode.ToString() == "Combo" && Config.SubMenu("Combo").Item("UseQ").GetValue<bool>())
                    OrbwalkToReticle(false, false);
                else if (orbwalker.ActiveMode.ToString() == "Mixed" &&
                         Config.SubMenu("Harass").Item("UseQ").GetValue<bool>())
                    OrbwalkToReticle(false, false);
                else if (orbwalker.ActiveMode.ToString() == "LastHit" &&
                         Config.SubMenu("Farm").Item("UseQ").GetValue<bool>())
                    OrbwalkToReticle(false, false);
                else if (orbwalker.ActiveMode.ToString() == "LaneClear" &&
                         Config.SubMenu("LaneClear").Item("UseQ").GetValue<bool>())
                    OrbwalkToReticle(false, false);
                else
                    orbwalker.SetOrbwalkingPoint(new Vector3());

                if (orbwalker.ActiveMode.ToString() == "Combo")
                {
                    var target = orbwalker.GetTarget();
                    if (!target.IsValidTarget())
                        return;

                    if (_W.IsReady() && (Config.SubMenu("Combo").Item("UseW").GetValue<bool>()) &&
                        target.IsValidTarget(_W.Range))
                    {
                        _W.Cast();
                    }

                    if (_E.IsReady() && Config.SubMenu("Combo").Item("UseE").GetValue<bool>() &&
                        Player.Distance(target) > Config.SubMenu("Misc").Item("MinERange").GetValue<Slider>().Value)
                    {
                        _E.Cast(target);
                    }
                }
                else if (orbwalker.ActiveMode.ToString() == "Mixed")
                {
                    var target = orbwalker.GetTarget();
                    if (!target.IsValidTarget())
                        return;

                    if (_W.IsReady() &&
                        (Config.SubMenu("Harass").Item("UseW").GetValue<bool>() && target.IsValidTarget(_W.Range)))
                    {
                        _W.Cast();
                    }

                    if (_E.IsReady() && Config.SubMenu("Harass").Item("UseE").GetValue<bool>() &&
                        Player.Distance(target) > Config.SubMenu("Misc").Item("MinERange").GetValue<Slider>().Value)
                    {
                        _E.Cast(target);
                    }

                }
                else if (orbwalker.ActiveMode.ToString() == "LastHit")
                {
                    var target = orbwalker.GetTarget();
                    if (!target.IsValidTarget())
                        return;

                    if (_W.IsReady() && _W.InRange(target.Position) && Config.SubMenu("Farm").Item("UseW").GetValue<bool>())
                    {
                        _W.Cast();
                    }
                }
                else if (orbwalker.ActiveMode.ToString() == "LaneClear")
                {
                    var target = orbwalker.GetTarget();
                    if (!target.IsValidTarget())
                        return;

                    if (_W.IsReady() && _W.InRange(target.Position) && Config.SubMenu("LaneClear").Item("UseW").GetValue<bool>())
                    {
                        _W.Cast();
                    }
                }
            }
        }
    
        private static void BeforeAttack(Orbwalking.BeforeAttackEventArgs args)
        {
            if (_Q.IsReady() && NumAxes == 0)
            {
                if (orbwalker.ActiveMode.ToString() == "Combo" && Config.SubMenu("Combo").Item("UseQ").GetValue<bool>())
                    _Q.Cast();
                else if (orbwalker.ActiveMode.ToString() == "Mixed" && Config.SubMenu("Harass").Item("UseQ").GetValue<bool>())
                    _Q.Cast();
                else if (orbwalker.ActiveMode.ToString() == "LastHit" && Config.SubMenu("Farm").Item("UseQ").GetValue<bool>())
                    _Q.Cast();
                else if (orbwalker.ActiveMode.ToString() == "LaneClear" && Config.SubMenu("LaneClear").Item("UseQ").GetValue<bool>())
                    _Q.Cast();
            }
        }

        private static void LxBeforeAttack(LXOrbwalker.BeforeAttackEventArgs args)
        {
            if (_Q.IsReady() && NumAxes == 0)
            { 
                if (LXOrbwalker.CurrentMode == LXOrbwalker.Mode.Combo && Config.SubMenu("Combo").Item("UseQ").GetValue<bool>())
                    _Q.Cast();
                if (LXOrbwalker.CurrentMode == LXOrbwalker.Mode.Harass && Config.SubMenu("Harass").Item("UseQ").GetValue<bool>())
                    _Q.Cast();
                if (LXOrbwalker.CurrentMode == LXOrbwalker.Mode.Lasthit && Config.SubMenu("Farm").Item("UseQ").GetValue<bool>())
                    _Q.Cast();
                if (LXOrbwalker.CurrentMode == LXOrbwalker.Mode.LaneClear && Config.SubMenu("LaneClear").Item("UseQ").GetValue<bool>())
                    _Q.Cast(); 
            }
        }

        private static void OnDraw(EventArgs args)
        {
            if (Config.SubMenu("Draw").Item("drawReticle").GetValue<Circle>().Active)
            {
                foreach (var ret in Reticles)
                {
                    Utility.DrawCircle(ret.gameobj.Position, Config.SubMenu("Reticle").Item("PlayerRadius").GetValue<Slider>().Value, Config.SubMenu("Draw").Item("drawReticle").GetValue<Circle>().Color);
                }
            }

            if (Config.SubMenu("Draw").Item("drawCatchRadius").GetValue<Circle>().Active && Config.SubMenu("Reticle").Item("Mode").GetValue<StringList>().SelectedIndex == 0)
            {
                Utility.DrawCircle(Game.CursorPos, Config.SubMenu("Reticle").Item("MouseRadius").GetValue<Slider>().Value, Config.SubMenu("Draw").Item("drawCatchRadius").GetValue<Circle>().Color);
            }

            if (Config.SubMenu("Draw").Item("drawOrbwalkPosition").GetValue<Circle>().Active && _orbWalkPos.To2D().IsValid())
            {
                Utility.DrawCircle(_orbWalkPos, 200, Config.SubMenu("Draw").Item("drawOrbwalkPosition").GetValue<Circle>().Color);
            }

            if (Config.SubMenu("Draw").Item("drawERange").GetValue<Circle>().Active)
            {
                Utility.DrawCircle(Player.Position, _E.Range, Config.SubMenu("Draw").Item("drawERange").GetValue<Circle>().Color);
            }

            if (Config.SubMenu("Draw").Item("drawRKSRange").GetValue<Circle>().Active)
            {
                Utility.DrawCircle(Player.Position, Config.SubMenu("KS").Item("maxRange").GetValue<Slider>().Value, Config.SubMenu("Draw").Item("drawRKSRange").GetValue<Circle>().Color);
            }
        }

        private static void OnCreateObj(GameObject obj, EventArgs args)
        {
            if (obj.Name.Contains("Q_reticle_self"))
            {
                var ret = new Reticle(obj, DateTime.Now.Ticks, DateTime.Now.Ticks + 1300);
                Reticles.Add(ret);
            }
        }

        private static void OnEnemyGapCloser(ActiveGapcloser gapcloser)
        {
            if (Config.SubMenu("Misc").Item("Gapcloser").GetValue<bool>() && _E.IsReady() && gapcloser.Sender.IsValidTarget(_E.Range))
            {
                _E.Cast(gapcloser.Sender);
            }
        }

        private static void OnPossibleToInterrupt(Obj_AI_Base unit, InterruptableSpell spell)
        {
            if (Config.SubMenu("Misc").Item("Interrrupt").GetValue<bool>() && _E.IsReady() && unit.IsValidTarget(_E.Range))
            {
                _E.Cast(unit);
            }
        }

        private static void KillSteal()
        {
            foreach (var enemy in ObjectManager.Get<Obj_AI_Hero>().Where(hero => hero.Team != Player.Team))
            {
                if (enemy.IsValidTarget(Config.SubMenu("KS").Item("maxRange").GetValue<Slider>().Value) &&
                    enemy.IsVisible && Player.Distance(enemy) >= Config.SubMenu("KS").Item("minRange").GetValue<Slider>().Value && !RBuff && Damage.GetSpellDamage(Player, enemy, SpellSlot.R) > enemy.Health)
                {
                    _R.Cast(enemy);
                }
            }
        }

        private static void OrbwalkToReticle(bool force, bool w)
        {
            var closestToPlayer = new Reticle(null, -1, -1);
            var closestToMouse = new Reticle(null, -1, -1);

            foreach (var ret in Reticles)
            {
                if (!ret.gameobj.IsValid)
                    Reticles.Remove(ret);
            }

            if (Reticles.Any())
            {
                if (Config.SubMenu("Reticle").Item("Mode").GetValue<StringList>().SelectedIndex == 0) // Close to mouse
                {
                    var closestDistance = float.MaxValue;
                    var closestIndex = int.MaxValue;

                    for (var i = 0; i < Reticles.Count(); i++)
                    {
                        var ret = Reticles[i];
                        var distance = Vector3.Distance(ret.gameobj.Position, Game.CursorPos);
                        if (distance < closestDistance)
                        {
                            closestIndex = i;
                            closestDistance = distance;
                        }
                        closestToMouse = Reticles[closestIndex];
                    }


                }
                else if (Config.SubMenu("Reticle").Item("Mode").GetValue<StringList>().SelectedIndex == 1) // Close to player
                {
                    var closestDistance = float.MaxValue;
                    var closestIndex = int.MaxValue;

                    for (var i = 0; i < Reticles.Count(); i++)
                    {
                        var ret = Reticles[i];
                        var distance = Vector3.Distance(ret.gameobj.Position, Player.Position);
                        if (distance < closestDistance)
                        {
                            closestIndex = i;
                            closestDistance = distance;
                        }
                        closestToPlayer = Reticles[closestIndex];
                    }
                }
            }
            else
            {
                _orbWalkPos = new Vector3();
                if (UsingLXOrbwalker)
                {
                    LXOrbwalker.CustomOrbwalkMode = false;
                }
                else if (orbwalker != null)
                {
                    orbwalker.SetOrbwalkingPoint(new Vector3());
                }
            }

            if (Config.SubMenu("Reticle").Item("Mode").GetValue<StringList>().SelectedIndex == 0)
            {
                if (closestToMouse.CreateTime != -1)
                {
                    var qMouseDistance = Vector3.Distance(closestToMouse.gameobj.Position, Game.CursorPos);
                    var qHeroDistance = Vector3.Distance(closestToMouse.gameobj.Position, Player.Position) - Player.BoundingRadius;
                    var canReach = DateTime.Now.Ticks + qHeroDistance/Player.MoveSpeed < closestToMouse.ExpireTime;
                    var qMouseRadius = Config.SubMenu("Reticle").Item("MouseRadius").GetValue<Slider>().Value;
                    var qPlayerRadius = Config.SubMenu("Reticle").Item("PlayerRadius").GetValue<Slider>().Value;

                    if ((force || canReach) && qMouseDistance < qMouseRadius && qHeroDistance > qPlayerRadius)
                    {
                        if (UsingLXOrbwalker)
                        {
                            LXOrbwalker.CustomOrbwalkMode = true;
                            LXOrbwalker.Orbwalk(closestToMouse.gameobj.Position, LXOrbwalker.GetPossibleTarget());
                        }
                        else if (orbwalker != null)
                        {
                            orbwalker.SetOrbwalkingPoint(closestToMouse.gameobj.Position);
                        }
                    }
                    /*else
                    {
                        _orbWalkPos = new Vector3();
                        if (UsingLXOrbwalker)
                        {
                            LXOrbwalker.CustomOrbwalkMode = false;
                        }
                        else if (orbwalker != null)
                        {
                            orbwalker.SetOrbwalkingPoint(new Vector3());
                        }

                    }*/
                }


            }
            else if (Config.SubMenu("Reticle").Item("Mode").GetValue<StringList>().SelectedIndex == 1)
            {
                if (closestToMouse.CreateTime != -1)
                {
                    var qHeroDistance = Vector3.Distance(closestToPlayer.gameobj.Position, Player.Position);
                    var canReach = DateTime.Now.Ticks + qHeroDistance/Player.MoveSpeed < closestToMouse.ExpireTime;
                    if (!canReach && w && _W.IsReady())
                    {
                        _W.Cast();
                        canReach = true;
                    }

                    var qPlayerRadius = Config.SubMenu("Reticle").Item("PlayerRadius").GetValue<Slider>().Value;
                    if ((force || canReach) && qHeroDistance > qPlayerRadius)
                    {
                        _orbWalkPos = closestToPlayer.gameobj.Position;
                        if (UsingLXOrbwalker)
                        {
                            LXOrbwalker.CustomOrbwalkMode = true;
                            LXOrbwalker.Orbwalk(closestToPlayer.gameobj.Position, LXOrbwalker.GetPossibleTarget());
                        }
                        else if (orbwalker != null)
                        {
                            orbwalker.SetOrbwalkingPoint(closestToPlayer.gameobj.Position);
                        }
                    }
                    /*else
                    {
                        _orbWalkPos = new Vector3();
                        if (UsingLXOrbwalker)
                        {
                            LXOrbwalker.CustomOrbwalkMode = false;
                        }
                        else if (orbwalker != null)
                        {
                            orbwalker.SetOrbwalkingPoint(new Vector3());
                        }
                    }*/
                }
            }
            else
            {
                _orbWalkPos = new Vector3();
                if (UsingLXOrbwalker)
                {
                    LXOrbwalker.CustomOrbwalkMode = false;
                }
                else if (orbwalker != null)
                {
                    orbwalker.SetOrbwalkingPoint(new Vector3());
                }
            }
        }
        
        private static void UpdateBuffs()
        {
            var AX = false;
            var R = false;

            var buffList = Player.Buffs;
            foreach (var buff in buffList)
            {
                if (buff.Name == "dravenspinningattack")
                {
                    AX = true;
                    NumAxes = buff.Count;
                }

                if (buff.Name == "dravenrdoublecast")
                {
                    R = true;
                }
            }

            AxesActive = AX;
            if (!AxesActive)
                NumAxes = 0;

            RBuff = R;
        }
    }
}
