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
        private bool _capturing = false;

        public HotkeySettingsWindow(string currentModifiers, string currentKey)
        {
            InitializeComponent();
            Modifiers = currentModifiers;
            KeyName = currentKey;
            CurrentHotkeyText.Text = $"Current: {Format(currentModifiers, currentKey)}";
            CaptureBox.Text = "Click here, then press a key combo…";
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

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
