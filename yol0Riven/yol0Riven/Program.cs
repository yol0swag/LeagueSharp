//#define RECORDJUMPS
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
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
        public const string Revision = "1.0.0.10";

        public static Obj_AI_Hero Player
        {
            get { return ObjectManager.Player; }
        }

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
        private static int LastCast;

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
        private const int rotateMultiplier = 15;

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

            //if (Utility.Map.GetMap()._MapType == Utility.Map.MapType.SummonersRift)
                //IsSR = true;
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
#if (RECORDJUMPS)
            Config.AddItem(new MenuItem("jumpRecord", "Print Position").SetValue(new KeyBind(32, KeyBindType.Press)));
            Obj_AI_Base.OnProcessSpellCast += JumpRecordProcessSpell;
#endif

            _r.SetSkillshot(0.25f, 60f, 2200, false, SkillshotType.SkillshotCone);
            _e.SetSkillshot(0, 0, 1450, false, SkillshotType.SkillshotLine);

            Orbwalking.BeforeAttack += BeforeAttack;
            Obj_AI_Base.OnProcessSpellCast += OnProcessSpell;
            Obj_AI_Base.OnPlayAnimation += OnAnimation;
            Game.OnGameUpdate += OnGameUpdate;
            Game.OnGameUpdate += Buffs_GameUpdate;
            Game.OnGameProcessPacket += OnGameProcessPacket;
            Drawing.OnDraw += OnDraw;
            AntiGapcloser.OnEnemyGapcloser += OnEnemyGapCloser;
            Interrupter.OnPossibleToInterrupt += OnPossibleToInterrupt;
            if (IsSR)
                Game.OnGameUpdate += Wallhopper_OnGameUpdate;

            Game.PrintChat("<font color=\"#FF0000\">yol0 Riven v" + Revision + " loaded!</font>");
        }

        private static void Buffs_GameUpdate(EventArgs args)
        {
#if (RECORDJUMPS)
            if (Config.Item("jumpRecord").GetValue<KeyBind>().Active)
            {
                Game.PrintChat("x="+Player.Position.X);
                Game.PrintChat("y=" + Player.Position.Y);
                Game.PrintChat("z=" + Player.Position.Z);
            }
#endif

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
#if (RECORDJUMPS)
                        var sA = Drawing.WorldToScreen(pos.pA);
                        var sB = Drawing.WorldToScreen(pos.pB);
                        Drawing.DrawText(sA.X, sA.Y, Color.Green, "#"+jumpPositions.IndexOf(pos));
                        Drawing.DrawText(sB.X, sB.Y, Color.GreenYellow, "#" + jumpPositions.IndexOf(pos));
#endif
                    }
                }
            }
            if (Config.SubMenu("Draw").Item("DrawTarget").GetValue<Circle>().Active)
            {
                Utility.DrawCircle(currentTarget.Position, currentTarget.BoundingRadius + 10,
                    Config.SubMenu("Draw").Item("DrawTarget").GetValue<Circle>().Color, 5);
                Utility.DrawCircle(currentTarget.Position, currentTarget.BoundingRadius + 25,
                    Config.SubMenu("Draw").Item("DrawTarget").GetValue<Circle>().Color, 6);
                Utility.DrawCircle(currentTarget.Position, currentTarget.BoundingRadius + 45,
                    Config.SubMenu("Draw").Item("DrawTarget").GetValue<Circle>().Color, 7);
            }
        }

        public static void OnEnemyGapCloser(ActiveGapcloser gapcloser)
        {
            if (_w.IsReady() && gapcloser.Sender.IsValidTarget(_w.Range) &&
                Config.SubMenu("Misc").Item("AntiGapcloser").GetValue<bool>() && CanCast())
            {
                LastCast = Environment.TickCount;
                _w.Cast();
            }
        }

        public static void OnPossibleToInterrupt(Obj_AI_Base unit, InterruptableSpell spell)
        {
            if (_w.IsReady() && unit.IsValidTarget(_w.Range) &&
                Config.SubMenu("Misc").Item("Interrupt").GetValue<bool>() && CanCast())
            {
                LastCast = Environment.TickCount;
                _w.Cast();
            }
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
                else
                {

                    if (!currentTarget.IsVisible)
                        AcquireTarget();

                    if (currentTarget.IsDead)
                        AcquireTarget();

                    if (!currentTarget.IsValidTarget(_e.Range + _q.Range + Player.AttackRange))
                        AcquireTarget();

                    if (Hud.SelectedUnit != null && Hud.SelectedUnit != currentTarget && Hud.SelectedUnit.IsVisible &&
                        Hud.SelectedUnit is Obj_AI_Hero)
                    {
                        var unit = (Obj_AI_Hero) Hud.SelectedUnit;
                        if (unit.IsValidTarget())
                            currentTarget = (Obj_AI_Base)Hud.SelectedUnit;
                    }

                    if (SimpleTs.GetSelectedTarget() != null && SimpleTs.GetSelectedTarget() != currentTarget &&
                        SimpleTs.GetSelectedTarget().IsVisible && SimpleTs.GetSelectedTarget().IsValidTarget())
                    {
                        currentTarget = SimpleTs.GetSelectedTarget();
                    }

                    if (!currentTarget.IsDead && currentTarget.IsVisible)
                    {
                        GapClose(currentTarget);
                        Combo(currentTarget);
                    }
                }
            }
            else
            {
                orbwalker.SetMovement(true);
                if (!IsRecalling && qCount != 0 && lastQCast + (3650 - Game.Ping / 2) < Environment.TickCount &&
                    Config.SubMenu("Misc").Item("QKeepAlive").GetValue<bool>() && CanCast())
                {
                    LastCast = Environment.TickCount;
                    _q.Cast(Game.CursorPos);
                }
            }
        }

        private static void Flee()
        {
            orbwalker.SetMovement(true);
            if (_q.IsReady() && CanCast())
            {
                LastCast = Environment.TickCount;
                _q.Cast(Game.CursorPos);
            }
            if (_e.IsReady() && CanCast())
            {
                LastCast = Environment.TickCount;
                _e.Cast(Game.CursorPos);
            }
            if (CanCast())
            {
                Player.IssueOrder(GameObjectOrder.MoveTo, Game.CursorPos);
                LastCast = Environment.TickCount;
            }

            
        }

        private static void AutoStun()
        {
            foreach (Obj_AI_Hero enemy in ObjectManager.Get<Obj_AI_Hero>().Where(hero => hero.Team != Player.Team))
            {
                if (_w.IsReady() && enemy.IsValidTarget(_w.Range) &&
                    Config.SubMenu("Misc").SubMenu("AutoStun").Item("Stun" + enemy.ChampionName).GetValue<bool>() && CanCast())
                {
                    LastCast = Environment.TickCount;
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
                Config.SubMenu("Combo").Item("UseUlti").GetValue<bool>() && currentTarget is Obj_AI_Hero && CanCast())
            {
                LastCast = Environment.TickCount;
                _r.Cast();
            }

            if (!(_tiamat.IsReady() || _tiamat2.IsReady()) && !_q.IsReady() && _w.IsReady() &&
                currentTarget.IsValidTarget(_w.Range) && CanCast())
            {
                LastCast = Environment.TickCount;
                _w.Cast();
            }

            if (nextSpell == null && useTiamat)
            {
                if (_tiamat.IsReady() && CanCast())
                {
                    LastCast = Environment.TickCount;
                    _tiamat.Cast();
                }
                else if (_tiamat2.IsReady() && CanCast())
                {
                    LastCast = Environment.TickCount;
                    _tiamat2.Cast();
                }

                useTiamat = false;
            }

            if (nextSpell == null && UseAttack)
            {
                Orbwalking.LastAATick = Environment.TickCount + Game.Ping / 2;
                Player.IssueOrder(GameObjectOrder.AttackUnit, currentTarget);
            }

            if (nextSpell == _q && CanCast())
            {
                LastCast = Environment.TickCount;
                _q.Cast(target.Position);
                nextSpell = null;
            }

            if (nextSpell == _w && CanCast())
            {
                LastCast = Environment.TickCount;
                _w.Cast();
                nextSpell = null;
            }

            if (nextSpell == _e && CanCast())
            {
                LastCast = Environment.TickCount;
                _e.Cast(currentTarget.ServerPosition);
                nextSpell = null;
            }
        }

        public static void OnGameProcessPacket(GamePacketEventArgs args)
        {
            try
            {
                if (args.PacketData[0] == Packet.S2C.Damage.Header) // damage dealt
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
                            if (_tiamat.IsReady() && currentTarget.IsValidTarget(_tiamat.Range) && CanCast())
                            {
                                LastCast = Environment.TickCount;
                                _tiamat.Cast();
                            }
                            else if (_tiamat2.IsReady() && currentTarget.IsValidTarget(_tiamat2.Range) && CanCast())
                            {
                                LastCast = Environment.TickCount;
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

                            if (!Config.SubMenu("Misc").Item("DCFix").GetValue<bool>() && CanCast())
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
                            LastCast = Environment.TickCount;
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
                else if (args.PacketData[0] == Packet.S2C.Teleport.Header)
                {
                    Packet.S2C.Teleport.Struct packet = Packet.S2C.Teleport.Decoded(args.PacketData);
                    if (packet.UnitNetworkId == Player.NetworkId)
                    {
                        if (packet.Status == Packet.S2C.Teleport.Status.Start)
                        {
                            IsRecalling = true;
                        }
                        else if (packet.Status == Packet.S2C.Teleport.Status.Abort ||
                                 packet.Status == Packet.S2C.Teleport.Status.Finish)
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
            if (CanCast())
            {
                LastCast = Environment.TickCount;
                Player.IssueOrder(GameObjectOrder.MoveTo, movePos);
            }
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
                    if (_tiamat.IsReady() && CanCast())
                    {
                        LastCast = Environment.TickCount;
                        _tiamat.Cast();
                    }
                    if (_tiamat2.IsReady() && CanCast())
                    {
                        LastCast = Environment.TickCount;
                        _tiamat2.Cast();
                    }
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
                        if (_tiamat.IsReady() && currentTarget.IsValidTarget(_tiamat.Range) && CanCast())
                        {
                            nextSpell = null;
                            useTiamat = true;
                        }
                        else if (_tiamat2.IsReady() && currentTarget.IsValidTarget(_tiamat2.Range) && CanCast())
                        {
                            nextSpell = null;
                            useTiamat = true;
                        }
                        else if (_q.IsReady() && currentTarget.IsValidTarget(_q.Range) && CanCast())
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
            if (_ghostblade.IsReady() && CanCast())
            {
                LastCast = Environment.TickCount;
                _ghostblade.Cast();
            }

            if (useQ && qCount < 2 && _q.IsReady() && qRange > distance && !_e.IsReady())
            {
                double noRComboDmg = DamageCalcNoR(target);
                if (_r.IsReady() && !ultiReady && noRComboDmg < target.Health &&
                    Config.SubMenu("Combo").Item("UseUlti").GetValue<bool>() && CanCast())
                {
                    LastCast = Environment.TickCount;
                    _r.Cast();
                }
                LastCast = Environment.TickCount;
                _q.Cast(target.ServerPosition);
            }
            else if (_e.IsReady() && eRange > distance + aRange && CanCast())
            {
                PredictionOutput pred = Prediction.GetPrediction(target, 0, 0, 1450);
                _e.Cast(pred.CastPosition);
            }
            else if (useQ && _e.IsReady() && _q.IsReady() && eqRange + aRange > distance && CanCast())
            {
                PredictionOutput pred = Prediction.GetPrediction(target, 0, 0, 1450);
                _e.Cast(pred.CastPosition);
            }
        }

        private static bool CanCast()
        {
            return true;
            //return Environment.TickCount - LastCast > 60;
        }
        private static void KillSecure()
        {
            foreach (Obj_AI_Hero hero in ObjectManager.Get<Obj_AI_Hero>())
            {
                if (hero.Team != Player.Team && !hero.IsDead && hero.IsVisible)
                {
                    if (ultiReady && Config.SubMenu("KS").Item("KillStealR").GetValue<bool>() &&
                        hero.IsValidTarget(_r.Range - 30) && GetRDamage(hero) - 20 >= hero.Health &&
                        !Config.SubMenu("KS").SubMenu("NoRKS").Item(hero.ChampionName).GetValue<bool>() && CanCast())
                    {
                        LastCast = Environment.TickCount;
                        _r.Cast(hero, aoe: true);
                        IsKSing = false;
                    }
                    else if (Config.SubMenu("KS").Item("KillStealQ").GetValue<bool>() && _q.IsReady() &&
                             hero.IsValidTarget(_q.Range) && GetQDamage(hero) - 10 >= hero.Health && CanCast())
                    {
                        LastCast = Environment.TickCount;
                        _q.Cast(hero.ServerPosition);
                    }
                    else if (Config.SubMenu("KS").Item("KillStealW").GetValue<bool>() && _w.IsReady() &&
                             hero.IsValidTarget(_w.Range) && GetWDamage(hero) - 10 >= hero.Health && CanCast())
                    {
                        LastCast = Environment.TickCount;
                        _w.Cast();
                    }
                    else if (Config.SubMenu("KS").Item("KillStealT").GetValue<bool>() &&
                             (_tiamat.IsReady() || _tiamat2.IsReady()) && hero.IsValidTarget(_tiamat.Range) &&
                             Player.GetItemDamage(hero, Damage.DamageItems.Tiamat) >= hero.Health && CanCast())
                    {
                        if (_tiamat.IsReady())
                        {
                            LastCast = Environment.TickCount;
                            _tiamat.Cast();
                        }
                        if (_tiamat2.IsReady())
                        {
                            LastCast = Environment.TickCount;
                            _tiamat2.Cast();
                        }
                    }
                    else if (!ultiReady && !ultiOn &&
                             Config.SubMenu("KS").Item("KillStealR").GetValue<bool>() &&
                             Config.SubMenu("KS").Item("KillStealRActivate").GetValue<bool>() &&
                             hero.IsValidTarget(_r.Range - 30) && GetRDamage(hero) - 20 >= hero.Health &&
                             orbwalker.ActiveMode.ToString() != "Combo" && CanCast())
                    {
                        IsKSing = true;
                        LastCast = Environment.TickCount;
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
            LastCast = Environment.TickCount;
            if (CanCast())
                _q.Cast(endPoint);
            Player.IssueOrder(GameObjectOrder.HoldPosition, Player.ServerPosition);
            Utility.DelayAction.Add(1000, delegate { freeFunction(); });
        }

        private static void freeFunction()
        {
            busy = false;
        }

        public static void PopulateList()
        {
            /*
            jumpPositions.Add(new WallHopPosition(new Vector3(3350f, 10260f, -65.33681f), new Vector3(2974f, 10062f, 54.25977f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(2806f, 9398f, 50.91022f), new Vector3(2504f, 9358f, 51.77357f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(2024f, 8656f, 51.77731f), new Vector3(1602f, 8696f, 52.8381f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(3824f, 7408f, 51.69005f), new Vector3(3601.863f, 7802.986f, 53.75616f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(4374f, 8156f, 48.74363f), new Vector3(3942f, 7856f, 51.82345f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(6534.132f, 8866.133f, -71.2406f), new Vector3(6855.339f, 9091.745f, 52.87077f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(7100f, 8690f, 52.8726f), new Vector3(6776.721f, 8419.823f, -71.2406f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(7174f, 8206f, -22.99838f), new Vector3(7637.021f, 8510.813f, 52.89912f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(8072f, 9806f, 51.05106f), new Vector3(8580.945f, 9796.738f, 50.38408f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(8562f, 9624f, 50.38406f), new Vector3(8569.948f, 9024.744f, 54.0139f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(7672f, 11456f, 50.60836f), new Vector3(7910f, 11806f, 56.4768f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(6474f, 11656f, 55.18494f), new Vector3(6307.973f, 12196.37f, 56.4768f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(4974f, 11856f, 56.75186f), new Vector3(5202.91f, 12261.46f, 56.4768f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(2274f, 4708f, 95.74805f), new Vector3(2350f, 5266f, 52.87057f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(3044f, 4912f, 53.79779f), new Vector3(2844.458f, 4412.269f, 95.74805f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(4586f, 3122f, 95.74808f), new Vector3(5074.371f, 3339.978f, 50.89781f)));
            jumpPositions.Add(new WallHopPosition(new Vector3(5024f, 2608f, 51.22015f), new Vector3(4462.926f, 2370.03f, 95.74805f)));

            */

        }
#if (RECORDJUMPS)
        private static Vector3 lastUsedStart;
        private static Vector3 lastUsedEnd;

        private static void EndPosition()
        {
            lastUsedEnd = Player.ServerPosition;
            const string path = @"jumps.txt";
            if (Player.Distance(lastUsedStart) >= 150)
            {
                

                 File.AppendAllText(path, "jumpPositions.Add(new WallHopPosition(new Vector3(" + lastUsedStart.X + "f, " +
                                 lastUsedStart.Y + "f, " + lastUsedStart.Z + "f), new Vector3(" + lastUsedEnd.X + "f, " +
                                 lastUsedEnd.Y + "f, " + lastUsedEnd.Z + "f)));\n");

            }
        }

        private static void JumpRecordProcessSpell(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (sender.IsMe && args.SData.Name == "RivenTriCleave" && qCount == 2)
            {
                lastUsedStart = Player.ServerPosition;
                Utility.DelayAction.Add(900, delegate { EndPosition(); });
            }
        }
#endif
    }
}