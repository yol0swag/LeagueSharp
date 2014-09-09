using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;

using LeagueSharp;
using LeagueSharp.Common;
using System.Drawing;
namespace yol0Riven
{
    internal class Program
    {
        public static Obj_AI_Hero Player = ObjectManager.Player;
        public static Spellbook sBook = Player.Spellbook;
        public static Orbwalking.Orbwalker orbwalker;

        public static Spell _q = new Spell(SpellSlot.Q, 260);
        public static Spell _w = new Spell(SpellSlot.W, 260);
        public static Spell _e = new Spell(SpellSlot.E, 325);
        public static Spell _r = new Spell(SpellSlot.R, 900);
        public static Items.Item _tiamat = new Items.Item(3077, 400);
        public static Items.Item _tiamat2 = new Items.Item(3074, 400);
        public static Menu Config;

        private static int qCount = 0; // 
        private static int pCount = 0; // passive stacks

        private static bool ultiOn = false;
        private static bool ultiReady = false;

        private static Spell nextSpell = null;
        private static bool UseAttack = false;
        private static bool useTiamat = false;

        private static Obj_AI_Hero currentTarget = null;
        public static void Main(string[] args)
        {
            CustomEvents.Game.OnGameLoad += OnLoad;
        }

        private static void OnLoad(EventArgs args)
        {
            Config = new Menu("yol0 Riven", "Riven", true);
            Config.AddSubMenu(new Menu("Orbwalker", "Orbwalker"));
            orbwalker = new Orbwalking.Orbwalker(Config.SubMenu("Orbwalker"));

            var tsMenu = new Menu("Target Selector", "Target Selector");
            SimpleTs.AddToMenu(tsMenu);
            Config.AddSubMenu(tsMenu);
            Config.AddToMainMenu();

            Config.AddItem(new MenuItem("KillSteal", "Killsteal").SetValue(true));
            Config.AddItem(new MenuItem("DrawRanges", "Draw engage range").SetValue(true));
           
            Orbwalking.BeforeAttack += BeforeAttack;
            Orbwalking.AfterAttack += AfterAttack;
            Obj_AI_Base.OnProcessSpellCast += OnProcessSpell;
            Obj_AI_Base.OnPlayAnimation += OnAnimation;
            Game.OnGameUpdate += OnGameUpdate;
            Game.OnGameUpdate += Buffs_GameUpdate;
            Game.OnGameProcessPacket += OnGameProcessPacket;
            Drawing.OnDraw += OnDraw;
        }


        private static void Buffs_GameUpdate(EventArgs args)
        {
            var ulti = false;
            var ulti2 = false;
            var q = false;

            BuffInstance[] buffList = Player.Buffs;
            foreach (var buff in buffList)
            {
                if (buff.Name == "rivenpassiveaaboost")
                {
                    pCount = buff.Count;
                }

                if (buff.Name == "rivenwindslashready")
                {
                    ulti = true;
                    ultiReady = true;
                }

                if (buff.Name == "RivenTriCleave")
                {
                    q = true;
                    qCount = buff.Count;
                }

                if (buff.Name == "RivenFengShuiEngine")
                {
                    ulti2 = true;
                    ultiOn = true;
                }
            }

            if (q == false)
                qCount = 0;

            if (ulti == false)
                ultiReady = false;

            if (ulti2 == false)
                ultiOn = false;
            
        }

        private static void OnDraw(EventArgs args)
        {
            if (Config.Item("DrawRanges").GetValue<bool>())
            {
                Utility.DrawCircle(Player.Position, _e.Range + _q.Range, System.Drawing.Color.Blue);
            }
        }

