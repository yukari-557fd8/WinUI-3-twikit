// App.xaml.cs
using Microsoft.UI.Xaml;

namespace App2
{
    public partial class App : Application
    {
        public static MainWindow? MainWindow { get; private set; }   // MainWindow型に変更
        public static AppViewModels ViewModels { get; } = new();

        public App()
        {
            InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            var window = new MainWindow();
            MainWindow = window;
            window.Activate();
        }
    }
}
