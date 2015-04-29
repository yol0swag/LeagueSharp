using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;
using Color = System.Drawing.Color;

namespace yol0LeeSin
{
    class Program
    {
        private static Obj_AI_Hero Player { get { return ObjectManager.Player; } }
        private static Spell _Q = new Spell(SpellSlot.Q, 1100);
        private static Spell _Q2 = new Spell(SpellSlot.Q, 1300);
        private static Spell _W = new Spell(SpellSlot.W, 700);
        private static Spell _E = new Spell(SpellSlot.E, 350);
        private static Spell _E2 = new Spell(SpellSlot.E, 500);
        private static Spell _R = new Spell(SpellSlot.R, 375);
        private static Spell _F = new Spell(SpellSlot.Unknown, 425);

        private static SpellSlot _I = SpellSlot.Unknown;

        private static Menu _menu;
        private static Orbwalking.Orbwalker _orbwalker;

        private static GameObject _ward;
        //private static Vector2 wardPosition;

        private static int pCount;
        private static int pTimer;

        private static int qTimer;
        private static int wTimer;
        private static int eTimer;

        private static int lastWardCast;

        private static Obj_AI_Hero _target;



        static void Main(string[] args)
        {
            CustomEvents.Game.OnGameLoad += OnGameLoad;
        }

        private static void OnGameLoad(EventArgs args)
        {
            _menu = new Menu("yol0 LeeSin", "yol0LeeSin", true);
            _menu.AddSubMenu(new Menu("Target Selector", "Target Selector"));
            _menu.AddSubMenu(new Menu("Orbwalker", "Orbwalker"));
            _menu.AddSubMenu(new Menu("Keys", "Keys"));
            //_menu.AddSubMenu(new Menu("Combo", "Combo"));
            _menu.AddSubMenu(new Menu("Insec", "Insec"));
            //_menu.AddSubMenu(new Menu("Dodge", "Dodge"));
            _menu.AddSubMenu(new Menu("Drawing", "Draw"));

            TargetSelector.AddToMenu(_menu.SubMenu("Target Selector"));
            _orbwalker = new Orbwalking.Orbwalker(_menu.SubMenu("Orbwalker"));

            _menu.SubMenu("Keys").AddItem(new MenuItem("Insec", "Insec").SetValue(new KeyBind("X".ToArray()[0], KeyBindType.Press)));
            _menu.SubMenu("Keys").AddItem(new MenuItem("Escape", "Escape").SetValue(new KeyBind("A".ToArray()[0], KeyBindType.Press)));
            _menu.SubMenu("Keys").AddItem(new MenuItem("Wardjump", "Ward Jump").SetValue(new KeyBind("Z".ToArray()[0], KeyBindType.Press)));

            //_menu.SubMenu("Combo").AddItem(new MenuItem("useQ", "Use Q").SetValue(true));
            //_menu.SubMenu("Combo").AddItem(new MenuItem("useQ2", "Use Q2").SetValue(true));
            //_menu.SubMenu("Combo").AddItem(new MenuItem("useW", "Use W").SetValue(false));
            //_menu.SubMenu("Combo").AddItem(new MenuItem("useW2", "Use W2").SetValue(true));
            //_menu.SubMenu("Combo").AddItem(new MenuItem("useE", "Use E").SetValue(true));
            //_menu.SubMenu("Combo").AddItem(new MenuItem("useE2", "Use E2").SetValue(true));
            //_menu.SubMenu("Combo").AddItem(new MenuItem("useR", "Use R").SetValue(true));
            //_menu.SubMenu("Combo").AddItem(new MenuItem("useI", "Use Ignite").SetValue(true));
            //_menu.SubMenu("Combo").AddItem(new MenuItem("useItems", "Use Items").SetValue(true));

            _menu.SubMenu("Insec").AddItem(new MenuItem("method", "Insec Method").SetValue(new StringList(new[] { "Wardjump only", "Flash Only", "Wardjump + Flash" }, 2)));
            _menu.SubMenu("Insec").AddItem(new MenuItem("mode", "Insec Mode").SetValue(new StringList(new[] { "To Ally", "To Mouse", "To Turret" }, 0)));

            _menu.SubMenu("Draw").AddItem(new MenuItem("drawQ", "Draw Q Range").SetValue(new Circle(true, Color.Green)));
            _menu.SubMenu("Draw").AddItem(new MenuItem("drawW", "Draw W Range").SetValue(new Circle(true, Color.Green)));
            _menu.SubMenu("Draw").AddItem(new MenuItem("drawE", "Draw E Range").SetValue(new Circle(true, Color.Green)));
            _menu.SubMenu("Draw").AddItem(new MenuItem("drawR", "Draw R Range").SetValue(new Circle(true, Color.Green)));

            _menu.SubMenu("Draw").AddItem(new MenuItem("drawInsec", "Draw Insec").SetValue(true));
            _menu.SubMenu("Draw").AddItem(new MenuItem("drawDamage", "Draw Damage on Healthbar").SetValue(true));
            _menu.SubMenu("Draw").AddItem(new MenuItem("drawTarget", "Draw Target").SetValue(true));

            _menu.AddToMainMenu();

            if (Player.Spellbook.GetSpell(SpellSlot.Summoner1).Name == "summonerdot")
                _I = SpellSlot.Summoner1;
            else if (Player.Spellbook.GetSpell(SpellSlot.Summoner2).Name == "summonerdot")
                _I = SpellSlot.Summoner2;

            if (Player.Spellbook.GetSpell(SpellSlot.Summoner1).Name == "summonerflash")
                _F.Slot = SpellSlot.Summoner1;
            else if (Player.Spellbook.GetSpell(SpellSlot.Summoner2).Name == "summonerflash")
                _F.Slot = SpellSlot.Summoner2;

            _Q.SetSkillshot(0.25f, 60f, 1750f, true, SkillshotType.SkillshotLine);

            Utility.HpBarDamageIndicator.DamageToUnit = delegate(Obj_AI_Hero enemy)
            {
                return (float)GetDamage(enemy);
            };
			GameObject.OnCreate += OnCreateObject;
            Obj_AI_Base.OnProcessSpellCast += OnProcessSpellCast;
            Game.OnUpdate += OnUpdate;
            Drawing.OnDraw += OnDraw;
        }

