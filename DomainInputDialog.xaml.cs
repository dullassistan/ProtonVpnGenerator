using System.Windows;
using System.Windows.Input;

namespace ProtonVpnGenerator
{
    public partial class DomainInputDialog : Window
    {
        public string Domain { get; private set; } = string.Empty;

        public DomainInputDialog(string? defaultDomain = null)
        {
            InitializeComponent();
            if (!string.IsNullOrWhiteSpace(defaultDomain))
            {
                TxtDomain.Text = defaultDomain;
            }
            TxtDomain.Focus();
            TxtDomain.SelectAll();
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            string domain = TxtDomain.Text.Trim();
            if (string.IsNullOrWhiteSpace(domain))
            {
                MessageBox.Show(this, "Пожалуйста, введите имя домена.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Domain = domain;
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void TxtDomain_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BtnConfirm_Click(sender, e);
            }
            else if (e.Key == Key.Escape)
            {
                BtnCancel_Click(sender, e);
            }
        }
    }
}
