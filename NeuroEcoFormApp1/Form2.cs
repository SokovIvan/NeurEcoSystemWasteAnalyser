using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NeuroEcoFormApp1
{
    public partial class Form2 : Form
    {
        private TextBox[] inputFields = new TextBox[6];
        private Label[] labels = new Label[6];
        private Button btnUpload1, btnUpload2, btnAnalyse, btnSubmit;
        private PictureBox pic1, pic2;
        private Label lblResultTitle, lblResultValue;
        private OpenFileDialog openFileDialog;

        public string ImagePath1 { get; private set; }
        public string ImagePath2 { get; private set; }
        public double NeuralResponse { get; private set; }

        public Form2()
        {
            InitializeComponent();
            this.Text = "Point input";
            this.Size = new Size(620, 650);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.BackColor = Color.FromArgb(225, 230, 240);   
            this.ForeColor = Color.Black;
            SetupControls();
        }

        private void SetupControls()
        {
            openFileDialog = new OpenFileDialog
            {
                Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp"
            };

            var title = new Label
            {
                Text = "EcoAnalysis input data",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                Location = new Point(20, 15),
                AutoSize = true
            };
            this.Controls.Add(title);

            string[] labelTexts = new string[]
            {
                "Temperature (°C)",
                "Methan (ppm)",
                "Max temperature (°C)",
                "Min temperature (°C)",
                "Coordinate X",
                "Coordinate Y"
            };

            int startY = 60;
            int labelWidth = 220;
            int fieldX = 240;

            for (int i = 0; i < 6; i++)
            {
                  labels[i] = new Label
                {
                    Text = labelTexts[i] + ":",
                    Location = new Point(20, startY + i * 45),
                    Size = new Size(labelWidth, 25),
                    TextAlign = ContentAlignment.MiddleRight,
                    Font = new Font("Segoe UI", 10)
                };
                this.Controls.Add(labels[i]);

                inputFields[i] = new TextBox
                {
                    Location = new Point(fieldX, startY + i * 45),
                    Size = new Size(140, 25),
                    Font = new Font("Segoe UI", 10)
                };
                this.Controls.Add(inputFields[i]);
            }

            int imagesY = 340;

            var lblImages = new Label
            {
                Text = "Images:",
                Location = new Point(20, imagesY - 30),
                AutoSize = true,
                Font = new Font("Segoe UI", 11, FontStyle.Bold)
            };
            this.Controls.Add(lblImages);

            pic1 = new PictureBox
            {
                Location = new Point(20, imagesY),
                Size = new Size(140, 140),
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White
            };
            this.Controls.Add(pic1);

            pic2 = new PictureBox
            {
                Location = new Point(180, imagesY),
                Size = new Size(140, 140),
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White
            };
            this.Controls.Add(pic2);

            btnUpload1 = new Button
            {
                Text = "Input image 1",
                Location = new Point(20, imagesY + 150),
                Size = new Size(140, 38),
                BackColor = Color.FromArgb(173, 216, 230),  
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 1, BorderColor = Color.White }
            };
            btnUpload1.Click += BtnUpload1_Click;
            this.Controls.Add(btnUpload1);

            btnUpload2 = new Button
            {
                Text = "Input image 2",
                Location = new Point(180, imagesY + 150),
                Size = new Size(140, 38),
                BackColor = Color.FromArgb(173, 216, 230),
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 1, BorderColor = Color.White }
            };
            btnUpload2.Click += BtnUpload2_Click;
            this.Controls.Add(btnUpload2);

            btnAnalyse = new Button
            {
                Text = "Analyse",
                Location = new Point(340, imagesY + 30),
                Size = new Size(240, 45),
                BackColor = Color.FromArgb(100, 149, 237),   
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 }
            };
            btnAnalyse.Click += BtnAnalyse_Click;
            this.Controls.Add(btnAnalyse);

            lblResultTitle = new Label
            {
                Text = "Prediction result:",
                Location = new Point(340, imagesY + 100),
                Size = new Size(240, 30),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
            this.Controls.Add(lblResultTitle);

            lblResultValue = new Label
            {
                Text = "—",
                Location = new Point(340, imagesY + 135),
                Size = new Size(240, 50),
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = Color.DarkBlue,
                TextAlign = ContentAlignment.MiddleCenter,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White
            };
            this.Controls.Add(lblResultValue);

            btnSubmit = new Button
            {
                Text = "Submit",
                Location = new Point(340, imagesY + 200),
                Size = new Size(240, 50),
                BackColor = Color.FromArgb(60, 179, 113),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 }
            };
            btnSubmit.Click += BtnSubmit_Click;
            this.Controls.Add(btnSubmit);
        }

        private void BtnUpload1_Click(object sender, EventArgs e)
        {
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                ImagePath1 = openFileDialog.FileName;
                pic1.Image = Image.FromFile(ImagePath1);
            }
        }

        private void BtnUpload2_Click(object sender, EventArgs e)
        {
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                ImagePath2 = openFileDialog.FileName;
                pic2.Image = Image.FromFile(ImagePath2);
            }
        }

        private void BtnAnalyse_Click(object sender, EventArgs e)
        {
            try
            {
                float tAir = float.Parse(inputFields[0].Text);
                float ch4 = float.Parse(inputFields[1].Text);
                float tMax = float.Parse(inputFields[2].Text);
                float tMin = float.Parse(inputFields[3].Text);

                NeuralResponse = Form1.useModel(
                    ImagePath1,
                    ImagePath2,
                    tAir, ch4, tMax, tMin
                );

                lblResultValue.Text = NeuralResponse.ToString("F4");
                lblResultValue.ForeColor = NeuralResponse > 0.5 ? Color.DarkRed : Color.DarkGreen;
                lblResultTitle.Text = NeuralResponse > 0.5
                    ? "Pollution (1)"
                    : "Clear (0)";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error:\n" + ex.Message,
                    "Erroe", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnSubmit_Click(object sender, EventArgs e)
        {
            if (NeuralResponse == 0 && lblResultValue.Text == "—")
            {
                MessageBox.Show("Analyse first.",
                    "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        public float[] GetData()
        {
            return new float[]
            {
                float.Parse(inputFields[4].Text),   
                float.Parse(inputFields[5].Text),   
                (float)NeuralResponse
            };
        }
    }
}