        #region Events
        private static void OnDraw(EventArgs args)
        {
            if (_menu.SubMenu("Draw").Item("drawQ").GetValue<Circle>().Active)
            {
                Render.Circle.DrawCircle(Player.Position, _Q.Range, _menu.SubMenu("Draw").Item("drawQ").GetValue<Circle>().Color);
            }

            if (_menu.SubMenu("Draw").Item("drawW").GetValue<Circle>().Active)
            {
                Render.Circle.DrawCircle(Player.Position, _W.Range, _menu.SubMenu("Draw").Item("drawW").GetValue<Circle>().Color);
            }

            if (_menu.SubMenu("Draw").Item("drawE").GetValue<Circle>().Active)
            {
                Render.Circle.DrawCircle(Player.Position, _E.Range, _menu.SubMenu("Draw").Item("drawE").GetValue<Circle>().Color);
            }

            if (_menu.SubMenu("Draw").Item("drawR").GetValue<Circle>().Active)
            {
                Render.Circle.DrawCircle(Player.Position, _R.Range, _menu.SubMenu("Draw").Item("drawR").GetValue<Circle>().Color);
            }

            if (_menu.SubMenu("Draw").Item("drawTarget").GetValue<bool>() && _target != null && !_target.IsDead)
            {
                Render.Circle.DrawCircle(_target.Position, _target.BoundingRadius, Color.Red);
                Render.Circle.DrawCircle(_target.Position, _target.BoundingRadius + 10, Color.Red);
                Render.Circle.DrawCircle(_target.Position, _target.BoundingRadius + 25, Color.Red);
            }

            if (_menu.SubMenu("Draw").Item("drawInsec").GetValue<bool>() && _target != null && !_target.IsDead && Hud.SelectedUnit != null && _target.NetworkId == Hud.SelectedUnit.NetworkId && _R.IsReady())
            {
                var insecPos = GetInsecPosition(_target);
                Render.Circle.DrawCircle(insecPos.To3D(), 40f, Color.Green);
                var dirPos = (_target.Position.To2D() - insecPos).Normalized();
                var endPos = _target.Position.To2D() + (dirPos * 1200);

                var wts1 = Drawing.WorldToScreen(insecPos.To3D());
                var wts2 = Drawing.WorldToScreen(endPos.To3D());

                Drawing.DrawLine(wts1, wts2, 2, Color.Green);

            }

        }

