namespace YouPander.Views
{
    public partial class MainPage : ContentPage
    {
        private bool _initialized;

        public MainPage(MainViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            if (_initialized)
            {
                return;
            }

            if (BindingContext is MainViewModel vm)
            {
                _initialized = true;

                try
                {
                    await vm.InitializeAsync();
                }
                catch (Exception ex)
                {
                    _initialized = false;
                    System.Diagnostics.Debug.WriteLine($"Error inicializando YouPander: {ex}");
                }
            }
        }
    }
}
