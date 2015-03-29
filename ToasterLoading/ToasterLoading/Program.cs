//#define UPDATEMODE
//#define DISABLED

/**************************
 * 
 * Toaster Loading
 * https://www.joduska.me/forum/topic/35388-
 * Maintained by yol0
 * 
 **************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Timers;
using System.IO;
using LeagueSharp;

namespace ToasterLoading
{
    internal class Program
    {
        private const int WM_KEYUP = 0x101;
        private const int DisableKey = 32;
        private const int SecondsToWait = 250;
        private static bool Escaped;
        private static Timer _escapeTimer; // Kills _failsafeTimer and _ticker when user presses spacebar
        private static Timer _failsafeTimer; // Sends packet if user does not press spacebar after SecondsToWait seconds

        private static Timer _ticker; // For the countdown timer when the packet is caught
        private static int TickerSeconds = SecondsToWait;

        private static MemoryStream packet;
        private static int Stage;
        private static string statusText;
        private static string statusText2;
        private static byte PacketHeader = 103; //TODO: update when OnSendPacket fixed
        private static bool GameStarted;

        static void Main(string[] args)
        {
            Drawing.OnDraw += Drawing_OnDraw;
            Game.OnStart += Game_OnStart;
#if !DISABLED
            packet = new MemoryStream();
            Game.OnSendPacket += Game_OnSendPacket;
            Game.OnWndProc += Game_OnWndProc;
#endif
        }

        private static void Game_OnStart(EventArgs args)
        {
            GameStarted = true;
            
            Game.OnStart -= Game_OnStart;
            Drawing.OnDraw -= Drawing_OnDraw;
#if !DISABLED
            Game.OnWndProc -= Game_OnWndProc;
            Game.OnSendPacket -= Game_OnSendPacket;
#endif
        }

        private static void Drawing_OnDraw(EventArgs args)
        {
            try
            {
                if (!GameStarted)
                {
#if !DISABLED
                    Drawing.DrawText(10, 10, Color.Red, "Toaster Loading:");

                    Color textColor = Color.LightYellow;
                    switch (Stage)
                    {
                        case 0: statusText = "Waiting for packet"; textColor = Color.LightYellow; break;
                        case 1: statusText = "Packet caught. Press spacebar when you're ready to play"; textColor = Color.LimeGreen; break;
                        case 2: statusText = "Toaster disabled. Game will start in several seconds"; textColor = Color.Turquoise; break;
                    }
                    Drawing.DrawText(10, 30, textColor, statusText + statusText2);
#else
                    Drawing.DrawText(10, 10, Color.Tomato, "Toaster Loading is outdated");
#endif
                    
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
            
        }

        private static void Game_OnWndProc(WndEventArgs args)
        {
            try
            {
                if (args.Msg != WM_KEYUP || args.WParam != DisableKey || Escaped)
                    return;
                Escaped = true;
                _escapeTimer = new Timer(5000);
                _escapeTimer.Elapsed += _escapeTimer_Elapsed;
                _escapeTimer.Start();
                Game.SendPacket(packet.ToArray(), PacketChannel.C2S, PacketProtocolFlags.Reliable);
                packet.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private static void Game_OnSendPacket(GamePacketEventArgs args)
        {
            try
            {
#if UPDATEMODE
                if (!GameStarted)
                    Console.WriteLine("Packet Sent: " + args.PacketData[0]);
#endif
                if (args.PacketData[0] != PacketHeader || Escaped)
                    return;
                Stage = 1; // Packet caught
                args.Process = false;
                packet.Write(args.PacketData, 0, args.PacketData.Length);
                _failsafeTimer = new Timer(SecondsToWait * 1000);
                _failsafeTimer.Elapsed += _failsafeTimer_Elapsed;
                _failsafeTimer.Start();
                _ticker = new Timer(1000);
                _ticker.AutoReset = true;
                _ticker.Elapsed += _ticker_Elapsed;
                _ticker.Start();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private static void _escapeTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                _ticker.Close();
                _failsafeTimer.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        static void _ticker_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (TickerSeconds > 0)
            {
                TickerSeconds -= 1;
                var ts = TimeSpan.FromSeconds(TickerSeconds);
                statusText2 = ": " + ts.ToString();
            }
        }

        static void _failsafeTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                if (!Escaped)
                {
                    _escapeTimer = new Timer(5000);
                    _escapeTimer.Elapsed += _escapeTimer_Elapsed;
                    _escapeTimer.Start();
                    Stage = 2; // Toaster disabled
                    Escaped = true;
                    Game.SendPacket(packet.ToArray(), PacketChannel.C2S, PacketProtocolFlags.Reliable);
                    packet.Close();
                }
                _ticker.Close();
                _failsafeTimer.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

        }
    }
}