		private static void OnCreateObject(GameObject sender, EventArgs args)
        {
            if (Player.Distance(sender.Position) <= 700 && sender.IsAlly && (sender.Name == "VisionWard" || sender.Name == "SightWard"))
            {
                _ward = sender;
            }
        }
		
        private static void OnUpdate(EventArgs args)
        {
            Utility.HpBarDamageIndicator.Enabled = _menu.SubMenu("Draw").Item("drawDamage").GetValue<bool>();
            _target = TargetSelector.GetTarget(1100, TargetSelector.DamageType.Physical);
            if (_menu.SubMenu("Keys").Item("Escape").GetValue<KeyBind>().Active)
            {
                Escape();
            }

            if (_menu.SubMenu("Keys").Item("Wardjump").GetValue<KeyBind>().Active)
            {
                Wardjump();
            }

            if (_menu.SubMenu("Keys").Item("Insec").GetValue<KeyBind>().Active)
            {
                if (Hud.SelectedUnit != null && _target != null && _target.NetworkId == Hud.SelectedUnit.NetworkId)
                    Insec(_target);
            }

            if (_orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo && _target != null)
            {
                Combo(_target);
            }
        }

        private static void OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (sender.IsMe)
            {
                if (args.SData.Name == "BlindMonkQOne")
                    qTimer = Environment.TickCount + 3000;
                else if (args.SData.Name == "BlindMonkWOne")
                    wTimer = Environment.TickCount + 3000;
                else if (args.SData.Name == "BlindMonkEOne")
                    eTimer = Environment.TickCount + 3000;
                else if (args.SData.Name == "BlindMonkRKick")
                    Orbwalking.ResetAutoAttackTimer();

                else if (args.SData.Name.Contains("ward") || args.SData.Name.Contains("Trinket"))
                    lastWardCast = Environment.TickCount;

            }
        }
        #endregion
        #region Damage Calculations
        private static double GetDamage(Obj_AI_Hero target)
        {
            var qDmg = _Q.IsReady() ? Player.GetSpellDamage(target, SpellSlot.Q) : 0.0;
            if (_Q.IsReady() && _Q.Instance.Name == "BlindMonkQOne")
            {
                qDmg += Player.GetSpellDamage(target, SpellSlot.Q, 1);
            }
            var eDmg = _E.IsReady() ? Player.GetSpellDamage(target, SpellSlot.E) : 0.0;
            var rDmg = _R.IsReady() ? Player.GetSpellDamage(target, SpellSlot.R) : 0.0;
            var aDmg = Player.GetAutoAttackDamage(target);
            var iDmg = 0.0;

            if (_I != SpellSlot.Unknown && Player.Spellbook.CanUseSpell(_I) == SpellState.Ready)
                iDmg = Player.GetSummonerSpellDamage(target, Damage.SummonerSpell.Ignite);

            return qDmg + eDmg + rDmg + aDmg * 2 + iDmg;
        }

