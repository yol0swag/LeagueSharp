//#define DEBUGCALC
//#define DEBUGCOMBO
//#define DEBUGPACKETS
//#define DEBUGGAPCLOSE
//#define DEBUGANIMATIONCANCEL
//#define DEBUGRDAMAGE
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
        public static Items.Item _ghostblade = new Items.Item(3142, 600);
        public static Menu Config;

        private static int qCount = 0; // 
        private static int pCount = 0; // passive stacks

        private static bool ultiOn = false;
        private static bool ultiReady = false;

        private static int lastCast = 0;

        private static Spell nextSpell = null;
        private static Spell lastSpell = null;
        private static bool UseAttack = false;
        private static bool useTiamat = false;

        private static Obj_AI_Hero currentTarget = null;
        public static void Main(string[] args)
        {
            CustomEvents.Game.OnGameLoad += OnLoad;
        }

        private static void OnLoad(EventArgs args)
        {
            if (Player.ChampionName != "Riven")
                return;

            Config = new Menu("yol0 Riven", "Riven", true);
            Config.AddSubMenu(new Menu("Orbwalker", "Orbwalker"));
            orbwalker = new Orbwalking.Orbwalker(Config.SubMenu("Orbwalker"));

            var tsMenu = new Menu("Target Selector", "Target Selector");
            SimpleTs.AddToMenu(tsMenu);
            Config.AddSubMenu(tsMenu);
            Config.AddToMainMenu();

            Config.AddItem(new MenuItem("KillSteal", "Killsteal").SetValue(true));
            Config.AddItem(new MenuItem("AntiGapcloser", "Auto W Gapclosers").SetValue(true));
            Config.AddItem(new MenuItem("Interrupt", "Auto W Interruptible Spells").SetValue(true));
            Config.AddItem(new MenuItem("DrawRanges", "Draw engage range").SetValue(true));
            Config.AddItem(new MenuItem("CancelDelay", "Animation Cancel Delay").SetValue <Slider>(new Slider(250, 100, 400)));
            
            
            Orbwalking.BeforeAttack += BeforeAttack;
            Orbwalking.AfterAttack += AfterAttack;
            Obj_AI_Base.OnProcessSpellCast += OnProcessSpell;
            Obj_AI_Base.OnPlayAnimation += OnAnimation;
            Game.OnGameUpdate += OnGameUpdate;
            Game.OnGameUpdate += Buffs_GameUpdate;
            Game.OnGameProcessPacket += OnGameProcessPacket;
            Drawing.OnDraw += OnDraw;
            AntiGapcloser.OnEnemyGapcloser += OnEnemyGapCloser;
            Interrupter.OnPosibleToInterrupt += OnPossibleToInterrupt;
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

        public static void OnEnemyGapCloser(ActiveGapcloser gapcloser)
        {
            if (_w.IsReady() && gapcloser.Sender.IsValidTarget(_w.Range) && Config.Item("AntiGapcloser").GetValue<bool>())
                _w.Cast();
        }

        public static void OnPossibleToInterrupt(Obj_AI_Base unit, InterruptableSpell spell)
        {
            if (_w.IsReady() && unit.IsValidTarget(_w.Range) && Config.Item("Interrupt").GetValue<bool>())
                _w.Cast();
        }


        private static void OnGameUpdate(EventArgs args)
        {
            KillSecure();
            if (orbwalker.ActiveMode.ToString() == "Combo")
            {
                // try not to switch targets unless needed
                if (currentTarget == null)
                    AcquireTarget();

                if (currentTarget != null && (currentTarget.IsDead || !currentTarget.IsVisible || !currentTarget.IsValidTarget(_e.Range + _q.Range + Player.AttackRange)))
                    orbwalker.SetMovement(true);

                if (!currentTarget.IsVisible)
                    AcquireTarget();

                if (currentTarget.IsDead)
                    AcquireTarget();

                if (!currentTarget.IsValidTarget(_e.Range + _q.Range + Player.AttackRange))
                    AcquireTarget();

                if (!currentTarget.IsDead && currentTarget.IsVisible)
                {
                    GapClose(currentTarget);
                    Combo(currentTarget);
                }
                
            }
        }

        private static void AcquireTarget()
        {
            currentTarget = SimpleTs.GetTarget(_e.Range + _q.Range + Player.AttackRange, SimpleTs.DamageType.Physical);
        }

        public static void AfterAttack(Obj_AI_Base hero, Obj_AI_Base target)
        {
            orbwalker.SetMovement(true);
        }

        public static void BeforeAttack(Orbwalking.BeforeAttackEventArgs args)
        {
            // orbwalker cancels autos sometimes, fucks up DPS bad
            if (!args.Target.IsMinion)
                orbwalker.SetMovement(false);
        }

        public static void Combo(Obj_AI_Hero target)
        {
            var noRComboDmg = DamageCalcNoR(target);
            var RComboDmg = DamageCalcR(target);
#if DEBUGCALC
            Console.WriteLine("No R Damage: " + noRComboDmg);
            Console.WriteLine("R Damage: " + RComboDmg);
#endif

            if (_r.IsReady() && !ultiReady && noRComboDmg < target.Health)
            {
                _r.Cast();
            }


            if (!(_tiamat.IsReady() || _tiamat2.IsReady()) && !_q.IsReady())
                CastW(target);

            if (nextSpell == null && useTiamat == true)
            {
#if DEBUGCOMBO
                Console.WriteLine("UseTiamat = true");
#endif
                if (_tiamat.IsReady())
                    _tiamat.Cast();
                else if (_tiamat2.IsReady())
                    _tiamat2.Cast();

                useTiamat = false;
            }

            if (nextSpell == null && UseAttack == true)
            {
#if DEBUGCOMBO
                Console.WriteLine("UseAttack = true");
#endif
                Orbwalking.Orbwalk(target, target.ServerPosition);
            }

            if (nextSpell == _q)
            {
#if DEBUGCOMBO
                Console.WriteLine("nextSpell = _q");
#endif
#if DEBUGGAPCLOSE
                Console.WriteLine("Casting Q in Combo()");
#endif
                _q.Cast(target.ServerPosition);
                nextSpell = null;
            }

            if (nextSpell == _w)
            {
#if DEBUGCOMBO
                Console.WriteLine("nextSpell = _w");
#endif
                _w.Cast();
            }

            if (nextSpell == _e)
            {
#if DEBUGCOMBO
                Console.WriteLine("nextSpell = _e");
#endif
                _e.Cast(currentTarget.ServerPosition);
            }

        }

        

        public static void OnGameProcessPacket(GamePacketEventArgs args)
        {
            try
            {
                if (args.PacketData[0] == 101) // damage dealt
                {
                    GamePacket packet = new GamePacket(args.PacketData);
                    packet.Position = 5;
                    int damageType = (int)packet.ReadByte();
                    int targetId = packet.ReadInteger();
                    int sourceId = packet.ReadInteger();

                    if (Player.NetworkId != sourceId)
                        return;

                    Obj_AI_Base target = ObjectManager.GetUnitByNetworkId<Obj_AI_Hero>(targetId);
#if DEBUGPACKETS
                    Console.WriteLine("DamageType = " + damageType);
#endif
                    if (orbwalker.ActiveMode.ToString() == "Combo")
                    {
                        if (damageType == 12 || damageType == 3 || damageType == 11)
                        {
                            if (_tiamat.IsReady() && Player.Distance(currentTarget.ServerPosition) < _tiamat.Range)
                            {
                                _tiamat.Cast();
                            } else if (_tiamat2.IsReady() && Player.Distance(currentTarget.ServerPosition) < _tiamat2.Range)
                            {
                                _tiamat2.Cast();
                            } else
                            { nextSpell = _q; }
                            UseAttack = false;
                            orbwalker.SetMovement(true);
                        }   
                    }
                }
                else if (args.PacketData[0] == 254) //attack started, auto use tiamat
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
                                Utility.DelayAction.Add(Game.Ping, delegate { _tiamat.Cast(); });
                                Orbwalking.ResetAutoAttackTimer();
                            }
                            if (_tiamat2.IsReady() && Player.Distance(currentTarget.Position) < _tiamat2.Range)
                            {
                                Utility.DelayAction.Add(Game.Ping, delegate { _tiamat2.Cast(); });
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

        private static double GetRDamage(Obj_AI_Hero target) // DamageLib doesn't do this correctly yet
        {
            var minDmg = 0.0;
            if (_r.Level == 0)
                return 0.0;

            minDmg = (80 + (40 * (_r.Level - 1))) + 0.6 * ((0.2 * (Player.BaseAttackDamage + Player.FlatPhysicalDamageMod)) + Player.FlatPhysicalDamageMod); 
            
            var targetPercentHealthMissing = 100* (1 - target.Health / target.MaxHealth);
            var dmg = 0.0;
            if (targetPercentHealthMissing > 75.0f)
            {
                dmg = minDmg * 3;
            }
            else
            {
                dmg = minDmg + minDmg * (0.0267 * targetPercentHealthMissing);
            }
            
            var realDmg = DamageLib.CalcPhysicalDmg(dmg, target);
#if DEBUGRDAMAGE
            Console.WriteLine("R minDmg = " + minDmg);
            Console.WriteLine("R pctHealth = " + targetPercentHealthMissing);
            Console.WriteLine("R predDmg = " + dmg);
            Console.WriteLine("R Damage = " + realDmg);
            Console.WriteLine("Cankill = " + (realDmg > target.Health));
#endif
            return realDmg;
            
        }

        private static double GetUltiQDamage(Obj_AI_Hero target) // account for bonus ulti AD
        {
            var dmg = 10 + ((_q.Level - 1) * 20) + 0.6 * (1.2 * (Player.BaseAttackDamage + Player.FlatPhysicalDamageMod));
            return DamageLib.CalcPhysicalDmg(dmg, target);
        }

        private static double GetUltiWDamage(Obj_AI_Hero target) // account for bonus ulti AD
        {
            var totalAD = Player.FlatPhysicalDamageMod + Player.BaseAttackDamage;
            var dmg = 50 + ((_w.Level - 1) * 30) + (0.2 * totalAD + Player.FlatPhysicalDamageMod);
            return DamageLib.CalcPhysicalDmg(dmg, target);
        }

        private static double GetQDamage(Obj_AI_Hero target)
        {
            var totalAD = Player.FlatPhysicalDamageMod + Player.BaseAttackDamage;
            var dmg = 10 + ((_q.Level - 1) * 20) + (0.35 + (Player.Level * 0.05)) * totalAD;
            return DamageLib.CalcPhysicalDmg(dmg, target);
        }

        private static double GetWDamage(Obj_AI_Hero target)
        {
            var dmg = 50 + (_w.Level * 30) + Player.FlatPhysicalDamageMod;
            return DamageLib.CalcPhysicalDmg(dmg, target);
        }

        private static double DamageCalcNoR(Obj_AI_Hero target)
        {
            var health = target.Health;

            var qDamage = GetQDamage(target);
            var wDamage = GetWDamage(target);
            var tDamage = 0.0;
            var aDamage = DamageLib.getDmg(target, DamageLib.SpellType.AD);
            var pDmgMultiplier = 0.2 + (0.05 * Math.Floor(Player.Level / 3.0));
            var totalAD = Player.BaseAttackDamage + Player.FlatPhysicalDamageMod;
            var pDamage = DamageLib.CalcPhysicalDmg(pDmgMultiplier * totalAD, target);

            if (_tiamat.IsReady() || _tiamat2.IsReady())
                tDamage = DamageLib.getDmg(target, DamageLib.SpellType.TIAMAT);

            if (!_q.IsReady() && qCount == 0)
                qDamage = 0.0;

            if (!_w.IsReady())
                wDamage = 0.0;

            return wDamage + tDamage + (qDamage * (3 - qCount)) + (pDamage * (3 - qCount)) + aDamage * (3 - qCount);
        }

        public static double DamageCalcR(Obj_AI_Hero target)
        {
            var health = target.Health;
            var qDamage = GetUltiQDamage(target);
            var wDamage = GetUltiWDamage(target);
            var rDamage = GetRDamage(target);
            var tDamage = 0.0;
            var totalAD = Player.FlatPhysicalDamageMod + Player.BaseAttackDamage;


            var aDamage = DamageLib.CalcPhysicalDmg(0.2 * totalAD + totalAD, target);
            
            var pDmgMultiplier = 0.2 + (0.05 * Math.Floor(Player.Level / 3.0));
            var pDamage = DamageLib.CalcPhysicalDmg(pDmgMultiplier * (0.2 * totalAD + totalAD), target);
            if (_tiamat.IsReady() || _tiamat2.IsReady())
                tDamage = DamageLib.getDmg(target, DamageLib.SpellType.TIAMAT);

            if (!_q.IsReady() && qCount == 0)
                qDamage = 0.0;

            if (!_w.IsReady())
                wDamage = 0.0;

            if (_r.IsReady())
                rDamage = 0.0;
            return (pDamage * (3 - qCount)) + (aDamage * (3 - qCount)) + wDamage + tDamage + rDamage + (qDamage * (3 - qCount));
        }

        public static void OnAnimation(Obj_AI_Base unit, GameObjectPlayAnimationEventArgs args)
        {
            if (unit.IsMe && args.Animation.Contains("Spell1")) // Spell1 = Q
            {
                //Console.WriteLine("Spell1");
                Utility.DelayAction.Add(Config.Item("CancelDelay").GetValue<int>(), delegate { CancelAnimation(); }); // Which one casts first?
            }
        }

        public static void CancelAnimation()
        {
#if DEBUGANIMATIONCANCEL
            Console.WriteLine("CancelAnimation()");
#endif
            Orbwalking.ResetAutoAttackTimer();
            
            var movePos = Game.CursorPos;
            if (currentTarget.IsValidTarget())
            {
                movePos = currentTarget.ServerPosition + (Player.ServerPosition - currentTarget.ServerPosition);
                movePos.Normalize();
                movePos *= Player.Distance(currentTarget.ServerPosition) + 50;
#if DEBUGANIMATIONCANCEL
                Console.WriteLine("movePos X = " + movePos.X);
                Console.WriteLine("movePos Y = " + movePos.Y);
                Console.WriteLine("movePos Z = " + movePos.Z);
#endif
            }
            Packet.C2S.Move.Encoded(new Packet.C2S.Move.Struct(movePos.X, movePos.Y)).Send();
            //Player.IssueOrder(GameObjectOrder.MoveTo, movePos);
            
        }
        public static void OnProcessSpell(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (sender.IsMe)
            {
                var SpellName = args.SData.Name;
                
                if (orbwalker.ActiveMode.ToString() == "Combo")
                {
                    lastSpell = null;
                    if (SpellName.Contains("Attack"))
                    {
                        // This should happen in packet too, but just in case :)
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
                        //TODO: fiddle with delay until animation cancels correctly
                        lastCast = Environment.TickCount;
                        //Utility.DelayAction.Add(Config.Item("CancelDelay").GetValue<int>(), delegate { CancelAnimation(); });
                        nextSpell = null;
                        lastSpell = _q;
                        if (qCount < 4 && Player.Distance(currentTarget.ServerPosition) + currentTarget.BoundingRadius < Player.AttackRange + Player.BoundingRadius)
                        {
                            nextSpell = null;
                            UseAttack = true;
                            return;
                        }
                        // Tiamat doesn't cancel Q animation, neither does w
                        /*if (_tiamat.IsReady() && Player.Distance(currentTarget.ServerPosition) + currentTarget.BoundingRadius <= _tiamat.Range)
                        {
                            nextSpell = null;
                            useTiamat = true;
                        }
                        else if (_tiamat2.IsReady() && Player.Distance(currentTarget.ServerPosition) + currentTarget.BoundingRadius <= _tiamat2.Range)
                        {
                            nextSpell = null;
                            useTiamat = true;
                        }
                        else */if (_w.IsReady() && Player.Distance(currentTarget.ServerPosition) <= _w.Range)
                            nextSpell = _w;
                        else
                        {
                            nextSpell = null;
                            UseAttack = true;
                        }

                    }
                    else if (SpellName == "RivenMartyr")
                    {
                        // Cancel W animation with Q
                        if (_q.IsReady() && Player.Distance(currentTarget.ServerPosition) + currentTarget.BoundingRadius < _q.Range)
                            nextSpell = _q;
                    }
                    else if (SpellName == "ItemTiamatCleave")
                    {
                        // Cancel tiamat animation with W or Q
                        if (_w.IsReady() && Player.Distance(currentTarget.ServerPosition) < _w.Range + currentTarget.BoundingRadius)
                            nextSpell = _w;
                        else if (_q.IsReady() && Player.Distance(currentTarget.ServerPosition) + currentTarget.BoundingRadius < _q.Range)
                            nextSpell = _q;
                    }
                    else if (SpellName == "RivenFengShuiEngine")
                    {

                        ultiOn = true;
                        //Cast tiamat to cancel R animation if target is in range, otherwise Q or E
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
#if DEBUGGAPCLOSE
            Console.WriteLine("CastQ()");
#endif
           if (_q.IsReady())
           {
               if (force)
               {
                   _q.Cast(target.ServerPosition);
               }
               else if (qCount < 1)
               {
                   var qRange = target.BoundingRadius + _q.Range;
                   if (qRange > Player.Distance(target.ServerPosition))
                       _q.Cast(target.ServerPosition);
               }
           }
        }



        private static void GapClose(Obj_AI_Base target)
        {
            var useE = _e.IsReady();
            var useQ = _q.IsReady();

            float aRange = Player.AttackRange + Player.BoundingRadius + target.BoundingRadius - 50;
            float eRange = aRange + _e.Range;
            float qRange = target.BoundingRadius + _q.Range;
            float eqRange = target.BoundingRadius + _q.Range + _e.Range;
            float distance = Player.Distance(target.ServerPosition);
            if (distance < aRange)
                return;

            if (_ghostblade.IsReady())
                _ghostblade.Cast();

            //Use Q first, then EQ, then E to try to not waste E if not needed
            if (qCount < 2 && _q.IsReady() && qRange < distance && !_e.IsReady())
            {
#if DEBUGGAPCLOSE
                Console.WriteLine("GapClose cond 1");
#endif
                _q.Cast(target.ServerPosition);
            }
            else if (_e.IsReady() && eRange > distance && aRange + 20 > distance)
            {
#if DEBUGGAPCLOSE
                Console.WriteLine("GapClose cond 2");
#endif
                _e.Cast(target.ServerPosition);
                nextSpell = null;
                UseAttack = true;
            }
            else if (qCount < 1 && _q.IsReady() && _e.IsReady() && eqRange > distance)
            {
#if DEBUGGAPCLOSE
                Console.WriteLine("GapClose cond 3");
#endif
                _e.Cast(target.ServerPosition);
                Utility.DelayAction.Add(500, delegate { CastQ(target); });
            } 
            
           
        }

        private static void KillSecure()
        {
            if (ultiReady && Config.Item("KillSteal").GetValue<bool>())
            {
                foreach (var hero in ObjectManager.Get<Obj_AI_Hero>())
                {
                    if (hero.Team != Player.Team && !hero.IsDead && hero.IsVisible && GetRDamage(hero) - 30 > hero.Health && Player.Distance(hero.ServerPosition) > 50)
                    {
                        _r.Cast(hero.ServerPosition);
                    }
                }
            }
        }
    }
}
