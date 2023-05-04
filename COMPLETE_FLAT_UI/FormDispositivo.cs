using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;


namespace COMPLETE_FLAT_UI
{
    public partial class FormDispositivo : Form
    {
        public FormDispositivo()
        {
            InitializeComponent();
        }

        private void BtnCerrar_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void FormDispositivo_Load(object sender, EventArgs e)
        {
            string path = Directory.GetCurrentDirectory() + "/config.conf";
            string[] lines = System.IO.File.ReadAllLines(@"" + path);
            txtid.Text = lines[0];
            textBox_port.Text = lines[1];
            textBox_port2.Text = lines[2];
            textBox_port3.Text = lines[3];
        }

        private void button1_Click(object sender, EventArgs e)
        {
            string path = Directory.GetCurrentDirectory() + "/config.conf";
            string[] lines = { txtid.Text, textBox_port.Text, textBox_port2.Text, textBox_port3.Text };
            System.IO.File.WriteAllLines(path, lines);
            Application.Restart();
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }
    }
}