        private static double GetQ2Damage(Obj_AI_Hero target, double dmg)
        {
            var hpafter = target.Health - dmg;
            var qDmg = ((_Q.Level * 30) + 20) + (Player.BaseAttackDamage * 0.9) + (0.08 * (target.MaxHealth - hpafter));
            return Damage.CalcDamage(Player, target, Damage.DamageType.Physical, qDmg);
        }
        #endregion
        #region Combo
        private static void Combo(Obj_AI_Hero target)
        {
            //_orbwalker.SetMovement(false);
            if (target.HasBuff("BlindMonkQOne") && _Q.IsReady() && Player.Mana >= 30)
            {
                
                var eDmg = Player.GetSpellDamage(target, SpellSlot.E);
                var rDmg = Player.GetSpellDamage(target, SpellSlot.R);
                var iDmg = _I != SpellSlot.Unknown ? Player.GetSummonerSpellDamage(target, Damage.SummonerSpell.Ignite) : 0.0;
                var aDmg = Player.GetAutoAttackDamage(target);

                if (target.IsValidTarget(_Q2.Range))
                {
                    if (GetQ2Damage(target, 0) > target.Health)
                    {
                        _Q2.Cast();
                        return;
                    }
                }

                if (_E.IsReady() && Player.Mana >= 80)
                {
                    if (target.Health <= eDmg + GetQ2Damage(target, eDmg) && target.IsValidTarget(_E.Range))
                    {
                        _E.Cast();
                        _Q2.Cast();
                        return;
                    }
                    else if (target.Health <= eDmg + GetQ2Damage(target, eDmg) + iDmg && target.IsValidTarget(_E.Range))
                    {
                        Player.Spellbook.CastSpell(_I, target);
                        _E.Cast();
                        _Q2.Cast();
                        return;
                    }
                }

                if (_R.IsReady() && _E.IsReady() && Player.Mana >= 80)
                {
                    if (target.Health <= eDmg + rDmg + GetQ2Damage(target, eDmg + rDmg) && target.IsValidTarget(_E.Range))
                    {
                        _E.Cast();
                        _R.CastOnUnit(target);
                        _Q2.Cast();
                        return;
                    }
                    else if (target.Health <= GetQ2Damage(target, rDmg) + rDmg && target.IsValidTarget(_Q2.Range))
                    {
                        _R.CastOnUnit(target);
                        _Q2.Cast();
                        return;
                    }
                    else if (target.Health <= GetQ2Damage(target, rDmg + eDmg) + rDmg + eDmg + iDmg && target.IsValidTarget(_E.Range))
                    {
                        Player.Spellbook.CastSpell(_I, target);
                        _E.Cast();
                        _R.CastOnUnit(target);
                        _Q2.Cast();
                        return;
                    }
                }

                if (_R.IsReady())
                {
                    if (target.Health <= rDmg && target.IsValidTarget(_R.Range))
                    {
                        _R.CastOnUnit(target);
                        return;
                    }
                    else if (target.Health <= rDmg + GetQ2Damage(target, rDmg) && target.IsValidTarget(_R.Range))
                    {
                        _R.CastOnUnit(target);
                        _Q2.Cast();
                        return;
                    }
                    else if (target.Health <= rDmg + GetQ2Damage(target, rDmg) + iDmg && target.IsValidTarget(_R.Range))
                    {
                        Player.Spellbook.CastSpell(_I, target);
                        _R.CastOnUnit(target);
                        _Q2.Cast();
                        return;
                    }
                }
            }
            else
            {
                var qDmg = Player.GetSpellDamage(target, SpellSlot.Q);
                var eDmg = Player.GetSpellDamage(target, SpellSlot.E);
                var rDmg = Player.GetSpellDamage(target, SpellSlot.R);
                var iDmg = Player.Spellbook.CanUseSpell(_I) == SpellState.Ready ? Player.GetSummonerSpellDamage(target, Damage.SummonerSpell.Ignite) : 0.0;
                var aDmg = Player.GetAutoAttackDamage(target);

                if (_E.IsReady())
                {
                    if (target.IsValidTarget(_E.Range))
                    {
                        if (target.Health <= eDmg)
                        {
                            _E.Cast();
                            return;
                        }
                        else if (target.Health <= eDmg + iDmg)
                        {
                            Player.Spellbook.CastSpell(_I, target);
                            _E.Cast();
                            return;
                        }
                    }
                }

                if (_R.IsReady())
                {
                    if (target.IsValidTarget(_R.Range))
                    {
                        if (target.Health <= rDmg)
                        {
                            _R.CastOnUnit(target);
                            return;
                        }
                        else if (target.Health <= rDmg + iDmg)
                        {
                            Player.Spellbook.CastSpell(_I, target);
                            _R.CastOnUnit(target);
                            return;
                        }
                    }
                }

                if (_E.IsReady() && _R.IsReady())
                {
                    if (target.IsValidTarget(_E.Range))
                    {
                        if (target.Health <= eDmg + rDmg)
                        {
                            _E.Cast();
                            _R.CastOnUnit(target);
                            return;
                        }
                        else if (target.Health <= eDmg + rDmg + iDmg)
                        {
                            _E.Cast();
                            Player.Spellbook.CastSpell(_I, target);
                            _R.CastOnUnit(target);
                            return;
                        }
                    }

                }

                if (_Q.IsReady())
                {
                    if (target.IsValidTarget(_Q.Range))
                    {
                        _Q.Cast(target);
                        return;
                    }
                }
            }

            if (_E.IsReady())
            {
                if (target.IsValidTarget(_E.Range))
                {
                    _E.Cast();
                }
            }

            if (_Q2.IsReady())
            {
                if (target.IsValidTarget(_Q2.Range))
                {
                    _Q2.Cast();
                }
            }
            //_orbwalker.SetMovement(true);
        }
        #endregion
        #region Escape
		
