namespace MobilniKucharka.Classes.Navigation
{
    public partial class BlazorShellPage : ContentPage
    {
        public static event Action? ShellAppeared;

        public BlazorShellPage()
        {
            InitializeComponent();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            ShellAppeared?.Invoke();
        }
    }
}