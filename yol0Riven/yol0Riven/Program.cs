using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;
using Color = System.Drawing.Color;

/* yol0Riven - by yol0swag  */
// wallhopper by blackiechan - adapted for L#

namespace yol0Riven
{
    public struct WallHopPosition
    {
        public Vector3 pA;
        public Vector3 pB;

        public WallHopPosition(Vector3 pA, Vector3 pB)
        {
            this.pA = pA;
            this.pB = pB;
        }
    }

    internal class Program
    {
        public const string Revision = "1.0.0.9";
        public static Obj_AI_Hero Player = ObjectManager.Player;
        public static Orbwalking.Orbwalker orbwalker;

        public static Spell _q = new Spell(SpellSlot.Q, 260);
        public static Spell _w = new Spell(SpellSlot.W, 250);
        public static Spell _e = new Spell(SpellSlot.E, 325);
        public static Spell _r = new Spell(SpellSlot.R, 900);
        public static Items.Item _tiamat = new Items.Item(3077, 400);
        public static Items.Item _tiamat2 = new Items.Item(3074, 400);
        public static Items.Item _ghostblade = new Items.Item(3142, 600);
        public static Menu Config;

        private static int qCount; // 
        private static int pCount; // passive stacks
        private static int lastQCast;

        private static bool ultiOn;
        private static bool ultiReady;

        private static bool ProcessPackets;
        private static Spell nextSpell;
        private static Spell lastSpell;
        private static string lastSpellName;
        private static bool UseAttack;
        private static bool useTiamat;
        private static bool IsKSing;
        private static Obj_AI_Base currentTarget;
        private static int lastGapClose;
        private static bool IsRecalling;

        public static int minRange = 100;
        private static int rotateMultiplier = 15;

        public static List<WallHopPosition> jumpPositions = new List<WallHopPosition>();
        public static Vector3 startPoint;
        public static Vector3 endPoint;
        public static Vector3 directionVector;
        private static Vector3 directionPos;

        public static bool busy = false;

        private static bool IsSR;

        public static void Main(string[] args)
        {
            CustomEvents.Game.OnGameLoad += OnLoad;
        }

        private static void OnLoad(EventArgs args)
        {
            if (Player.ChampionName != "Riven")
                return;

            if (Utility.Map.GetMap()._MapType == Utility.Map.MapType.SummonersRift)
                IsSR = true;
            Config = new Menu("yol0 Riven", "Riven", true);
            Config.AddSubMenu(new Menu("Orbwalker", "Orbwalker"));
            orbwalker = new Orbwalking.Orbwalker(Config.SubMenu("Orbwalker"));

            var tsMenu = new Menu("Target Selector", "Target Selector");
            SimpleTs.AddToMenu(tsMenu);
            Config.AddSubMenu(tsMenu);
            Config.AddToMainMenu();

            Config.AddSubMenu(new Menu("Combo Settings", "Combo"));
            Config.AddSubMenu(new Menu("KS Settings", "KS"));
            Config.AddSubMenu(new Menu("Misc", "Misc"));
            Config.AddSubMenu(new Menu("Draw Settings", "Draw"));
            Config.SubMenu("Combo").AddItem(new MenuItem("UseUlti", "Use Ultimate in Combo").SetValue(true));
            Config.SubMenu("Combo").AddItem(new MenuItem("UseQGapClose", "Use Q to gapclose").SetValue(true));
            Config.SubMenu("KS").AddItem(new MenuItem("KillStealRActivate", "Activate ulti for KS").SetValue(false));
            Config.SubMenu("KS").AddItem(new MenuItem("KillStealR", "KS with R2").SetValue(true));
            Config.SubMenu("KS").AddSubMenu(new Menu("Don't Use R For KS", "NoRKS"));
            foreach (Obj_AI_Hero enemy in ObjectManager.Get<Obj_AI_Hero>().Where(hero => hero.Team != Player.Team))
            {
                Config.SubMenu("KS")
                    .SubMenu("NoRKS")
                    .AddItem(new MenuItem(enemy.ChampionName, enemy.ChampionName).SetValue(false));
            }
            Config.SubMenu("KS").AddItem(new MenuItem("KillStealQ", "KS with Q").SetValue(true));
            Config.SubMenu("KS").AddItem(new MenuItem("KillStealW", "KS with W").SetValue(true));
            Config.SubMenu("KS").AddItem(new MenuItem("KillStealT", "KS with Tiamat").SetValue(true));
            Config.SubMenu("Misc")
                .AddItem(new MenuItem("Flee", "Flee Mode").SetValue(new KeyBind(72, KeyBindType.Press)));
            Config.SubMenu("Misc").AddSubMenu(new Menu("Auto Stun", "AutoStun"));
            foreach (Obj_AI_Hero enemy in ObjectManager.Get<Obj_AI_Hero>().Where(hero => hero.Team != Player.Team))
            {
                Config.SubMenu("Misc")
                    .SubMenu("AutoStun")
                    .AddItem(new MenuItem("Stun" + enemy.ChampionName, "Stun " + enemy.ChampionName).SetValue(false));
            }

            Config.SubMenu("Misc").AddItem(new MenuItem("AutoW", "Auto W Enemies in Range").SetValue(false));
            Config.SubMenu("Misc").AddItem(new MenuItem("AntiGapcloser", "Auto W Gapclosers").SetValue(true));
            Config.SubMenu("Misc").AddItem(new MenuItem("Interrupt", "Auto W Interruptible Spells").SetValue(true));
            Config.SubMenu("Misc").AddItem(new MenuItem("QKeepAlive", "Keep Q Alive").SetValue(true));
            Config.SubMenu("Misc").AddItem(new MenuItem("DCFix", "Try Disconnect Fix").SetValue(false));
            Config.SubMenu("Draw")
                .AddItem(
                    new MenuItem("DrawRanges", "Draw engage range").SetValue(new Circle(true,
                        Color.FromKnownColor(KnownColor.Green))));
            Config.SubMenu("Draw")
                .AddItem(
                    new MenuItem("DrawTarget", "Draw current target").SetValue(new Circle(true,
                        Color.FromKnownColor(KnownColor.Red))));

            if (IsSR)
            {
                Config.SubMenu("Draw").AddItem(new MenuItem("DrawJumps", "Draw Jump spots (always)").SetValue(false));
                Config.SubMenu("Draw")
                    .AddItem(new MenuItem("DrawJumps2", "Draw Jump spots").SetValue(new KeyBind(71, KeyBindType.Press)));
                Config.SubMenu("Draw")
                    .AddItem(new MenuItem("DrawJumpsRange", "Draw Jumps Range").SetValue(new Slider(1000, 200, 10000)));
                Config.AddItem(new MenuItem("WallJump", "Wall Jump").SetValue(new KeyBind(71, KeyBindType.Press)));

                PopulateList();
            }

            _r.SetSkillshot(0.25f, 60f, 2200, false, SkillshotType.SkillshotCone);
            _e.SetSkillshot(0, 0, 1450, false, SkillshotType.SkillshotLine);

            Orbwalking.BeforeAttack += BeforeAttack;
            Obj_AI_Base.OnProcessSpellCast += OnProcessSpell;
            Obj_AI_Base.OnPlayAnimation += OnAnimation;
            Game.OnGameUpdate += OnGameUpdate;
            Game.OnGameUpdate += Buffs_GameUpdate;
            Game.OnGameProcessPacket += OnGameProcessPacket;
            Game.OnWndProc += OnWndProc;
            Drawing.OnDraw += OnDraw;
            AntiGapcloser.OnEnemyGapcloser += OnEnemyGapCloser;
            Interrupter.OnPossibleToInterrupt += OnPossibleToInterrupt;
            if (IsSR)
                Game.OnGameUpdate += Wallhopper_OnGameUpdate;

            Game.PrintChat("<font color=\"#FF0000\">yol0 Riven v" + Revision + " loaded!</font>");
        }

