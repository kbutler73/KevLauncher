using Microsoft.Win32;
using System.Windows;

namespace KevLauncher;

public partial class EditItemWindow : Window
{
    public string ItemName { get; private set; } = string.Empty;
    public string ItemPath { get; private set; } = string.Empty;
    public string ItemParameters { get; private set; } = string.Empty;

    public EditItemWindow(string name, string path, string parameters)
    {
        InitializeComponent();

        NameBox.Text = name;
        PathBox.Text = path;
        ParamsBox.Text = parameters;
    }

    private void OnBrowsePath(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            CheckFileExists = true,
            Filter = "Executable and scripts|*.exe;*.bat;*.cmd;*.ps1;*.msi;*.lnk;*.url|All files|*.*"
        };

        if (dlg.ShowDialog(this) == true)
        {
            PathBox.Text = dlg.FileName;
            if (string.IsNullOrWhiteSpace(NameBox.Text))
            {
                NameBox.Text = System.IO.Path.GetFileNameWithoutExtension(dlg.FileName);
            }
        }
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        ItemName = NameBox.Text.Trim();
        ItemPath = PathBox.Text.Trim();
        ItemParameters = ParamsBox.Text.Trim();
        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
