using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Speech.Synthesis;
using System.Timers;
using LeagueSharp;
using ToasterLoading.Properties;

/*
    Copyright (C) 2014 Nikita Bernthaler

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

namespace ToasterLoading
{
    internal class ToasterLoading
    {
        private const int WM_KEYUP = 0x101;
        private const int Disable = 0x20; // Space
        private const int SecondsToWait = 250;
        private static readonly SpeechSynthesizer Ss = new SpeechSynthesizer();
        private static readonly MainView Mv = new MainView();
        private bool _escaped;
        private Timer _time;
        private Timer _timer;
        private readonly MemoryStream _packet;

        public ToasterLoading()
        {
            Mv.Show();
            _packet = new MemoryStream();
            Game.OnGameSendPacket += OnGameSendPacket;
            Game.OnWndProc += OnWndProc;
            Drawing.OnDraw += OnDraw;
            Ss.Volume = 100;
            Ss.SelectVoice(Ss.GetInstalledVoices()[1].VoiceInfo.Name);
            Ss.SpeakAsync("Toaster Loading by Alxspb Started... Waiting for packet.");
        }

        private void OnWndProc(WndEventArgs args)
        {
            try
            {
                if (args.Msg != WM_KEYUP || args.WParam != Disable || _escaped)
                {
                    return;
                }

                _escaped = true;
                Mv.pictureBox1.Image = Resources.toaster_anim;
                _time = new Timer(5000);
                _time.Elapsed += OnTimeEnd;
                _time.Start();
                Ss.SpeakAsync("Toaster disabled. Game will start is several seconds.");
                Game.SendPacket(_packet.ToArray(), PacketChannel.C2S, PacketProtocolFlags.Reliable);
                _packet.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private void OnDraw(EventArgs args)
        {
            try
            {
                if (_escaped)
                {
                    return;
                }

                Drawing.DrawText(10, 10, Color.Green, Assembly.GetExecutingAssembly().GetName().Name);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                Console.WriteLine(ex.ToString());
            }
        }

        private void OnGameSendPacket(GamePacketEventArgs args)
        {
            try
            {
                if (args.PacketData[0] != 33 || _escaped)
                {
                    return;
                }

                Ss.SpeakAsync("Packet caught. Waiting for escape.");
                args.Process = false;
                _packet.Write(args.PacketData, 0, args.PacketData.Length);
                _timer = new Timer(SecondsToWait*1000);
                _timer.Elapsed += OnTimedEvent;
                Mv.label1.Visible = true;
                Mv.tmer.Start();
                _timer.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            try
            {
                if (!_escaped)
                {
                    Mv.pictureBox1.Image = Properties.Resources.toaster_anim;
                    _time = new Timer(5000);
                    _time.Elapsed += OnTimeEnd;
                    _time.Start();
                    Ss.SpeakAsync("Toaster disabled. Game will start is several seconds.");
                    _escaped = true;
                    Game.SendPacket(_packet.ToArray(), PacketChannel.C2S, PacketProtocolFlags.Reliable);
                    _packet.Close();
                }
                _timer.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private void OnTimeEnd(object source, ElapsedEventArgs e)
        {
            try
            {
                Mv.Hide();
                _timer.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }
}