        private static void Buffs_GameUpdate(EventArgs args)
        {
            bool ulti = false;
            bool ulti2 = false;
            bool q = false;

            BuffInstance[] buffList = Player.Buffs;
            foreach (BuffInstance buff in buffList)
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
            {
                ultiReady = false;
                IsKSing = false;
            }

            if (ulti2 == false)
                ultiOn = false;
        }

        private static void OnDraw(EventArgs args)
        {
            if (Config.SubMenu("Draw").Item("DrawRanges").GetValue<Circle>().Active)
            {
                Utility.DrawCircle(Player.Position,
                    (Config.SubMenu("Combo").Item("UseQGapClose").GetValue<bool>() ? _q.Range + _e.Range : _e.Range),
                    Config.SubMenu("Draw").Item("DrawRanges").GetValue<Circle>().Color);
            }
            if (IsSR &&
                (Config.SubMenu("Draw").Item("DrawJumps").GetValue<bool>() ||
                 Config.SubMenu("Draw").Item("DrawJumps2").GetValue<KeyBind>().Active))
            {
                foreach (WallHopPosition pos in jumpPositions)
                {
                    if (Player.Distance(pos.pA) <=
                        Config.SubMenu("Draw").Item("DrawJumpsRange").GetValue<Slider>().Value ||
                        Player.Distance(pos.pB) <=
                        Config.SubMenu("Draw").Item("DrawJumpsRange").GetValue<Slider>().Value)
                    {
                        Utility.DrawCircle(pos.pA, minRange, Color.Green);
                        Utility.DrawCircle(pos.pB, minRange, Color.GreenYellow);
                    }
                }
            }
            if (Config.SubMenu("Draw").Item("DrawTarget").GetValue<Circle>().Active)
            {
                Utility.DrawCircle(currentTarget.ServerPosition, currentTarget.BoundingRadius + 10,
                    Config.SubMenu("Draw").Item("DrawTarget").GetValue<Circle>().Color, 5);
                Utility.DrawCircle(currentTarget.ServerPosition, currentTarget.BoundingRadius + 25,
                    Config.SubMenu("Draw").Item("DrawTarget").GetValue<Circle>().Color, 6);
                Utility.DrawCircle(currentTarget.ServerPosition, currentTarget.BoundingRadius + 45,
                    Config.SubMenu("Draw").Item("DrawTarget").GetValue<Circle>().Color, 7);
            }
        }

        public static void OnEnemyGapCloser(ActiveGapcloser gapcloser)
        {
            if (_w.IsReady() && gapcloser.Sender.IsValidTarget(_w.Range) &&
                Config.SubMenu("Misc").Item("AntiGapcloser").GetValue<bool>())
                _w.Cast();
        }

        public static void OnPossibleToInterrupt(Obj_AI_Base unit, InterruptableSpell spell)
        {
            if (_w.IsReady() && unit.IsValidTarget(_w.Range) &&
                Config.SubMenu("Misc").Item("Interrupt").GetValue<bool>())
                _w.Cast();
        }

