using System;
using System.Windows;

namespace ProtonVpnGenerator
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            AppDomain.CurrentDomain.UnhandledException += (s, ev) =>
            {
                MessageBox.Show(
                    $"Необработанная ошибка:\n{ev.ExceptionObject}",
                    "Ошибка ProtonVPN Генератор",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            };

            DispatcherUnhandledException += (s, ev) =>
            {
                MessageBox.Show(
                    $"Ошибка приложения:\n{ev.Exception.Message}\n\n{ev.Exception.StackTrace}",
                    "Ошибка ProtonVPN Генератор",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                ev.Handled = true;
            };
        }
    }
}
