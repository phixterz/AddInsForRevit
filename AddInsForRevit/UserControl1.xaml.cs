using RevitAddin;
using System;
using System.Windows;

namespace AddInsForRevit
{
    public partial class FinishingSettingsWindow : Window
    {
        private static FinishingSettingsWindow _instance;
        public static FinishingSettingsWindow Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new FinishingSettingsWindow();
                }
                return _instance;
            }
        }

        public Action RecalculateAction { get; set; }
        public double DividerValue { get; private set; }

        private FinishingSettingsWindow()
        {
            InitializeComponent();
            DividerTextBox.Text = FinishingCommand.Divider.ToString();
        }

        private void RecalculateButton_Click(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(DividerTextBox.Text, out double value) && value > 0)
            {
                DividerValue = value;
                FinishingCommand.Divider = value;

                // Вызов пересчёта через ExternalEvent
                RecalculateAction?.Invoke();
            }
            else
            {
                MessageBox.Show("Пожалуйста, введите корректное число.", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Hide(); // Скрываем окно, вместо закрытия
        }
    }
}
