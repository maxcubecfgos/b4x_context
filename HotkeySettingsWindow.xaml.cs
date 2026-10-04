using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace b4x_context
{
    public partial class HotkeySettingsWindow : Window
    {
        public string Modifiers { get; private set; }
        public string KeyName { get; private set; }
        /// <summary>Selected context window in tokens (one of <see cref="B4XContext.Services.ContextBudget.Stops"/>).</summary>
        public int ContextTokens { get; private set; }
        public string LocalEndpoint { get; private set; } = B4XContext.Services.LocalCompactor.DefaultEndpoint;
        public string LocalModel { get; private set; } = B4XContext.Services.LocalCompactor.DefaultModel;
        private bool _capturing = false;
        private string _initialModel;

        public HotkeySettingsWindow(string currentModifiers, string currentKey)
            : this(currentModifiers, currentKey, B4XContext.Services.ContextBudget.DefaultContext,
                B4XContext.Services.LocalCompactor.DefaultEndpoint, B4XContext.Services.LocalCompactor.DefaultModel)
        {
        }

        public HotkeySettingsWindow(string currentModifiers, string currentKey, int currentContext,
            string currentEndpoint, string currentModel)
        {
            InitializeComponent();
            Modifiers = currentModifiers;
            KeyName = currentKey;
            CurrentHotkeyText.Text = $"Current: {Format(currentModifiers, currentKey)}";
            CaptureBox.Text = "Click here, then press a key combo…";

            var stops = B4XContext.Services.ContextBudget.Stops;
            var nearest = B4XContext.Services.ContextBudget.NearestStop(currentContext);
            int idx = System.Array.IndexOf(stops, nearest);
            if (idx < 0) idx = System.Array.IndexOf(stops, B4XContext.Services.ContextBudget.DefaultContext);
            ContextSlider.Value = idx;
            RenderContextLabel();

            EndpointBox.Text = string.IsNullOrWhiteSpace(currentEndpoint)
                ? B4XContext.Services.LocalCompactor.DefaultEndpoint : currentEndpoint;
            _initialModel = string.IsNullOrWhiteSpace(currentModel)
                ? B4XContext.Services.LocalCompactor.DefaultModel : currentModel;
            ModelBox.Text = _initialModel;
            ContextTokens = nearest;
            LocalEndpoint = EndpointBox.Text;
            LocalModel = ModelBox.Text;

            EndpointBox.LostFocus += (s, e) => _ = LoadModelsAsync();
            _ = LoadModelsAsync();
        }

        /// <summary>Fetches the models served by the endpoint so the user picks a real one.</summary>
        private async System.Threading.Tasks.Task LoadModelsAsync()
        {
            try
            {
                var endpoint = string.IsNullOrWhiteSpace(EndpointBox.Text)
                    ? B4XContext.Services.LocalCompactor.DefaultEndpoint : EndpointBox.Text;
                // Prefer the current selection; with no items yet Text can read empty on a
                // non-editable ComboBox, so fall back to the model configured at open time.
                var initial = ModelBox.SelectedItem != null ? ModelBox.Text : _initialModel;
                var models = await B4XContext.Services.LocalCompactor.ListModelsAsync(endpoint);

                if (models.Count == 0)
                {
                    ModelStatusText.Text = $"No models reported at {B4XContext.Services.LocalCompactor.BaseUrl(endpoint)} — start Ollama (ollama serve) or check the endpoint.";
                    return;
                }

                ModelBox.ItemsSource = models;
                int idx = models.FindIndex(m => !string.IsNullOrWhiteSpace(initial) &&
                                                 m.Equals(initial, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0)
                    ModelBox.SelectedIndex = idx; // keep the configured model
                else
                    ModelBox.SelectedItem = B4XContext.Services.LocalCompactor.PickModel(initial, models);

                var shown = models.Count > 6
                    ? string.Join(", ", models.Take(6)) + ", …"
                    : string.Join(", ", models);
                ModelStatusText.Text = $"{models.Count} model(s) available: {shown}";
            }
            catch (Exception ex)
            {
                ModelStatusText.Text = $"Could not list models: {ex.Message}";
            }
        }

        private void ContextSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            RenderContextLabel();
        }

        private void RenderContextLabel()
        {
            // x:Name fields are not assigned yet while InitializeComponent builds the tree.
            if (ContextSlider == null || ContextValueText == null || ContextHintText == null) return;

            var stops = B4XContext.Services.ContextBudget.Stops;
            int idx = (int)Math.Round(ContextSlider.Value);
            if (idx < 0) idx = 0;
            if (idx > stops.Length - 1) idx = stops.Length - 1;
            int tokens = stops[idx];
            ContextValueText.Text = B4XContext.Services.ContextBudget.Label(tokens);
            int reserved = B4XContext.Services.ContextBudget.ReservedOutput(tokens);
            int usable = B4XContext.Services.ContextBudget.Usable(tokens);
            ContextHintText.Text = $"{tokens:N0} tokens — {reserved:N0} reserved for the answer, {usable:N0} usable for the prompt.";
            ContextTokens = tokens;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            ContextTokens = B4XContext.Services.ContextBudget.Stops[(int)Math.Round(ContextSlider.Value)];
            LocalEndpoint = string.IsNullOrWhiteSpace(EndpointBox.Text)
                ? B4XContext.Services.LocalCompactor.DefaultEndpoint : EndpointBox.Text.Trim();
            // Fall back to the model that was already configured, not to the default:
            // with the endpoint offline the combo has no items and Text is empty.
            LocalModel = string.IsNullOrWhiteSpace(ModelBox.Text)
                ? _initialModel : ModelBox.Text.Trim();
            DialogResult = true;
            Close();
        }

        private string Format(string mods, string key) => string.IsNullOrEmpty(mods) ? key : mods + "+" + key;

        private void CaptureBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            _capturing = true;
            CaptureBox.Text = "Press modifiers + a key...";
            ErrorText.Visibility = Visibility.Collapsed;
            CaptureBox.Focus();
            e.Handled = true;
        }

        private void CaptureBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (!_capturing) return;

            var mods = new System.Collections.Generic.List<string>();
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) mods.Add("Control");
            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) mods.Add("Shift");
            if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0) mods.Add("Alt");
            if ((Keyboard.Modifiers & ModifierKeys.Windows) != 0) mods.Add("Win");

            // Ignore if only modifiers pressed
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl || e.Key == Key.LeftAlt || e.Key == Key.RightAlt || e.Key == Key.LeftShift || e.Key == Key.RightShift || e.Key == Key.LWin || e.Key == Key.RWin)
            {
                CaptureBox.Text = string.Join("+", mods) + "+...";
                e.Handled = true;
                return;
            }

            if (mods.Count == 0)
            {
                // require at least one modifier
                ErrorText.Text = "Use at least one modifier (Ctrl, Alt, Shift, or Win) + another key.";
                ErrorText.Visibility = Visibility.Visible;
                e.Handled = true;
                return;
            }

            var keyName = e.Key.ToString();
            Modifiers = string.Join(",", mods);
            KeyName = keyName;
            CaptureBox.Text = Format(Modifiers.Replace(",", "+"), KeyName);
            _capturing = false;
            e.Handled = true;
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            Modifiers = "Control,Shift";
            KeyName = "P";
            CaptureBox.Text = Format(Modifiers.Replace(",", "+"), KeyName);
            ErrorText.Visibility = Visibility.Collapsed;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
