using System.Windows;

namespace MostlyLucid.MailLLMFilter.App.Views;

public partial class TemplateTestWindow : Window
{
    public TemplateTestWindow()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
