using System;
using System.Drawing;
using System.Windows.Forms;
using MaxwellBoost.Config;

namespace MaxwellBoost.UI
{
    public class GainSliderControl : UserControl
    {
        private readonly AppSettings _settings;
        private readonly Action<double> _onGainChanged;

        private readonly Label _label;
        private readonly TrackBar _trackBar;
        private bool _isUpdatingFromExternal;

        public GainSliderControl(AppSettings settings, Action<double> onGainChanged)
        {
            _settings = settings;
            _onGainChanged = onGainChanged;

            DoubleBuffered = true;
            Width = 230;
            Height = 65;
            BackColor = Color.Transparent;
            Padding = new Padding(8, 4, 8, 4);

            _label = new Label
            {
                Text = $"Gain Boost: +{_settings.GainDb:0.#} dB",
                Font = new Font(Control.DefaultFont.FontFamily, 9f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(8, 4),
                ForeColor = Color.Black
            };

            _trackBar = new TrackBar
            {
                Minimum = 0,
                Maximum = 40,
                Value = Math.Clamp((int)Math.Round(_settings.GainDb), 0, 40),
                TickFrequency = 5,
                TickStyle = TickStyle.BottomRight,
                SmallChange = 1,
                LargeChange = 5,
                Location = new Point(4, 24),
                Width = 220,
                Height = 35
            };

            _trackBar.ValueChanged += OnSliderValueChanged;
            _trackBar.MouseUp += (s, e) => ApplyGainChange();
            _trackBar.KeyUp += (s, e) => ApplyGainChange();

            Controls.Add(_label);
            Controls.Add(_trackBar);
        }

        private void OnSliderValueChanged(object? sender, EventArgs e)
        {
            var val = _trackBar.Value;
            _label.Text = $"Gain Boost: +{val} dB";
        }

        private void ApplyGainChange()
        {
            if (_isUpdatingFromExternal) return;

            var val = (double)_trackBar.Value;
            _label.Text = $"Gain Boost: +{val:0.#} dB";
            _onGainChanged?.Invoke(val);
        }

        public void SetGain(double gainDb)
        {
            _isUpdatingFromExternal = true;
            try
            {
                var clamped = Math.Clamp((int)Math.Round(gainDb), 0, 40);
                _trackBar.Value = clamped;
                _label.Text = $"Gain Boost: +{gainDb:0.#} dB";
            }
            finally
            {
                _isUpdatingFromExternal = false;
            }
        }
    }

    public class ToolStripGainSlider : ToolStripControlHost
    {
        public GainSliderControl SliderControl => (GainSliderControl)Control;

        public ToolStripGainSlider(AppSettings settings, Action<double> onGainChanged)
            : base(new GainSliderControl(settings, onGainChanged))
        {
            AutoSize = false;
            Width = 235;
            Height = 70;
            Margin = new Padding(2);
        }
    }
}
