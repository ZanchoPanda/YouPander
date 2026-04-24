using YouPander.Views;

namespace YouPander
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            //Routing.RegisterRoute(nameof(MainPage), typeof(MainPage));
            //Routing.RegisterRoute(nameof(HistoryPage), typeof(HistoryPage));
            //Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
            Routing.RegisterRoute(nameof(BrowserPage), typeof(BrowserPage));
        }
    }
}
