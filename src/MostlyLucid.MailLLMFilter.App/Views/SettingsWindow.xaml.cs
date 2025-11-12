using System.Windows;

namespace MostlyLucid.MailLLMFilter.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.SettingsViewModel viewModel && viewModel.HasUnsavedChanges)
        {
            var result = MessageBox.Show(
                "You have unsaved changes. Are you sure you want to close?",
                "Unsaved Changes",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;
        }

        Close();
    }
}