        private static void OnGameUpdate(EventArgs args)
        {
            KillSecure();
            if (orbwalker.ActiveMode.ToString() == "Combo")
            {
                if (currentTarget != null && currentTarget.IsDead)
                    orbwalker.SetMovement(true);

                currentTarget = SimpleTs.GetTarget(_e.Range + _q.Range + Player.AttackRange, SimpleTs.DamageType.Physical);
                if (!currentTarget.IsDead && currentTarget.IsVisible)
                {
                    GapClose(currentTarget);
                    Combo(currentTarget);
                }
                
            }
        }

        public static void AfterAttack(Obj_AI_Base hero, Obj_AI_Base target)
        {
            orbwalker.SetMovement(true);
        }

        public static void BeforeAttack(Orbwalking.BeforeAttackEventArgs args)
        {
            if (!args.Target.IsMinion)
                orbwalker.SetMovement(false);

        }

        public static void Combo(Obj_AI_Hero target)
        {
            var comboDmg = DamageCalc(target);
            

            if (_r.IsReady() && !ultiReady && comboDmg >= target.Health)
                _r.Cast();


            if (!(_tiamat.IsReady() || _tiamat2.IsReady()) && !_q.IsReady())
                CastW(target);

            if (nextSpell == null && useTiamat == true)
            {
                Console.WriteLine("UseTiamat = true");
                if (_tiamat.IsReady())
                    _tiamat.Cast();
                else if (_tiamat2.IsReady())
                    _tiamat2.Cast();

                useTiamat = false;
            }

            if (nextSpell == null && UseAttack == true)
            {
                Console.WriteLine("UseAttack = true");
                Orbwalking.Orbwalk(target, target.ServerPosition);
            }

            if (nextSpell == _q)
            {
                Console.WriteLine("nextSpell = _q");
                _q.Cast(target.ServerPosition);
                nextSpell = null;
            }

            if (nextSpell == _w)
            {
                Console.WriteLine("nextSpell = _w");
                _w.Cast();
            }

            if (nextSpell == _e)
            {
                Console.WriteLine("nextSpell = _e");
                _e.Cast(currentTarget.ServerPosition);
            }

        }

        public static void OnAnimation(Obj_AI_Base unit, GameObjectPlayAnimationEventArgs args)
        {
            if (unit.IsMe && args.Animation.Contains("Spell1"))
            {
                Utility.DelayAction.Add(Game.Ping + 125, delegate { CancelAnimation(); });
                
            }

        }