        private static void Wallhopper_OnGameUpdate(EventArgs args)
        {
            if (!busy && Config.Item("WallJump").GetValue<KeyBind>().Active && qCount == 2)
            {
                float closest = minRange + 1f;
                foreach (WallHopPosition pos in jumpPositions)
                {
                    if (Player.Distance(pos.pA) < closest || Player.Distance(pos.pB) < closest)
                    {
                        busy = true;
                        if (Player.Distance(pos.pA) < Player.Distance(pos.pB))
                        {
                            closest = Player.Distance(pos.pA);
                            startPoint = pos.pA;
                            endPoint = pos.pB;
                        }
                        else
                        {
                            closest = Player.Distance(pos.pB);
                            startPoint = pos.pB;
                            endPoint = pos.pA;
                        }
                    }
                }
                if (busy)
                {
                    directionVector.X = startPoint.X - endPoint.X;
                    directionVector.Y = startPoint.Y - endPoint.Y;
                    Player.IssueOrder(GameObjectOrder.HoldPosition, Player.ServerPosition);
                    Packet.C2S.Move.Encoded(new Packet.C2S.Move.Struct(startPoint.X, startPoint.Y)).Send();
                    Utility.DelayAction.Add(180, delegate { changeDirection1(); });
                }
            }
        }

        private static void OnGameUpdate(EventArgs args)
        {
            KillSecure();
            AutoStun();
            if (Config.SubMenu("Misc").Item("Flee").GetValue<KeyBind>().Active)
                Flee();

            if (orbwalker.ActiveMode.ToString() == "Combo")
            {
                // try not to switch targets unless needed
                if (currentTarget == null)
                    AcquireTarget();

                if (currentTarget != null &&
                    (currentTarget.IsDead || !currentTarget.IsVisible ||
                     !currentTarget.IsValidTarget(_e.Range + _q.Range + Player.AttackRange)))
                    orbwalker.SetMovement(true);

                if (currentTarget == null)
                    orbwalker.SetMovement(true);


                if (!currentTarget.IsVisible)
                    AcquireTarget();

                if (currentTarget.IsDead)
                    AcquireTarget();

                if (!currentTarget.IsValidTarget(_e.Range + _q.Range + Player.AttackRange))
                    AcquireTarget();

                if (SimpleTs.GetSelectedTarget() != null && SimpleTs.GetSelectedTarget() != currentTarget && SimpleTs.GetSelectedTarget().IsVisible && SimpleTs.GetSelectedTarget().IsValidTarget())
                {
                    currentTarget = SimpleTs.GetSelectedTarget();
                }

                if (!currentTarget.IsDead && currentTarget.IsVisible)
                {
                    GapClose(currentTarget);
                    Combo(currentTarget);
                }
            }
            else
            {
                orbwalker.SetMovement(true);
                if (!IsRecalling && qCount != 0 && lastQCast + (3650 - Game.Ping / 2) < Environment.TickCount &&
                    Config.SubMenu("Misc").Item("QKeepAlive").GetValue<bool>())
                {
                    _q.Cast(Game.CursorPos);
                }
            }
        }

        private static void Flee()
        {
            orbwalker.SetMovement(true);
            if (_q.IsReady())
                _q.Cast(Game.CursorPos, true);
            if (_e.IsReady())
                _e.Cast(Game.CursorPos);
            Player.IssueOrder(GameObjectOrder.MoveTo, Game.CursorPos);
        }

        private static void AutoStun()
        {
            foreach (Obj_AI_Hero enemy in ObjectManager.Get<Obj_AI_Hero>().Where(hero => hero.Team != Player.Team))
            {
                if (_w.IsReady() && enemy.IsValidTarget(_w.Range) &&
                    Config.SubMenu("Misc").SubMenu("AutoStun").Item("Stun" + enemy.ChampionName).GetValue<bool>())
                {
                    _w.Cast();
                }
            }
        }

        private static void AcquireTarget()
        {
            currentTarget = SimpleTs.GetTarget(_e.Range + _q.Range + Player.AttackRange, SimpleTs.DamageType.Physical);
        }

        public static void BeforeAttack(Orbwalking.BeforeAttackEventArgs args)
        {
            // orbwalker cancels autos sometimes, fucks up DPS bad
            if (!args.Target.IsMinion)
                orbwalker.SetMovement(false);
        }

        public static void Combo(Obj_AI_Base target)
        {
            double noRComboDmg = DamageCalcNoR(target);
            if (_r.IsReady() && !ultiReady && noRComboDmg < target.Health &&
                Config.SubMenu("Combo").Item("UseUlti").GetValue<bool>() && currentTarget is Obj_AI_Hero)
            {
                _r.Cast();
            }

            if (!(_tiamat.IsReady() || _tiamat2.IsReady()) && !_q.IsReady() && _w.IsReady() &&
                currentTarget.IsValidTarget(_w.Range))
                _w.Cast();

            if (nextSpell == null && useTiamat)
            {
                if (_tiamat.IsReady())
                    _tiamat.Cast();
                else if (_tiamat2.IsReady())
                    _tiamat2.Cast();

                useTiamat = false;
            }

            if (nextSpell == null && UseAttack)
            {
                Orbwalking.LastAATick = Environment.TickCount + Game.Ping / 2;
                Player.IssueOrder(GameObjectOrder.AttackUnit, currentTarget);
            }

            if (nextSpell == _q)
            {
                _q.Cast(target.Position, true);
                nextSpell = null;
            }

            if (nextSpell == _w)
            {
                _w.Cast();
                nextSpell = null;
            }

            if (nextSpell == _e)
            {
                _e.Cast(currentTarget.ServerPosition);
                nextSpell = null;
            }
        }