		/*private static InventorySlot GetWardSlot()
        {
            var wardNames = new[] { "Warding Totem (Trinket)", "Greater Totem (Trinket)", "Greater Stealth Totem (Trinket)", "Ruby Sightstone", "Sightstone", "Stealth Ward" };
            foreach (var name in wardNames)
            {
                var id = Player.InventoryItems.FirstOrDefault(slot => slot.DisplayName == name);
                if (id.IsValidSlot() && Player.Spellbook.CanUseSpell(id.SpellSlot) == SpellState.Ready)
                {
                    return id;
                }
            }
            return null;
        }*/
		
        private static bool CanCastWard()
        {
            return _W.Instance.Name == "BlindMonkWOne" && Environment.TickCount - 2000 > lastWardCast && !Player.HasBuff("BlindMonkWOne");
        }
        private static Obj_AI_Base GetEscapeObject(Vector3 pos, int range = 700)
        {
            var allies = HeroManager.Allies.Where(hero => hero.Distance(pos) <= range).OrderBy(hero => hero.Distance(pos)).ToList();
            var minions = MinionManager.GetMinions(pos, range, MinionTypes.All, MinionTeam.Ally).OrderBy(minion => minion.Distance(pos)).ToList();
            var wards = ObjectManager.Get<Obj_AI_Minion>().Where(obj => (obj.Name.Contains("Ward") || obj.Name.Contains("ward") || obj.Name.Contains("Trinket")) && obj.IsAlly && pos.Distance(obj.Position) <= range).OrderBy(obj => obj.Distance(pos)).ToList();
            foreach (var ally in allies)
            {
                if (!ally.IsMe)
                    return ally;
            }

			if (_ward != null && _ward.IsValid && !_ward.IsDead && Player.Distance(_ward.Position) <= range)
            {
                return _ward as Obj_AI_Base;
            }
			
            foreach (var ward in wards)
            {
                return ward;
            }

            foreach (var minion in minions)
            {
                return minion;
            }


            return null;
        }

        private static void Wardjump()
        {
            var escapeObject = GetEscapeObject(Game.CursorPos);
            if (escapeObject != null)
            {
                if (CanCastW())
                {
                    wTimer = Environment.TickCount + 3000;
                    _W.CastOnUnit(escapeObject);
                }
            }
            else if (_W.IsReady() && !Player.HasBuff("BlindMonkWOne"))
            {
                var wardSlot = Items.GetWardSlot();
                if (wardSlot.IsValidSlot() && (Player.Spellbook.CanUseSpell(wardSlot.SpellSlot) == SpellState.Ready || wardSlot.Stacks != 0) && CanCastWard())
                {
                    lastWardCast = Environment.TickCount;
                    Player.Spellbook.CastSpell(wardSlot.SpellSlot, GetCorrectedMousePosition());
                }
            }
        }
        private static void Escape()
        {
            var escapeObject = GetEscapeObject(Game.CursorPos);
            if (escapeObject != null)
            {
                if (CanCastW())
                {
                    wTimer = Environment.TickCount + 3000;
                    _W.CastOnUnit(escapeObject);
                }
            }
        }