        public static void OnGameProcessPacket(GamePacketEventArgs args)
        {
            try
            {
                if (args.PacketData[0] == 101)
                {
                    GamePacket packet = new GamePacket(args.PacketData);
                    packet.Position = 5;
                    int damageType = (int)packet.ReadByte();
                    int targetId = packet.ReadInteger();
                    int sourceId = packet.ReadInteger();

                    if (Player.NetworkId != sourceId)
                        return;

                    Obj_AI_Hero target = ObjectManager.GetUnitByNetworkId<Obj_AI_Hero>(targetId);
                    if (damageType == 12 || damageType == 3)
                    {
                        if (orbwalker.ActiveMode.ToString() == "Combo")
                        {
                            UseAttack = false;
                            orbwalker.SetMovement(true);
                            nextSpell = _q;
                        }
                    }
                }
                else if (args.PacketData[0] == 254)
                {
                    if (orbwalker.ActiveMode.ToString() == "Combo")
                    {
                        GamePacket packet = new GamePacket(args.PacketData);
                        packet.Position = 1;
                        var sourceId = packet.ReadInteger();
                        if (sourceId == Player.NetworkId)
                        {
                            if (_tiamat.IsReady() && Player.Distance(currentTarget.Position) < _tiamat.Range)
                            {
                                Utility.DelayAction.Add(Game.Ping / 2, delegate { _tiamat.Cast(); });
                                Orbwalking.ResetAutoAttackTimer();
                            }
                            if (_tiamat2.IsReady() && Player.Distance(currentTarget.Position) < _tiamat2.Range)
                            {
                                Utility.DelayAction.Add(Game.Ping / 2, delegate { _tiamat2.Cast(); });
                                Orbwalking.ResetAutoAttackTimer();
                            }

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        public static double DamageCalc(Obj_AI_Hero target)
        {
            var health = target.Health;
            var comboDmg = 0.0;
            var qDamage = DamageLib.getDmg(target, DamageLib.SpellType.Q);
            var wDamage = DamageLib.getDmg(target, DamageLib.SpellType.W);
            var tDamage = 0.0;
            
            

            var aDamage = DamageLib.getDmg(target, DamageLib.SpellType.AD);
            var rDamage = DamageLib.getDmg(target, DamageLib.SpellType.R);
            var pDmgMultiplier = 0.2;

            if (_tiamat.IsReady() || _tiamat2.IsReady())
                tDamage = DamageLib.getDmg(target, DamageLib.SpellType.TIAMAT);

            if (!_q.IsReady() && qCount == 0)
                qDamage = 0.0;

            if (!_w.IsReady())
                wDamage = 0.0;

            if (_r.IsReady())
                rDamage = 0.0;

            switch (Player.Level)
            {
                case 1: pDmgMultiplier = 0.2; break;
                case 2:
                case 3: pDmgMultiplier = 0.25; break;
                case 4:
                case 5:
                case 6: pDmgMultiplier = 0.3; break;
                case 7:
                case 8:
                case 9: pDmgMultiplier = 0.35; break;
                case 10:
                case 11:
                case 12: pDmgMultiplier = 0.4; break;
                case 13:
                case 14:
                case 15: pDmgMultiplier = 0.45; break;
                case 16:
                case 17:
                case 18: pDmgMultiplier = 0.5; break;
                default: Console.WriteLine("Weird Player level: " + Player.Level); break;
            }

            var pDamage = DamageLib.CalcPhysicalDmg(pDmgMultiplier * (Player.BaseAttackDamage + Player.FlatPhysicalDamageMod), target);

            comboDmg = pDamage * 3 + aDamage * 3 + qDamage * 3 + wDamage + tDamage + rDamage;
            return comboDmg;
        }
        public static void CancelAnimation()
        {
            Packet.C2S.Move.Encoded(new Packet.C2S.Move.Struct(Game.CursorPos.X, Game.CursorPos.Y)).Send();
            Utility.DelayAction.Add(Game.Ping/2, delegate { Orbwalking.ResetAutoAttackTimer(); });
        }
        public static void OnProcessSpell(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            //Console.WriteLine("Spellname = " + args.SData.Name);
            //RivenTriCleave ItemTiamatCleave RivenFeint RivenMartyr
            if (sender.IsMe)
            {
                var SpellName = args.SData.Name;
                
                if (orbwalker.ActiveMode.ToString() == "Combo")
                {
                    if (SpellName.Contains("Attack") && qCount > 0)
                    {
                        if (_tiamat.IsReady() && Player.Distance(currentTarget.ServerPosition) + currentTarget.BoundingRadius <= _tiamat.Range)
                        {
                            nextSpell = null;
                            useTiamat = true;
                        }
                        else if (_tiamat2.IsReady() && Player.Distance(currentTarget.ServerPosition) + currentTarget.BoundingRadius <= _tiamat2.Range)
                        {
                            nextSpell = null;
                            useTiamat = true;
                        }
                    }
                    else if (SpellName == "RivenTriCleave")
                    {
                        //Utility.DelayAction.Add((int)args.SData.SpellCastTime - Game.Ping/2, delegate { CancelAnimation(); });
                        nextSpell = null;
                        if (_tiamat.IsReady() && Player.Distance(currentTarget.ServerPosition) + currentTarget.BoundingRadius <= _tiamat.Range)
                        {
                            nextSpell = null;
                            useTiamat = true;
                        }
                        else if (_tiamat2.IsReady() && Player.Distance(currentTarget.ServerPosition) + currentTarget.BoundingRadius <= _tiamat2.Range)
                        {
                            nextSpell = null;
                            useTiamat = true;
                        }
                        else if (_w.IsReady() && Player.Distance(currentTarget.Position) <= _w.Range)
                            nextSpell = _w;
                        else
                        {
                            nextSpell = null;
                            UseAttack = true;
                        }

                    }
                    else if (SpellName == "RivenMartyr")
                    {
                        if (_q.IsReady() && Player.Distance(currentTarget.ServerPosition) + currentTarget.BoundingRadius <= _q.Range)
                            nextSpell = _q;
                    }
                    else if (SpellName == "ItemTiamatCleave")
                    {
                        if (_w.IsReady())
                            nextSpell = _w;
                        else if (_q.IsReady() && Player.Distance(currentTarget.ServerPosition) + currentTarget.BoundingRadius <= _q.Range)
                            nextSpell = _q;
                    }
                    else if (SpellName == "RivenFengShuiEngine")
                    {

                        ultiOn = true;
                        if (_tiamat.IsReady() && Player.Distance(currentTarget.ServerPosition) < _tiamat.Range)
                        {
                            nextSpell = null;
                            useTiamat = true;
                        }
                        else if (_tiamat2.IsReady() && Player.Distance(currentTarget.ServerPosition) < _tiamat2.Range)
                        {
                            nextSpell = null;
                            useTiamat = true;
                        }
                        else if (_q.IsReady() && Player.Distance(currentTarget.ServerPosition) + currentTarget.BoundingRadius <= _q.Range)
                        {
                            nextSpell = _q;
                        }
                        else if (_e.IsReady())
                        {
                            nextSpell = _e;
                        }
                    }
                }
            }
        }

        private static void CastW(Obj_AI_Base target)
        {
            
            if (_w.IsReady() && target.Distance(Player.ServerPosition) < _w.Range + target.BoundingRadius)
            {
                _w.Cast();
            }

        }

        private static void CastQ(Obj_AI_Base target, bool force = false)
        {
           if (_q.IsReady())
           {
               if (force)
               {
                   _q.Cast(target.ServerPosition);
               }
               else
               {
                   var qRange = target.BoundingRadius + _q.Range;
                   if (qRange < Player.Distance(target.ServerPosition))
                       _q.Cast(target.ServerPosition);
               }
           }
        }



        private static void GapClose(Obj_AI_Base target)
        {
            var useE = _e.IsReady();
            var useQ = _q.IsReady();

            float aRange = Player.AttackRange + target.BoundingRadius;
            float eRange = target.BoundingRadius + _e.Range;
            float qRange = target.BoundingRadius + _q.Range;
            float eqRange = target.BoundingRadius + _q.Range + _e.Range;
            float distance = Player.Distance(target) + target.BoundingRadius;
            if (distance < aRange)
                return;

            //Use Q first, then EQ, then E to try to not waste E if not needed
            if (qCount < 2 && _q.IsReady() && qRange > distance)
            {
                _q.Cast(target.ServerPosition);
            }
            else if (qCount < 2 && _q.IsReady() && _e.IsReady() && eqRange > distance)
            {
                _e.Cast(target.ServerPosition);
                Utility.DelayAction.Add(300, delegate { CastQ(target, true); });
            } 
            else if (_e.IsReady() && eRange > distance)
            {
                _e.Cast(target.ServerPosition);
            }
           
        }

        private static void KillSecure()
        {
            if (ultiReady && Config.Item("KillSteal").GetValue<bool>())
            {
                foreach (var hero in ObjectManager.Get<Obj_AI_Hero>())
                {
                    if (hero.Team != Player.Team && !hero.IsDead && hero.IsVisible && DamageLib.getDmg(hero, DamageLib.SpellType.R) > hero.Health)
                    {
                        _r.Cast(hero.ServerPosition);
                    }
                }
            }
        }
    }
}