        private static void OnWndProc(WndEventArgs args)
        {
            if (MenuGUI.IsChatOpen || Player.Spellbook.SelectedSpellSlot != SpellSlot.Unknown)
                return;

            if (args.WParam == 1 && args.Msg == 257)
            {
                Utility.DelayAction.Add(100, delegate { AcquireTarget(); });
            }
        }

        public static void OnGameProcessPacket(GamePacketEventArgs args)
        {
            try
            {
                if (args.PacketData[0] == 0x65) // damage dealt
                {
                    var packet = new GamePacket(args.PacketData);
                    packet.Position = 1;
                    int targetId = packet.ReadInteger();
                    int damageType = packet.ReadByte();
                    packet.Position = 16;
                    int sourceId = packet.ReadInteger();

                    if (Player.NetworkId != sourceId)
                        return;

                    var target = ObjectManager.GetUnitByNetworkId<Obj_AI_Base>(targetId);

                    if (orbwalker.ActiveMode.ToString() == "Combo")
                    {
                        //4.18 - 4 = basic attack/all spells, 3 = crit attack
                        if ((damageType == 3 || damageType == 4) && lastSpellName.Contains("Attack"))
                        {
                            if (_tiamat.IsReady() && currentTarget.IsValidTarget(_tiamat.Range))
                            {
                                _tiamat.Cast();
                            }
                            else if (_tiamat2.IsReady() && currentTarget.IsValidTarget(_tiamat2.Range))
                            {
                                _tiamat2.Cast();
                            }
                            else if (_w.IsReady() && currentTarget.IsValidTarget(_w.Range) && qCount != 0)
                            {
                                nextSpell = _w;
                            }
                            else
                            {
                                nextSpell = _q;
                            }
                            UseAttack = false;
                            orbwalker.SetMovement(true);
                        }
                    }
                }
                else if (args.PacketData[0] == 0x34)
                {
                    var packet = new GamePacket(args.PacketData);
                    packet.Position = 9;
                    int action = packet.ReadByte();
                    packet.Position = 1;
                    int sourceId = packet.ReadInteger();
                    if (action == 17 && sourceId == Player.NetworkId)
                    {
                        if (ProcessPackets)
                        {

                            if (!Config.SubMenu("Misc").Item("DCFix").GetValue<bool>())
                                CancelAnimation();
                            Orbwalking.ResetAutoAttackTimer();
                        }
                    }
                }
                else if (args.PacketData[0] == 0x61) //move
                {
                    var packet = new GamePacket(args.PacketData);
                    packet.Position = 12;
                    int sourceId = packet.ReadInteger();
                    if (sourceId == Player.NetworkId)
                    {
                        if (currentTarget != null && ProcessPackets && orbwalker.ActiveMode.ToString() == "Combo")
                        {
                            Packet.C2S.Move.Encoded(new Packet.C2S.Move.Struct(currentTarget.ServerPosition.To2D().X,
                                currentTarget.ServerPosition.To2D().Y, 3, currentTarget.NetworkId)).Send();
                            Orbwalking.ResetAutoAttackTimer();
                            ProcessPackets = false;
                        }
                        if (ProcessPackets)
                        {
                            Orbwalking.ResetAutoAttackTimer();
                            ProcessPackets = false;
                        }
                    }
                }
                else if (args.PacketData[0] == 0x38) //animation2
                {
                    var packet = new GamePacket(args.PacketData);
                    packet.Position = 1;
                    int sourceId = packet.ReadInteger();
                    if (packet.Size() == 9 && sourceId == Player.NetworkId)
                    {
                        if (ProcessPackets)
                        {
                            CancelAnimation(); // wait until recv packet 0x61
                            Orbwalking.ResetAutoAttackTimer();
                        }
                    }
                }
                else if (args.PacketData[0] == 0xD8)
                {
                    Packet.S2C.Recall.Struct packet = Packet.S2C.Recall.Decoded(args.PacketData);
                    if (packet.UnitNetworkId == Player.NetworkId)
                    {
                        if (packet.Status == Packet.S2C.Recall.RecallStatus.RecallStarted)
                        {
                            IsRecalling = true;
                        }
                        else if (packet.Status == Packet.S2C.Recall.RecallStatus.RecallAborted ||
                                 packet.Status == Packet.S2C.Recall.RecallStatus.RecallFinished)
                        {
                            IsRecalling = false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        public static void OnAnimation(GameObject unit, GameObjectPlayAnimationEventArgs args)
        {
            if (unit.IsMe && orbwalker.ActiveMode.ToString() == "Combo") // Spell1 = Q
            {
                if (args.Animation.Contains("Spell1"))
                {
                    ProcessPackets = true;
                    if (!Config.SubMenu("Misc").Item("DCFix").GetValue<bool>())
                        CancelAnimation();
                }
            }
        }

        public static void CancelAnimation()
        {
            Vector3 movePos = Game.CursorPos;
            if (currentTarget.IsValidTarget(600))
            {
                movePos = currentTarget.ServerPosition + (Player.ServerPosition - currentTarget.ServerPosition);
                movePos.Normalize();
                movePos *= Player.Distance(currentTarget.ServerPosition) + 55;
            }
            //Packet.C2S.Move.Encoded(new Packet.C2S.Move.Struct(movePos.X, movePos.Y)).Send();
            Player.IssueOrder(GameObjectOrder.MoveTo, movePos);
        }

        private static double GetRDamage(Obj_AI_Base target) // DamageLib doesn't do this correctly yet
        {
            double minDmg = 0.0;
            if (_r.Level == 0)
                return 0.0;

            minDmg = (80 + (40 * (_r.Level - 1))) +
                     0.6 * ((0.2 * (Player.BaseAttackDamage + Player.FlatPhysicalDamageMod)) + Player.FlatPhysicalDamageMod);

            float targetPercentHealthMissing = 100 * (1 - target.Health / target.MaxHealth);
            double dmg = 0.0;
            if (targetPercentHealthMissing > 75.0f)
            {
                dmg = minDmg * 3;
            }
            else
            {
                dmg = minDmg + minDmg * (0.0267 * targetPercentHealthMissing);
            }

            double realDmg = Player.CalcDamage(target, Damage.DamageType.Physical, dmg - 20);
            return realDmg;
        }

        private static double GetUltiQDamage(Obj_AI_Base target) // account for bonus ulti AD
        {
            double dmg = 10 + ((_q.Level - 1) * 20) + 0.6 * (1.2 * (Player.BaseAttackDamage + Player.FlatPhysicalDamageMod));
            return Player.CalcDamage(target, Damage.DamageType.Physical, dmg - 10);
        }

        private static double GetUltiWDamage(Obj_AI_Base target) // account for bonus ulti AD
        {
            float totalAD = Player.FlatPhysicalDamageMod + Player.BaseAttackDamage;
            double dmg = 50 + ((_w.Level - 1) * 30) + (0.2 * totalAD + Player.FlatPhysicalDamageMod);
            return Player.CalcDamage(target, Damage.DamageType.Physical, dmg - 10);
        }

        private static double GetQDamage(Obj_AI_Base target)
        {
            float totalAD = Player.FlatPhysicalDamageMod + Player.BaseAttackDamage;
            double dmg = 10 + ((_q.Level - 1) * 20) + (0.35 + (Player.Level * 0.05)) * totalAD;
            return Player.CalcDamage(target, Damage.DamageType.Physical, dmg - 10);
        }

        private static double GetWDamage(Obj_AI_Base target)
        {
            float dmg = 50 + (_w.Level * 30) + Player.FlatPhysicalDamageMod;
            return Player.CalcDamage(target, Damage.DamageType.Physical, dmg - 10);
        }

        private static double DamageCalcNoR(Obj_AI_Base target)
        {
            float health = target.Health;

            double qDamage = GetQDamage(target);
            double wDamage = GetWDamage(target);
            double tDamage = 0.0;
            double aDamage = Player.GetAutoAttackDamage(target);
            double pDmgMultiplier = 0.2 + (0.05 * Math.Floor(Player.Level / 3.0));
            float totalAD = Player.BaseAttackDamage + Player.FlatPhysicalDamageMod;
            double pDamage = Player.CalcDamage(target, Damage.DamageType.Physical, pDmgMultiplier * totalAD);

            if (_tiamat.IsReady() || _tiamat2.IsReady())
                tDamage = Player.GetItemDamage(target, Damage.DamageItems.Tiamat);

            if (!_q.IsReady() && qCount == 0)
                qDamage = 0.0;

            if (!_w.IsReady())
                wDamage = 0.0;

            return wDamage + tDamage + (qDamage * (3 - qCount)) + (pDamage * (3 - qCount)) + aDamage * (3 - qCount);
        }

        public static double DamageCalcR(Obj_AI_Base target)
        {
            float health = target.Health;
            double qDamage = GetUltiQDamage(target);
            double wDamage = GetUltiWDamage(target);
            double rDamage = GetRDamage(target);
            double tDamage = 0.0;
            float totalAD = Player.FlatPhysicalDamageMod + Player.BaseAttackDamage;


            double aDamage = Player.CalcDamage(target, Damage.DamageType.Physical, 0.2 * totalAD + totalAD);
            double pDmgMultiplier = 0.2 + (0.05 * Math.Floor(Player.Level / 3.0));
            double pDamage = Player.CalcDamage(target, Damage.DamageType.Physical,
                pDmgMultiplier * (0.2 * totalAD + totalAD));
            if (_tiamat.IsReady() || _tiamat2.IsReady())
                tDamage = Player.GetItemDamage(target, Damage.DamageItems.Tiamat);

            if (!_q.IsReady() && qCount == 0)
                qDamage = 0.0;

            if (!_w.IsReady())
                wDamage = 0.0;

            if (_r.IsReady())
                rDamage = 0.0;
            return (pDamage * (3 - qCount)) + (aDamage * (3 - qCount)) + wDamage + tDamage + rDamage +
                   (qDamage * (3 - qCount));
        }


        public static void OnProcessSpell(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (sender.IsMe)
            {
                string SpellName = args.SData.Name;
                lastSpellName = SpellName;
                if (IsKSing && SpellName == "RivenFengShuiEngine") // cancel r animation to fire quickly
                {
                    if (_tiamat.IsReady())
                        _tiamat.Cast();
                    if (_tiamat2.IsReady())
                        _tiamat2.Cast();
                }

                if (SpellName == "RivenTriCleave")
                {
                    lastQCast = Environment.TickCount;
                }

                if (orbwalker.ActiveMode.ToString() == "Combo")
                {
                    lastSpell = null;
                    if (SpellName.Contains("Attack"))
                    {
                        // This should happen in packet too, but just in case :)
                        if (_tiamat.IsReady() && currentTarget.IsValidTarget(_tiamat.Range))
                        {
                            nextSpell = null;
                            useTiamat = true;
                        }
                        else if (_tiamat2.IsReady() && currentTarget.IsValidTarget(_tiamat2.Range))
                        {
                            nextSpell = null;
                            useTiamat = true;
                        }
                    }

                    else if (SpellName == "RivenTriCleave")
                    {
                        nextSpell = null;
                        lastSpell = _q;
                        if (!Config.SubMenu("Misc").Item("DCFix").GetValue<bool>())
                            CancelAnimation();
                        if (Player.Distance(currentTarget.ServerPosition) + currentTarget.BoundingRadius <
                            Player.AttackRange + Player.BoundingRadius)
                        {
                            nextSpell = null;
                            UseAttack = true;
                            return;
                        }
                        if (_w.IsReady() && currentTarget.IsValidTarget(_w.Range))
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
                        if (_q.IsReady())
                        {
                            nextSpell = null;
                            Utility.DelayAction.Add(175, delegate { nextSpell = _q; });
                        }
                        else
                        {
                            nextSpell = null;
                            UseAttack = true;
                        }
                    }
                    else if (SpellName == "ItemTiamatCleave")
                    {
                        // Cancel tiamat animation with W or Q
                        if (_w.IsReady() && currentTarget.IsValidTarget(_w.Range))
                            nextSpell = _w;
                        else if (_q.IsReady() && currentTarget.IsValidTarget(_q.Range))
                            nextSpell = _q;
                    }
                    else if (SpellName == "RivenFengShuiEngine")
                    {
                        ultiOn = true;
                        //Cast tiamat to cancel R animation if target is in range, otherwise Q or E
                        if (_tiamat.IsReady() && currentTarget.IsValidTarget(_tiamat.Range))
                        {
                            nextSpell = null;
                            useTiamat = true;
                        }
                        else if (_tiamat2.IsReady() && currentTarget.IsValidTarget(_tiamat2.Range))
                        {
                            nextSpell = null;
                            useTiamat = true;
                        }
                        else if (_q.IsReady() && currentTarget.IsValidTarget(_q.Range))
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

        private static void GapClose(Obj_AI_Base target)
        {
            bool useE = _e.IsReady();
            bool useQ = _q.IsReady() && qCount < 2 && Config.SubMenu("Combo").Item("UseQGapClose").GetValue<bool>();
            if (lastGapClose + 300 > Environment.TickCount && lastGapClose != 0)
                return;

            lastGapClose = Environment.TickCount;

            float aRange = Player.AttackRange + Player.BoundingRadius + target.BoundingRadius;
            float eRange = aRange + _e.Range;
            float qRange = _q.Range + aRange;
            float eqRange = _q.Range + _e.Range;
            float distance = Player.Distance(target.ServerPosition);
            if (distance < aRange)
                return;

            nextSpell = null;
            useTiamat = false;
            UseAttack = true;
            if (_ghostblade.IsReady())
                _ghostblade.Cast();

            if (useQ && qCount < 2 && _q.IsReady() && qRange > distance && !_e.IsReady())
            {
                double noRComboDmg = DamageCalcNoR(target);
                if (_r.IsReady() && !ultiReady && noRComboDmg < target.Health &&
                    Config.SubMenu("Combo").Item("UseUlti").GetValue<bool>())
                {
                    _r.Cast();
                }
                _q.Cast(target.ServerPosition, true);
            }
            else if (_e.IsReady() && eRange > distance + aRange)
            {
                PredictionOutput pred = Prediction.GetPrediction(target, 0, 0, 1450);
                _e.Cast(pred.CastPosition);
            }
            else if (useQ && _e.IsReady() && _q.IsReady() && eqRange + aRange > distance)
            {
                PredictionOutput pred = Prediction.GetPrediction(target, 0, 0, 1450);
                _e.Cast(pred.CastPosition);
            }
        }

        private static void KillSecure()
        {
            foreach (Obj_AI_Hero hero in ObjectManager.Get<Obj_AI_Hero>())
            {
                if (hero.Team != Player.Team && !hero.IsDead && hero.IsVisible)
                {
                    if (ultiReady && Config.SubMenu("KS").Item("KillStealR").GetValue<bool>() &&
                        hero.IsValidTarget(_r.Range - 30) && GetRDamage(hero) - 20 >= hero.Health &&
                        !Config.SubMenu("KS").SubMenu("NoRKS").Item(hero.ChampionName).GetValue<bool>())
                    {
                        _r.Cast(hero, aoe: true);
                        IsKSing = false;
                    }
                    else if (Config.SubMenu("KS").Item("KillStealQ").GetValue<bool>() && _q.IsReady() &&
                             hero.IsValidTarget(_q.Range) && GetQDamage(hero) - 10 >= hero.Health)
                    {
                        _q.Cast(hero.ServerPosition, true);
                    }
                    else if (Config.SubMenu("KS").Item("KillStealW").GetValue<bool>() && _w.IsReady() &&
                             hero.IsValidTarget(_w.Range) && GetWDamage(hero) - 10 >= hero.Health)
                    {
                        _w.Cast();
                    }
                    else if (Config.SubMenu("KS").Item("KillStealT").GetValue<bool>() &&
                             (_tiamat.IsReady() || _tiamat2.IsReady()) && hero.IsValidTarget(_tiamat.Range) &&
                             Player.GetItemDamage(hero, Damage.DamageItems.Tiamat) >= hero.Health)
                    {
                        if (_tiamat.IsReady())
                            _tiamat.Cast();
                        if (_tiamat2.IsReady())
                            _tiamat2.Cast();
                    }
                    else if (!ultiReady && !ultiOn &&
                             Config.SubMenu("KS").Item("KillStealR").GetValue<bool>() &&
                             Config.SubMenu("KS").Item("KillStealRActivate").GetValue<bool>() &&
                             hero.IsValidTarget(_r.Range - 30) && GetRDamage(hero) - 20 >= hero.Health &&
                             orbwalker.ActiveMode.ToString() != "Combo")
                    {
                        IsKSing = true;
                        _r.Cast();
                    }
                }
            }
        }

        public static void changeDirection1()
        {
            Player.IssueOrder(GameObjectOrder.HoldPosition, Player.ServerPosition);
            Packet.C2S.Move.Encoded(new Packet.C2S.Move.Struct(startPoint.X + directionVector.X / rotateMultiplier,
                startPoint.Y + directionVector.Y / rotateMultiplier)).Send();

            directionPos = new Vector3(startPoint.X, startPoint.Y, startPoint.Z);
            directionPos.X = startPoint.X + directionVector.X / rotateMultiplier;
            directionPos.Y = startPoint.Y + directionVector.Y / rotateMultiplier;
            directionPos.Z = startPoint.Z + directionVector.Z / rotateMultiplier;
            Utility.DelayAction.Add(60, delegate { changeDirection2(); });
        }

        public static void changeDirection2()
        {
            Packet.C2S.Move.Encoded(new Packet.C2S.Move.Struct(startPoint.X, startPoint.Y)).Send();
            Utility.DelayAction.Add(64, delegate { CastJump(); });
        }

        public static void CastJump()
        {
            _q.Cast(endPoint, true);
            Player.IssueOrder(GameObjectOrder.HoldPosition, Player.ServerPosition);
            Utility.DelayAction.Add(1000, delegate { freeFunction(); });
        }

        private static void freeFunction()
        {
            busy = false;
        }

        public static void PopulateList()
        {
            jumpPositions.Add(new WallHopPosition(new Vector3(6393.7299804688f, 8341.7451171875f, -63.87451171875f),
                new Vector3(6612.1625976563f, 8574.7412109375f, 56.018413543701f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(7041.7885742188f, 8810.1787109375f, 0f),
                new Vector3(7296.0341796875f, 9056.4638671875f, 55.610824584961f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(2805.4074707031f, 6140.130859375f, 55.182941436768f),
                new Vector3(2614.3215332031f, 5816.9438476563f, 60.193073272705f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(6696.486328125f, 5377.4013671875f, 61.310482025146f),
                new Vector3(6868.6918945313f, 5698.1455078125f, 55.616455078125f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(2809.3254394531f, 10178.6328125f, -58.759708404541f),
                new Vector3(2553.8962402344f, 9974.4677734375f, 53.364395141602f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(5102.642578125f, 10322.375976563f, -62.845260620117f),
                new Vector3(5483f, 10427f, 54.5009765625f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(6000.2373046875f, 11763.544921875f, 39.544124603271f),
                new Vector3(6056.666015625f, 11388.752929688f, 54.385917663574f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(3319.087890625f, 7472.4760742188f, 55.027889251709f),
                new Vector3(3388.0522460938f, 7101.2568359375f, 54.486026763916f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(3989.9423828125f, 7929.3422851563f, 51.94282913208f),
                new Vector3(3671.623046875f, 7723.146484375f, 53.906265258789f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(4936.8452148438f, 10547.737304688f, -63.064865112305f),
                new Vector3(5156.7397460938f, 10853.216796875f, 52.951190948486f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(5028.1235351563f, 10115.602539063f, -63.082695007324f),
                new Vector3(5423f, 10127f, 55.15357208252f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(6035.4819335938f, 10973.666015625f, 53.918266296387f),
                new Vector3(6385.4013671875f, 10827.455078125f, 54.63500213623f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(4747.0625f, 11866.421875f, 41.584358215332f),
                new Vector3(4743.23046875f, 11505.842773438f, 51.196254730225f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(6749.4487304688f, 12980.83984375f, 44.903495788574f),
                new Vector3(6701.4965820313f, 12610.278320313f, 52.563804626465f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(3114.1865234375f, 9420.5078125f, -42.718975067139f),
                new Vector3(2757f, 9255f, 53.77322769165f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(2786.8354492188f, 9547.8935546875f, 53.645294189453f),
                new Vector3(3002.0930175781f, 9854.39453125f, -53.198081970215f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(3803.9470214844f, 7197.9018554688f, 53.730079650879f),
                new Vector3(3664.1088867188f, 7543.572265625f, 54.18229675293f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(2340.0886230469f, 6387.072265625f, 60.165466308594f),
                new Vector3(2695.6096191406f, 6374.0634765625f, 54.339839935303f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(3249.791015625f, 6446.986328125f, 55.605854034424f),
                new Vector3(3157.4558105469f, 6791.4458007813f, 54.080295562744f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(3823.6242675781f, 5923.9130859375f, 55.420352935791f),
                new Vector3(3584.2561035156f, 6215.4931640625f, 55.6123046875f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(5796.4809570313f, 5060.4116210938f, 51.673671722412f),
                new Vector3(5730.3081054688f, 5430.1635742188f, 54.921173095703f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(6007.3481445313f, 4985.3803710938f, 51.673641204834f),
                new Vector3(6388.783203125f, 4987f, 51.673400878906f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(7040.9892578125f, 3964.6728515625f, 57.192108154297f),
                new Vector3(6668.0073242188f, 3993.609375f, 51.671356201172f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(7763.541015625f, 3294.3481445313f, 54.872283935547f),
                new Vector3(7629.421875f, 3648.0581054688f, 56.908012390137f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(4705.830078125f, 9440.6572265625f, -62.586814880371f),
                new Vector3(4779.9809570313f, 9809.9091796875f, -63.09009552002f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(4056.7907714844f, 10216.12109375f, -63.152275085449f),
                new Vector3(3680.1550292969f, 10182.296875f, -63.701038360596f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(4470.0883789063f, 12000.479492188f, 41.59789276123f),
                new Vector3(4232.9799804688f, 11706.015625f, 49.295585632324f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(5415.5708007813f, 12640.216796875f, 40.682685852051f),
                new Vector3(5564.4409179688f, 12985.860351563f, 41.373748779297f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(6053.779296875f, 12567.381835938f, 40.587882995605f),
                new Vector3(6045.4555664063f, 12942.313476563f, 41.211364746094f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(4454.66015625f, 8057.1313476563f, 42.799690246582f),
                new Vector3(4577.8681640625f, 7699.3686523438f, 53.31339263916f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(7754.7700195313f, 10449.736328125f, 52.890430450439f),
                new Vector3(8096.2885742188f, 10288.80078125f, 53.66955947876f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(7625.3139648438f, 9465.7001953125f, 55.008113861084f),
                new Vector3(7995.986328125f, 9398.1982421875f, 53.530490875244f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(9767f, 8839f, 53.044532775879f),
                new Vector3(9653.1220703125f, 9174.7626953125f, 53.697280883789f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(10775.653320313f, 7612.6943359375f, 55.35241317749f),
                new Vector3(10665.490234375f, 7956.310546875f, 65.222145080566f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(10398.484375f, 8257.8642578125f, 66.200691223145f),
                new Vector3(10176.104492188f, 8544.984375f, 64.849853515625f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(11198.071289063f, 8440.4638671875f, 67.641044616699f),
                new Vector3(11531.436523438f, 8611.0087890625f, 53.454048156738f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(11686.700195313f, 8055.9624023438f, 55.458232879639f),
                new Vector3(11314.19140625f, 8005.4946289063f, 58.438243865967f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(10707.119140625f, 7335.1752929688f, 55.350387573242f),
                new Vector3(10693f, 6943f, 54.870254516602f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(10395.380859375f, 6938.5009765625f, 54.869094848633f),
                new Vector3(10454.955078125f, 7316.7041015625f, 55.308219909668f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(10358.5859375f, 6677.1704101563f, 54.86909866333f),
                new Vector3(10070.067382813f, 6434.0815429688f, 55.294486999512f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(11161.98828125f, 5070.447265625f, 53.730766296387f),
                new Vector3(10783f, 4965f, -63.57177734375f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(11167.081054688f, 4613.9829101563f, -62.898971557617f),
                new Vector3(11501f, 4823f, 54.571090698242f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(11743.823242188f, 4387.4672851563f, 52.005855560303f),
                new Vector3(11379f, 4239f, -61.565242767334f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(10388.120117188f, 4267.1796875f, -63.61775970459f),
                new Vector3(10033.036132813f, 4147.1669921875f, -60.332069396973f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(8964.7607421875f, 4214.3833007813f, -63.284225463867f),
                new Vector3(8569f, 4241f, 55.544258117676f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(5554.8657226563f, 4346.75390625f, 51.680099487305f),
                new Vector3(5414.0634765625f, 4695.6860351563f, 51.611679077148f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(7311.3393554688f, 10553.6015625f, 54.153884887695f),
                new Vector3(6938.5209960938f, 10535.8515625f, 54.441242218018f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(7669.353515625f, 5960.5717773438f, -64.488967895508f),
                new Vector3(7441.2182617188f, 5761.8989257813f, 54.347793579102f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(7949.65625f, 2647.0490722656f, 54.276401519775f),
                new Vector3(7863.0063476563f, 3013.7814941406f, 55.178623199463f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(8698.263671875f, 3783.1169433594f, 57.178703308105f),
                new Vector3(9041f, 3975f, -63.242683410645f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(9063f, 3401f, 68.192077636719f),
                new Vector3(9275.0751953125f, 3712.8935546875f, -63.257461547852f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(12064.340820313f, 6424.11328125f, 54.830627441406f),
                new Vector3(12267.9375f, 6742.9453125f, 54.83561706543f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(11913.165039063f, 5373.34375f, 54.050819396973f),
                new Vector3(11569.1953125f, 5211.7143554688f, 57.787326812744f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(7324.2783203125f, 1461.2199707031f, 52.594970703125f),
                new Vector3(7357.3852539063f, 1837.4309082031f, 54.282878875732f)));
        }
    }
}