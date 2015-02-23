using System;
using System.Windows.Forms;

namespace ToasterLoading
{
    public partial class MainView : Form
    {
        private static int timeLeft = 250;
        public MainView()
        {
            InitializeComponent();
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
        }

        private void tmer_Tick(object sender, EventArgs e)
        {
            if (timeLeft > 0)
            {
                timeLeft = timeLeft - 1;
                if(timeLeft>59)
                {
                    label1.Text = ((int)(timeLeft / 60)).ToString();
                    label1.Text += ":";
                    label1.Text += timeLeft - ((int)(timeLeft / 60)) * 60;
                }
                else
                {
                    label1.Text = timeLeft.ToString();
                }
            }
            else
            {
                tmer.Stop();
            }
        }
    }
}