        private static Vector3 GetCorrectedMousePosition()
        {
            return Player.Position - (Player.Position - Game.CursorPos).Normalized() * 600;
        }
        #endregion
        #region Insec
        private static Vector2 GetInsecPosition(Obj_AI_Hero target)
        {
            if (_menu.SubMenu("Insec").Item("mode").GetValue<StringList>().SelectedValue == "To Ally")
            {
                var nearestTurret = ObjectManager.Get<Obj_AI_Turret>().Where(obj => obj.IsAlly).OrderBy(obj => Player.Distance(obj.Position)).ToList()[0];
                var allies = HeroManager.Allies.Where(hero => hero.Distance(target.Position) <= 1500).OrderByDescending(hero => hero.Distance(target.Position)).ToList();
                if (allies.Count() > 0 && !allies[0].IsMe)
                {
                    var directionVector = (target.Position - allies[0].Position).Normalized().To2D();
                    return target.Position.To2D() + (directionVector * 250);
                }
                var dirVector = (target.Position - nearestTurret.Position).Normalized().To2D();
                return target.Position.To2D() + (dirVector * 250);
            }
            else if (_menu.SubMenu("Insec").Item("mode").GetValue<StringList>().SelectedValue == "To Mouse")
            {
                var directionVector = (target.Position - Game.CursorPos).Normalized().To2D();
                return target.Position.To2D() + (directionVector * 250);
            }
            else
            {
                var nearestTurret = ObjectManager.Get<Obj_AI_Turret>().Where(obj => obj.IsAlly).OrderBy(obj => Player.Distance(obj.Position)).ToList()[0];
                var dirVector = (target.Position - nearestTurret.Position).Normalized().To2D();
                return target.Position.To2D() + (dirVector * 250);
            }
        }

        private static Obj_AI_Base GetInsecObject(Vector3 pos, int range = 700)
        {
            var allies = HeroManager.Allies.Where(hero => hero.Distance(pos) <= range).OrderBy(hero => hero.Distance(pos)).ToList();
            var minions = MinionManager.GetMinions(pos, range, MinionTypes.All, MinionTeam.Ally).OrderBy(minion => minion.Distance(pos)).ToList();
            var wards = ObjectManager.Get<Obj_AI_Minion>().Where(obj => (obj.Name.Contains("ward") || obj.Name.Contains("Ward") || obj.Name.Contains("Trinket")) && obj.IsAlly && pos.Distance(obj.Position) <= range).OrderByDescending(obj => obj.Distance(pos)).ToList();
            foreach (var ally in allies)
            {
                if (!ally.IsMe)
                    return ally;
            }

			if (_ward != null && _ward.IsValid && !_ward.IsDead && Player.Distance(_ward.Position) <= range)
            {
                return _ward as Obj_AI_Base;
            }
			
            foreach (var minion in minions)
            {
                return minion;
            }

            foreach (var ward in wards)
            {
                return ward;
            }
            return null;
        }

