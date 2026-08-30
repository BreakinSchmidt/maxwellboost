using System;
using System.Drawing;
using System.Windows.Forms;

namespace MaxwellBoost.UI
{
    public class CustomGainDialog : Form
    {
        private readonly NumericUpDown _numericInput;
        public double SelectedGain { get; private set; }

        public CustomGainDialog(double currentGain)
        {
            SelectedGain = currentGain;

            Text = "Set Custom Gain";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = true;
            Width = 320;
            Height = 175;
            TopMost = true;
            Font = SystemFonts.MessageBoxFont ?? Control.DefaultFont;

            var lblPrompt = new Label
            {
                Text = "Enter microphone boost gain (in dB):",
                Location = new Point(20, 18),
                AutoSize = true
            };

            _numericInput = new NumericUpDown
            {
                Location = new Point(20, 45),
                Width = 260,
                Minimum = 0,
                Maximum = 60,
                DecimalPlaces = 1,
                Increment = 0.5m,
                Value = (decimal)Math.Clamp(currentGain, 0.0, 60.0)
            };

            var btnOk = new Button
            {
                Text = "Apply",
                DialogResult = DialogResult.OK,
                Location = new Point(115, 88),
                Width = 80,
                Height = 28
            };

            var btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(200, 88),
                Width = 80,
                Height = 28
            };

            btnOk.Click += (s, e) =>
            {
                SelectedGain = (double)_numericInput.Value;
                Close();
            };

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            Controls.Add(lblPrompt);
            Controls.Add(_numericInput);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);
        }
    }
}
