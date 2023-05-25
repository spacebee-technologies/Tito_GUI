using System;
using System.Diagnostics;
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
        Stopwatch stopwatch = new Stopwatch();

        //Capa aplicativa de protocolo

        const UInt16 MESSAGE_HANDLER_AREA_VERSION = 1;              //Version del protocolo
        const UInt16 MESSAGE_HANDLER_CRC_SIZE = 2;                  //Tamaño del CRC segun algortimo
        const int MAXIMUM_BUFFER_SIZE = 512;                     //Maximo tamaño del buffer de datos

        //Enumeracion y definiciones de los tipos de interaccion en el protocolo
        const byte MESSAGE_HEADER_INTERACTION_TYPE_SEND = 1;
        const UInt16 MESSAGE_HEADER_INTERACTION_TYPE_SUBMIT = 2;
        const UInt16 MESSAGE_HEADER_INTERACTION_TYPE_REQUEST = 3;
        const UInt16 MESSAGE_HEADER_INTERACTION_TYPE_PUBSUB = 6;

        //Enumeracion y definiciones de los tipos de servicio en el protocolo (Determina si es TM o TC)
        const UInt16 MESSAGE_HEADER_SERVICE_TELEMETRY = 0;
        const UInt16 MESSAGE_HEADER_SERVICE_TELECOMMAND = 1;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct header_t
        {
            public UInt64 timestamp;                       //Marca de tiempo: Tiempo en milisegundos
            public UInt16 interactionType;                 //Tipo de interacción: 1 para ENVIAR, 2 para ENVIAR, 3 para SOLICITAR y 6 para PUBSUB (según CCSDS 521.0-B-2, Sección 4.4.1)
            public byte interactionStage;                 //Etapa de interacción: 1 o 2 según el orden del mensaje
            public UInt64 transactionId;                   //ID de transacción: Identificador incremental único
            public UInt16 service;                         //Servicio: 0 para telemetrías (TM) o 1 para telecomandos (TC)
            public UInt16 operation;                       //Operación: Identificador único para una telemetría o telecomando dado
            public UInt16 areaVersion;                     //Versión del encabezado: Versión del protocolo
            public byte isErrorMessage;                   //Indica si es un mensaje de error: Valor booleano para indicar si es un mensaje de error (0x1 para verdadero, 0x0 para falso)
            public UInt16 bodyLength;                      //Longitud del cuerpo: Longitud en bytes del cuerpo del mensaje
        }
        
        /*Estructura del mensaje completo: Header+body+crc
        +--------------+---------------------+-------------+
        | Header       | Body                | CRC         |
        +--------------+---------------------+-------------+
        |<- 224 bits ->|<- Variable length ->|<- 16 bits ->|
        */
        public unsafe class Message_t
        {
            public header_t header = new header_t();
            //public byte* body;
            public byte[] body = new byte[MAXIMUM_BUFFER_SIZE];
            public byte[] crc = new byte[MESSAGE_HANDLER_CRC_SIZE];
        }

        //Creo dos objetos del tipo mensaje. Uno para recepcion y otro para transmicion
        Message_t message_rx = new Message_t();
        Message_t message_tx = new Message_t();

        /*========================================================================
          Funcion: crc16
          Descripcion: Genera el codigo CRC16 utilizando polinomio normal 0x1021
          Parametro de entrada:  const uint8_t *buffer          = Buffer al cual se quiere generar el CRC
                                 size_t size        = Tamaño del buffer
          Retortna:
			        crc: Valor de CRC obtenido.
          ========================================================================*/
        unsafe UInt16 crc16(byte[] buffer, int size) {
            UInt16 crc = 0xFFFF;
            for (int i = 0; i<size; ++i) {
                crc ^= (UInt16) (buffer[i] << 8);
                for (int j = 0; j< 8; ++j) {
                    if ((crc & 0x8000)!=0) {
                        crc = (UInt16)((crc << 1) ^ 0x1021);
                    } else {
                        crc <<= 1;
                    }
                }
            }
            return (UInt16)crc;
        }


        /*========================================================================
          Funcion: MessageHandler_initializeHeader
          Descripcion: Inicializa el encabezado o header del mensaje
          Parametro de entrada:  Message_t *message             = Puntero al objeto que se desea inicializar el encabezado
                                 uint8_t interactionType        = Indica el tipo de interaccion 1 para ENVIAR, 2 para ENVIAR, 3 para SOLICITAR y 6 para PUBSUB (según CCSDS 521.0-B-2, Sección 4.4.1)
                                 uint8_t interactionStage       = Etapa de interacción: 1 o 2 según el orden del mensaje
                                 MessageHeaderService_t service = 0 para telemetrías (TM) o 1 para telecomandos (TC)
                                 bool isErrorMessage            = Indica si es un mensaje de error: Valor booleano para indicar si es un mensaje de error (0x1 para verdadero, 0x0 para falso)
          No retorna nada
          ========================================================================
        */
        unsafe void MessageHandler_initializeHeader(ref Message_t message, byte interactionType, byte interactionStage, UInt16 service, byte isErrorMessage)
        {
            message.header.interactionType = interactionType;
            message.header.interactionStage = interactionStage;
            message.header.transactionId = 0;                             //Establece el id de transicion en 0
            message.header.service = service;
            message.header.areaVersion = MESSAGE_HANDLER_AREA_VERSION;    //Indica la version del protocolo
            message.header.isErrorMessage = isErrorMessage;
            message.header.timestamp = 0;
        }

        /*========================================================================
          Funcion: MessageHandler_send
          Descripcion: Envia mensaje por UDP con el protocolo
          Parametro de entrada:  Message_t *message             = Puntero al objeto que se desea enviar
                                 char *messageBody              = Cuerpo o mensaje que se desea enviar
                                 uint16_t messageBodySize       = Tamaño del mensaje que se quiere enviar
          No retorna nada
          ========================================================================*/
        unsafe void MessageHandler_send(ref Message_t message, string messageBody, UInt16 messageBodySize)
        {
            
            message.header.timestamp = (ulong) stopwatch.ElapsedMilliseconds; // obtener el tiempo transcurrido                          
            message.header.bodyLength = messageBodySize;
            UInt16 messageFullSize = (UInt16)(sizeof(header_t) + messageBodySize * sizeof(byte) + MESSAGE_HANDLER_CRC_SIZE*sizeof(byte));
            byte[] buffer = new byte[messageFullSize];
            Array.Clear(buffer, 0, buffer.Length);
            
            int memoryOffset = 0;
            Buffer.BlockCopy(BitConverter.GetBytes(message.header.timestamp), 0, buffer, memoryOffset, Marshal.SizeOf(message.header.timestamp));
            memoryOffset += Marshal.SizeOf(message.header.timestamp);
            Buffer.BlockCopy(BitConverter.GetBytes(message.header.interactionType), 0, buffer, memoryOffset, Marshal.SizeOf(message.header.interactionType));
            memoryOffset += Marshal.SizeOf(message.header.interactionType);
            Buffer.BlockCopy(BitConverter.GetBytes(message.header.interactionStage), 0, buffer, memoryOffset, Marshal.SizeOf(message.header.interactionStage));
            memoryOffset += Marshal.SizeOf(message.header.interactionStage);
            Buffer.BlockCopy(BitConverter.GetBytes(message.header.transactionId), 0, buffer, memoryOffset, Marshal.SizeOf(message.header.transactionId));
            memoryOffset += Marshal.SizeOf(message.header.transactionId);
            Buffer.BlockCopy(BitConverter.GetBytes(message.header.service), 0, buffer, memoryOffset, Marshal.SizeOf(message.header.service));
            memoryOffset += Marshal.SizeOf(message.header.service);
            Buffer.BlockCopy(BitConverter.GetBytes(message.header.operation), 0, buffer, memoryOffset, Marshal.SizeOf(message.header.operation));
            memoryOffset += Marshal.SizeOf(message.header.operation);
            Buffer.BlockCopy(BitConverter.GetBytes(message.header.areaVersion), 0, buffer, memoryOffset, Marshal.SizeOf(message.header.areaVersion));
            memoryOffset += Marshal.SizeOf(message.header.areaVersion);
            Buffer.BlockCopy(BitConverter.GetBytes(message.header.isErrorMessage), 0, buffer, memoryOffset, Marshal.SizeOf(message.header.isErrorMessage));
            memoryOffset += Marshal.SizeOf(message.header.isErrorMessage);
            Buffer.BlockCopy(BitConverter.GetBytes(message.header.bodyLength), 0, buffer, memoryOffset, Marshal.SizeOf(message.header.bodyLength));
            memoryOffset += Marshal.SizeOf(message.header.bodyLength);

            //Copio el mensaje a continuacion
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(messageBody), 0, buffer, memoryOffset, messageBodySize * sizeof(byte));

            memoryOffset += messageBodySize * sizeof(byte);
            //Calculo CRC
            char[] crc = new char[MESSAGE_HANDLER_CRC_SIZE];
            UInt16 crc2  = crc16(buffer, sizeof(header_t)+ messageBodySize);

            label3.Text = sizeof(header_t).ToString();
            crc[1] = (char)(crc2 >> 8);
            crc[0] = (char)(crc2 & 0xFF);
            //textBox1.Text = Convert.ToString(crc2, 2);
            //Agrego CRC a buffer
            Buffer.BlockCopy(BitConverter.GetBytes(crc[0]), 0, buffer, memoryOffset, sizeof(byte));
            memoryOffset += sizeof(byte);
            Buffer.BlockCopy(BitConverter.GetBytes(crc[1]), 0, buffer, memoryOffset, sizeof(byte));
            memoryOffset += sizeof(byte);
            //Buffer.BlockCopy(crc, 0, buffer, memoryOffset, MESSAGE_HANDLER_CRC_SIZE * sizeof(byte));
            message.header.transactionId++;
                       

            if (conectado == 1)
            {
                try
                {
                    udpClient.Send(buffer, buffer.Length, serverEndPoint); //Envio TC port 51524
                }
                catch (Exception eee)
                {
                    Console.WriteLine(eee.ToString());
                }
            }
        }


        /*========================================================================
      Funcion: MessageHandler_parseHeader
      Descripcion: Parsea el buffer para obtener los datos del header y almacenarlo en los diferentes campos del objeto
      Parametro de entrada:  Message_t *message             = Puntero al objeto
                             const char *buffer             = Buffer que se desea parsear
      No retorna nada
      ========================================================================*/
        unsafe static int MessageHandler_parseHeader(ref Message_t message, byte[] buffer) {
            
          int memoryOffset = 0;
            try
            {
                message.header.timestamp = BitConverter.ToUInt64(buffer, memoryOffset);
                memoryOffset += Marshal.SizeOf(message.header.timestamp);
                message.header.interactionType = BitConverter.ToUInt16(buffer, memoryOffset);
                memoryOffset += Marshal.SizeOf(message.header.interactionType);
                message.header.interactionStage = buffer[memoryOffset];
                memoryOffset += Marshal.SizeOf(message.header.interactionStage);
                message.header.transactionId = BitConverter.ToUInt64(buffer, memoryOffset);
                memoryOffset += Marshal.SizeOf(message.header.transactionId);
                message.header.service = BitConverter.ToUInt16(buffer, memoryOffset);
                memoryOffset += Marshal.SizeOf(message.header.service);
                message.header.operation = BitConverter.ToUInt16(buffer, memoryOffset);
                memoryOffset += Marshal.SizeOf(message.header.operation);
                message.header.areaVersion = BitConverter.ToUInt16(buffer, memoryOffset);
                memoryOffset += Marshal.SizeOf(message.header.areaVersion);
                message.header.isErrorMessage = buffer[memoryOffset];
                memoryOffset += Marshal.SizeOf(message.header.isErrorMessage);
                message.header.bodyLength = BitConverter.ToUInt16(buffer, memoryOffset);
                memoryOffset += Marshal.SizeOf(message.header.bodyLength);
                return memoryOffset;
             
            }
            catch (FormatException e)
            {
                return 0;
            }
            


        }

    /*========================================================================
      Funcion: MessageHandler_parseBody
      Descripcion: Parsea el buffer para obtener los datos del body y almacenarlo en el campo body del objeto
      Parametro de entrada:  Message_t *message             = Puntero al objeto
                             const char *buffer             = Buffer que se desea parsear
                             size_t bufferLength            = Tamaño del buffer
      No retorna nada
      ========================================================================*/
    unsafe void MessageHandler_parseBody(ref Message_t message, byte[] buffer, UInt16 bufferLength, int offsetmem)
    {
        int bodySize=(int)message.header.bodyLength * sizeof(byte);
        Array.Clear(message.body, 0, message.body.Length);
        Buffer.BlockCopy(buffer, offsetmem, message.body, 0, bodySize);
     }

    /*========================================================================
      Funcion: MessageHandler_parseCrc
      Descripcion: Parsea el buffer para obtener los datos del crc y almacenarlo en el campo crc del objeto
      Parametro de entrada:  Message_t *message             = Puntero al objeto
                             const char *buffer             = Buffer que se desea parsear
                             size_t bufferLength            = Tamaño del buffer
      No retorna nada
      ========================================================================*/
    static void MessageHandler_parseCrc(ref Message_t message, byte[] buffer, UInt16 bufferLength, int offsetmem)
    {
            Buffer.BlockCopy(buffer, offsetmem, message.crc, 0, MESSAGE_HANDLER_CRC_SIZE * sizeof(byte));
            //memcpy(message->crc, buffer + bufferLength - MESSAGE_HANDLER_CRC_SIZE, MESSAGE_HANDLER_CRC_SIZE);
        }

    /*========================================================================
      Funcion: MessageHandler_parse
      Descripcion: Parsea el buffer y almacena los datos en los campos correspondiente del objeto
      Parametro de entrada:  Message_t *message             = Puntero al objeto
                             const char *buffer             = Puntero al buffer que se desea parsear
                             size_t bufferLength            = Tamaño del buffer
      Retorna:  
                False: Si no se pudo realizar 
                True:  Si se pudo realizar o si fallo la comprobacion de CRC
      ========================================================================*/
    unsafe bool MessageHandler_parse(ref Message_t message, byte[] buffer, UInt16 bufferLength)
    {
        UInt16 minMessageLength = (UInt16)(sizeof(header_t) + MESSAGE_HANDLER_CRC_SIZE);                     //Tamaño minimo del mensaje
        if (bufferLength < minMessageLength) { return false; }                                              //Si el mensaje posee menos bytes, se retorna falso

        int memoryOffset= MessageHandler_parseHeader(ref message, buffer);                                                      //Se parsea el header
        UInt16 crc2 = crc16(buffer, (int)(memoryOffset + message.header.bodyLength * sizeof(byte)));             //Se calcula el CRC

        UInt16 expectedMessageLength = (UInt16)(sizeof(header_t) + message.header.bodyLength * sizeof(byte) + MESSAGE_HANDLER_CRC_SIZE * sizeof(byte)); //Obtengo el tamaño que deberia tener el mensaje (tamaño header+tamaño que se indica que es el mensaje+ tamaño crc)
        if (bufferLength != expectedMessageLength) { return false; }                                      //Si el tamaño del mensaje es diferente del que se espera, se devuelve false
        MessageHandler_parseBody(ref message, buffer, bufferLength, memoryOffset);
        MessageHandler_parseCrc(ref message, buffer, bufferLength, memoryOffset+ (int)message.header.bodyLength);
            
            label6.Text = Convert.ToString(crc2, 2);
        if ((message.crc[1] == (crc2 >> 8)) && (message.crc[0] == (crc2 & 0xFF)))
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    //Fin capa aplicativa de protocolo



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
            // Iniciar el cronómetro
            stopwatch.Start();
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
                //udpClient.Send(messageBytes, messageBytes.Length, serverEndPoint); //Envio TC port 51524
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
                    MessageHandler_send(ref message_tx, message, (ushort)message.Length);


                    // Envía mensajes al servidor
                    string message2 = "i";
                    byte[] messageBytes2 = Encoding.UTF8.GetBytes(message);
                    udpClient2.Send(messageBytes2, messageBytes2.Length, serverEndPoint2); //Envio un caracter para que obtenga direccion ip y puerto del lander
                    udpClient3.Send(messageBytes2, messageBytes2.Length, serverEndPoint3); //Envio un caracter para que obtenga direccion ip y puerto del lander

                    // Establece un tiempo de espera de 5 ms para recibir un mensaje       //Recibo TM  PORT 51526
                    udpClient3.Client.ReceiveTimeout = 5;
                    byte[] receiveBytes = udpClient3.Receive(ref serverEndPoint3);
                    bool isCorrectMessage = MessageHandler_parse(ref message_rx, receiveBytes, (UInt16)receiveBytes.Length);
                    //bool isCorrectMessage = false;
                    if (isCorrectMessage)
                    {           //Si no hay error de tamaño en el parceo
                                //Aca se debe verificar CRC
                        label4.Text = "OK";
                        textBox1.Text = Encoding.ASCII.GetString(message_rx.body);
                    }
                    else
                    {
                        label4.Text = "Error";
                    }


                }
                catch (Exception eee)
                {
                    Console.WriteLine(eee.ToString());
                }
            }
           
        }

        private unsafe void button2_Click(object sender, EventArgs e)
        {
            message_rx.header.timestamp = 10;
            MessageHandler_initializeHeader(ref message_tx, MESSAGE_HEADER_INTERACTION_TYPE_SEND,1, MESSAGE_HEADER_SERVICE_TELECOMMAND, 0);
            //label6.Text = message_rx.header.timestamp.ToString();
            MessageHandler_send(ref message_tx, "Hello", 5);


        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

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