        private static void Insec(Obj_AI_Hero target)
        {
            if (!_R.IsReady())
                return;

            if (!target.IsValidTarget() || target.IsDead)
                return;

            var insecPos = GetInsecPosition(target);
            if (Player.Distance(insecPos) < 150)
            {
                _R.CastOnUnit(target);
                return;
            }

            if (_menu.SubMenu("Insec").Item("method").GetValue<StringList>().SelectedValue == "Wardjump only")
            {
                if (_W.IsReady() && Player.Mana >= 50)
                {
                    if (Player.Distance(insecPos) <= 600)
                    {
                        var insecObj = GetInsecObject(insecPos.To3D(), 200);
                        if (insecObj != null)
                        {
                            if (Player.Distance(insecObj.Position) <= 600)
                            {
                                _W.CastOnUnit(insecObj);
                            }
                        }
                        else
                        {
                            var slot = Items.GetWardSlot();
                            if (slot.IsValidSlot() && Player.Spellbook.CanUseSpell(slot.SpellSlot) == SpellState.Ready)
                            {
                                Player.Spellbook.CastSpell(slot.SpellSlot, insecPos.To3D());
                            }
                            return;
                        }
                    }
                    if (!Items.GetWardSlot().IsValidSlot())
                        return;

                    if (!_Q.IsReady())
                        return;

                    if (Player.Mana >= 130 && Player.Distance(insecPos) > 600)
                    {
                        _Q.Cast(target);
                    }

                    if (_target.HasBuff("BlindMonkQOne") && Player.Mana >= 80 && target.Distance(insecPos) <= 600)
                    {
                        _Q2.Cast();
                    }
                }
            }
            else if (_menu.SubMenu("Insec").Item("method").GetValue<StringList>().SelectedValue == "Flash Only")
            {
                if (_F.Slot == SpellSlot.Unknown || !_F.IsReady())
                    return;

                if (Player.Distance(insecPos) < 425)
                {
                    _F.Cast(insecPos);
                    return;
                }

                if (!_Q.IsReady())
                    return;
                if (Player.Mana >= 80 && Player.Distance(insecPos) > 600)
                {
                    _Q.Cast(target);
                }

                if (_target.HasBuff("BlindMonkQOne") && Player.Mana >= 80 && target.Distance(insecPos) <= 325)
                {
                    _Q2.Cast();
                }
            }
            else
            {
                if (_W.IsReady() && Player.Mana >= 50)
                {
                    if (Player.Distance(insecPos) <= 600)
                    {
                        var insecObj = GetInsecObject(insecPos.To3D(), 200);
                        if (insecObj != null)
                        {
                            if (Player.Distance(insecObj.Position) <= 600)
                            {
                                _W.CastOnUnit(insecObj);
                            }
                        }
                        else
                        {
                            var slot = Items.GetWardSlot();
                            if (slot.IsValidSlot() && Player.Spellbook.CanUseSpell(slot.SpellSlot) == SpellState.Ready)
                            {
                                Player.Spellbook.CastSpell(slot.SpellSlot, insecPos.To3D());
                            }
                            return;
                        }
                    }
                    if (!Items.GetWardSlot().IsValidSlot())
                        return;

                    if (!_Q.IsReady())
                        return;

                    if (Player.Mana >= 130 && Player.Distance(insecPos) > 600)
                    {
                        _Q.Cast(target);
                    }

                    if (_target.HasBuff("BlindMonkQOne") && Player.Mana >= 80 && target.Distance(insecPos) <= 600)
                    {
                        _Q2.Cast();
                    }
                }
                else if (_F.Slot != SpellSlot.Unknown && _F.IsReady() && _W.IsReady())
                {
                    if (Player.Distance(insecPos) < 425 && _F.Slot != SpellSlot.Unknown && _F.IsReady())
                    {
                        _F.Cast(insecPos);
                        return;
                    }

                    if (!_Q.IsReady())
                        return;

                    if (Player.Mana >= 80 && Player.Distance(insecPos) > 600)
                    {
                        _Q.Cast(target);
                    }

                    if (_target.HasBuff("BlindMonkQOne") && Player.Mana >= 80 && target.Distance(insecPos) <= 325)
                    {
                        _Q2.Cast();
                    }
                }
            }
        }
        #endregion
        #region Cast Checks
        private static bool CanCastQ()
        {
            return _Q.IsReady();
        }

        private static bool CanCastW()
        {
            return _W.IsReady() && _W.Instance.Name == "BlindMonkWOne";
        }

        private static bool CanCastE()
        {
            return _E.IsReady() && _E.Instance.Name == "BlindMonkEOne";
        }

        #endregion
    }
}
