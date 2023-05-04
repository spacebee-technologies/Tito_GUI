using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;   //proporciona servicios de interoperabilidad para la comunicación entre componentes de software escritos en lenguajes de programación diferentes. Esto significa que puede usar el espacio de nombres para comunicarse con componentes de software escritos en lenguajes como C#, Visual Basic y C++. Esto se logra mediante la creación de una interfaz común entre los componentes de software, lo que permite que los componentes de software escritos en diferentes lenguajes de programación se comuniquen entre sí.
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System.Globalization;

namespace COMPLETE_FLAT_UI
{
    public partial class FormMenuPrincipal : Form
    {
        //Constructor
        public FormMenuPrincipal()
        {
            InitializeComponent();
            //Estas lineas eliminan los parpadeos del formulario o controles en la interfaz grafica (Pero no en un 100%)
            this.SetStyle(ControlStyles.ResizeRedraw, true);
            this.DoubleBuffered = true;
        }

        //METODO PARA REDIMENCIONAR/CAMBIAR TAMAÑO A FORMULARIO  TIEMPO DE EJECUCION ----------------------------------------------------------
        private int tolerance = 15;
        private const int WM_NCHITTEST = 132;
        private const int HTBOTTOMRIGHT = 17;
        private Rectangle sizeGripRectangle;

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case WM_NCHITTEST:
                    base.WndProc(ref m);
                    var hitPoint = this.PointToClient(new Point(m.LParam.ToInt32() & 0xffff, m.LParam.ToInt32() >> 16));
                    if (sizeGripRectangle.Contains(hitPoint))
                        m.Result = new IntPtr(HTBOTTOMRIGHT);
                    break;
                default:
                    base.WndProc(ref m);
                    break;
            }
        }
        //----------------DIBUJAR RECTANGULO / EXCLUIR ESQUINA PANEL 
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            var region = new Region(new Rectangle(0, 0, this.ClientRectangle.Width, this.ClientRectangle.Height));

            sizeGripRectangle = new Rectangle(this.ClientRectangle.Width - tolerance, this.ClientRectangle.Height - tolerance, tolerance, tolerance);

            region.Exclude(sizeGripRectangle);
            this.panelContenedorPrincipal.Region = region;
            this.Invalidate();
        }

        //----------------COLOR Y GRIP DE RECTANGULO INFERIOR
        protected override void OnPaint(PaintEventArgs e)
        {

            SolidBrush blueBrush = new SolidBrush(Color.FromArgb(55, 61, 69));
            e.Graphics.FillRectangle(blueBrush, sizeGripRectangle);

            base.OnPaint(e);
            ControlPaint.DrawSizeGrip(e.Graphics, Color.Transparent, sizeGripRectangle);
        }
       
        //METODO PARA ARRASTRAR EL FORMULARIO---------------------------------------------------------------------
        [DllImport("user32.DLL", EntryPoint = "ReleaseCapture")]
        private extern static void ReleaseCapture();

        [DllImport("user32.DLL", EntryPoint = "SendMessage")]
        private extern static void SendMessage(System.IntPtr hWnd, int wMsg, int wParam, int lParam);

        private void PanelBarraTitulo_MouseDown(object sender, MouseEventArgs e)
        {
            ReleaseCapture();
            SendMessage(this.Handle, 0x112, 0xf012, 0);
        }

        //METODOS PARA CERRAR,MAXIMIZAR, MINIMIZAR FORMULARIO------------------------------------------------------
        int lx, ly;
        int sw, sh;
        private void btnMaximizar_Click(object sender, EventArgs e)
        {
            lx = this.Location.X;
            ly = this.Location.Y;
            sw = this.Size.Width;
            sh = this.Size.Height;
            this.Size = Screen.PrimaryScreen.WorkingArea.Size;
            this.Location = Screen.PrimaryScreen.WorkingArea.Location;
            btnMaximizar.Visible = false;
            btnNormal.Visible = true;

        }

        private void btnNormal_Click(object sender, EventArgs e)
        {
            this.Size = new Size(sw, sh);
            this.Location = new Point(lx, ly);
            btnNormal.Visible = false;
            btnMaximizar.Visible = true;
        }

        private void btnMinimizar_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
        }

        private void btnCerrar_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("¿Está seguro de cerrar?", "Alerta¡¡", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
               
                Application.Exit();
            }
        }

        private void btnSalir_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("¿Está seguro de cerrar?", "Alerta¡¡", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                Application.Exit();
            }
        }

        //METODOS PARA ANIMACION DE MENU SLIDING--
        private void btnMenu_Click(object sender, EventArgs e)
        {
            //-------CON EFECTO SLIDING
            if (panelMenu.Width == 230)
            {
                this.tmContraerMenu.Start();
            }
            else if (panelMenu.Width == 55)
            {
                this.tmExpandirMenu.Start();
            }

            //-------SIN EFECTO 
            //if (panelMenu.Width == 55)
            //{
            //    panelMenu.Width = 230;
            //}
            //else

            //    panelMenu.Width = 55;
        }

        private void tmExpandirMenu_Tick(object sender, EventArgs e)
        {
            if (panelMenu.Width >= 230)
                this.tmExpandirMenu.Stop();
            else
                panelMenu.Width = panelMenu.Width + 5;
            
        }

        private void tmContraerMenu_Tick(object sender, EventArgs e)
        {
            if (panelMenu.Width <= 55)
                this.tmContraerMenu.Stop();
            else
                panelMenu.Width = panelMenu.Width - 5;
        }

        //METODO PARA ABRIR FORM DENTRO DE PANEL-----------------------------------------------------
        private void AbrirFormEnPanel(object formHijo)
        {
            if (this.panelContenedorForm.Controls.Count > 0)
                this.panelContenedorForm.Controls.RemoveAt(0);
            Form fh = formHijo as Form;
            fh.TopLevel = false;
            fh.FormBorderStyle = FormBorderStyle.None;
            fh.Dock = DockStyle.Fill;            
            this.panelContenedorForm.Controls.Add(fh);
            this.panelContenedorForm.Tag = fh;
            fh.Show();
        }

        //METODO PARA MOSTRAR FORMULARIO DE LOGO Al INICIAR ----------------------------------------------------------
        private void MostrarFormLogo()
        {
            AbrirFormEnPanel(new FormLogo());
        }

        private void FormMenuPrincipal_Load(object sender, EventArgs e)
        {
            string path = Directory.GetCurrentDirectory() + "/config.conf";
            string[] lines = System.IO.File.ReadAllLines(@"" + path);
            ip = lines[0];
            port = lines[1];
            port2 = lines[2];
            port3 = lines[3];

            MostrarFormLogo();
        }

        //METODO PARA MOSTRAR FORMULARIO DE LOGO Al CERRAR OTROS FORM ----------------------------------------------------------
        private void MostrarFormLogoAlCerrarForms(object sender, FormClosedEventArgs e)
        {
            MostrarFormLogo();
        }


        //METODOS PARA ABRIR OTROS FORMULARIOS Y MOSTRAR FORM DE LOGO Al CERRAR ----------------------------------------------------------

        private void button6_Click(object sender, EventArgs e)
        {
            FormDispositivo fm = new FormDispositivo();
            fm.FormClosed += new FormClosedEventHandler(MostrarFormLogoAlCerrarForms);
            AbrirFormEnPanel(fm);

        }

        private void button5_Click(object sender, EventArgs e)
        {
            //AbrirFormEnPanel(new Form1());
            FormUsuario fm = new FormUsuario();
            fm.FormClosed += new FormClosedEventHandler(MostrarFormLogoAlCerrarForms);
            AbrirFormEnPanel(fm);

        }

        private void button3_Click(object sender, EventArgs e)
        {
            FormListaTareas fm = new FormListaTareas();
            fm.FormClosed += new FormClosedEventHandler(MostrarFormLogoAlCerrarForms);
            AbrirFormEnPanel(fm);
        }

        private void button1_Click(object sender, EventArgs e)
        {

        }


        //METODO PARA HORA Y FECHA ACTUAL ----------------------------------------------------------
        private void tmFechaHora_Tick(object sender, EventArgs e)
        {
            lbFecha.Text = DateTime.Now.ToLongDateString();
            lblHora.Text = DateTime.Now.ToString("HH:mm:ssss");
        }
        



        //APLICACION TITO TMTC


        
        String ip="";
        String port = "";
        String port2 = "";
        String port3 = "";
        public UdpClient udpClient;
        public IPEndPoint serverEndPoint;
        UdpClient udpClient2;
        IPEndPoint serverEndPoint2;
        UdpClient udpClient3;
        IPEndPoint serverEndPoint3;

        int conectado = 0;
        private void Iniciar_socket_Tick(object sender, EventArgs e)
        {
            try
            {
                // Crea un socket UDP
                udpClient = new UdpClient();

                // Establece la dirección IP y el puerto del servidor
                IPAddress serverIP = IPAddress.Parse(ip);
                int serverPort = Int32.Parse(port);
                serverEndPoint = new IPEndPoint(serverIP, serverPort);
                IPEndPoint localEndPoint = new IPEndPoint(serverIP, serverPort);
                //udpClient.Client.Bind(localEndPoint);

                // Crea un socket UDP
                udpClient2 = new UdpClient();

                // Establece la dirección IP y el puerto del servidor
                IPAddress serverIP2 = IPAddress.Parse(ip);
                int serverPort2 = Int32.Parse(port2);
                serverEndPoint2 = new IPEndPoint(serverIP2, serverPort2);
                IPEndPoint localEndPoint2 = new IPEndPoint(serverIP2, serverPort2);
                //udpClient2.Client.Bind(localEndPoint2);

                // Crea un socket UDP
                udpClient3 = new UdpClient();

                // Establece la dirección IP y el puerto del servidor
                IPAddress serverIP3 = IPAddress.Parse(ip);
                int serverPort3 = Int32.Parse(port3);
                serverEndPoint3 = new IPEndPoint(serverIP3, serverPort3);
                IPEndPoint localEndPoint3 = new IPEndPoint(serverIP3, serverPort3);
                //udpClient3.Client.Bind(localEndPoint3);
                Iniciar_socket.Enabled = false;
                UDP_read.Enabled = true;


                conectado = 1;
            }
            catch (Exception ee)
            {
                Console.WriteLine(ee.ToString());
            }
        }

        public void send_tc(string dato)
        {
            try
            {
                // Envía mensajes al servidor
                byte[] messageBytes = Encoding.UTF8.GetBytes(dato);
                udpClient.Send(messageBytes, messageBytes.Length, serverEndPoint); //Envio TC port 51524
            }
            catch (Exception eee)
            {
                Console.WriteLine(eee.ToString());
            }
        }
        int i = 0;
        private void UDP_read_Tick(object sender, EventArgs e)
        {
            if (conectado==1) {
                try
                {
                    if (i > 9) { i = 0; }
                    // Envía mensajes al servidor
                    string message = "HOLA TITO, ESTO ES TC"+i.ToString();
                    i++;
                    byte[] messageBytes = Encoding.UTF8.GetBytes(message);
                    udpClient.Send(messageBytes, messageBytes.Length, serverEndPoint); //Envio TC port 51524


                    // Envía mensajes al servidor
                    string message2 = "i";
                    byte[] messageBytes2 = Encoding.UTF8.GetBytes(message);
                    udpClient2.Send(messageBytes2, messageBytes2.Length, serverEndPoint2); //Envio un caracter para que obtenga direccion ip y puerto del lander
                    udpClient3.Send(messageBytes2, messageBytes2.Length, serverEndPoint3); //Envio un caracter para que obtenga direccion ip y puerto del lander

                    // Establece un tiempo de espera de 5 ms para recibir un mensaje       //Recibo TM  PORT 51526
                    udpClient3.Client.ReceiveTimeout = 5;
                    byte[] receiveBytes = udpClient3.Receive(ref serverEndPoint3);
                    string receiveMessage = Encoding.UTF8.GetString(receiveBytes);
                    textBox1.Text = receiveMessage;

                }
                catch (Exception eee)
                {
                    Console.WriteLine(eee.ToString());
                }
            }
           
        }

        private static void ShowErrorDialog(string message)
        {
            MessageBox.Show(message, Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void PanelBarraTitulo_Paint(object sender, PaintEventArgs e)
        {

        }



    }
}
