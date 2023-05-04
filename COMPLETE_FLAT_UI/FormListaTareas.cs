using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace COMPLETE_FLAT_UI
{
    
    public partial class FormListaTareas : Form
    {
        
        public FormListaTareas()
        {
            InitializeComponent();
        }

        private void InsertarFilas()
        {
            string path = Directory.GetCurrentDirectory() + "/TC.txt";
            string[] lines = System.IO.File.ReadAllLines(@"" + path);
            /*
            // Agrega las columnas al DataGridView
            string[] headers = lines[0].Split(',');
            foreach (string header in headers)
            {
                dataGridView1.Columns.Add(header, header);
            }
            */
            // Agrega las filas al DataGridView
            for (int i = 1; i < lines.Length; i++)
            {
                string[] fields = lines[i].Split(',');
                dataGridView1.Rows.Add(fields);
            }
        }

        private void btnNuevo_Click(object sender, EventArgs e)
        {
            FormNewTarea frm = new FormNewTarea();
            frm.ShowDialog();
        }

        private void dataGridView1_CellContentDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            /*
            FormListaTareas frm = Owner as FormListaTareas;
            //FormMembresia frm = new FormMembresia();

            frm.txtid.Text = dataGridView1.CurrentRow.Cells[0].Value.ToString();
            frm.txtnombre.Text = dataGridView1.CurrentRow.Cells[1].Value.ToString();
            frm.txtapellido.Text = dataGridView1.CurrentRow.Cells[2].Value.ToString();
            this.Close();
            */
        }

        private void btnEditar_Click(object sender, EventArgs e)
        {
            // Crea un objeto StringBuilder para almacenar los datos del DataGridView
            StringBuilder sb = new StringBuilder();
            /*
            // Agrega los encabezados de columna al StringBuilder
            foreach (DataGridViewColumn column in dataGridView1.Columns)
            {
                sb.Append(column.HeaderText + ",");
            }
            sb.Remove(sb.Length - 1, 1); // Elimina la última coma
            */
            // Agrega las filas al StringBuilder
            foreach (DataGridViewRow row in dataGridView1.Rows)
            {
                sb.Append("\n");
                foreach (DataGridViewCell cell in row.Cells)
                {
                    sb.Append(cell.Value + ",");
                }
                sb.Remove(sb.Length - 1, 1); // Elimina la última coma
            }

            // Guarda los datos en un archivo de texto
            System.IO.File.WriteAllText(Directory.GetCurrentDirectory() + "/TC.txt", sb.ToString());
            MessageBox.Show("Operación completada", "Información", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BtnCerrar_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void FormListaTareas_Load(object sender, EventArgs e)
        {
            InsertarFilas();
        }

        private void button2_Click(object sender, EventArgs e)
        {

        }

        private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {

        }

        private void label2_Click(object sender, EventArgs e)
        {

        }

        private void button2_Click_1(object sender, EventArgs e)
        {
            dataGridView1.Rows.Clear();
            InsertarFilas();
        }
        public void text_TC(string texto)
        {
            richTextBox1.Text = texto;
        }
        private void button1_Click(object sender, EventArgs e)
        {
            if (dataGridView1.SelectedRows.Count == 1)
            {
                DataGridViewRow row = dataGridView1.SelectedRows[0];
                // Aquí puedes acceder a los valores de las celdas de la fila seleccionada
                richTextBox1.Text = row.Cells[3].Value.ToString(); // Valor de la celda
            }
            else
            {
                MessageBox.Show("Seleccione una sola linea", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
