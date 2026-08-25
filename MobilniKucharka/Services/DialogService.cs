namespace MobilniKucharka.Services
{
    public interface IDialogService
    {
        Task ShowAlertAsync(string title, string message, string cancel = "OK");
        Task<bool> ShowConfirmAsync(string title, string message, string accept, string cancel);
        Task<string?> ShowActionSheetAsync(string title, string cancel, string? destruction, params string[] buttons);
        Task<string?> ShowPromptAsync(string title, string message, string accept = "OK", string cancel = "Zrušit", string? placeholder = null, int maxLength = -1, Keyboard? keyboard = null, string initialValue = "");
    }

    public class DialogService : IDialogService
    {
        private static Page? GetCurrentPage()
        {
            var window = Application.Current?.Windows.FirstOrDefault();
            if (window?.Page is NavigationPage navPage)
                return navPage.CurrentPage;
            return window?.Page;
        }

        public async Task ShowAlertAsync(string title, string message, string cancel = "OK")
        {
            var page = GetCurrentPage();
            if (page != null)
            {
                await page.DisplayAlertAsync(title, message, cancel);
            }
        }

        public async Task<bool> ShowConfirmAsync(string title, string message, string accept, string cancel)
        {
            var page = GetCurrentPage();
            if (page != null)
            {
                return await page.DisplayAlertAsync(title, message, accept, cancel);
            }
            return false;
        }

        public async Task<string?> ShowActionSheetAsync(string title, string cancel, string? destruction, params string[] buttons)
        {
            var page = GetCurrentPage();
            if (page != null)
            {
                return await page.DisplayActionSheetAsync(title, cancel, destruction, buttons);
            }
            return null;
        }

        public async Task<string?> ShowPromptAsync(string title, string message, string accept = "OK", string cancel = "Zrušit", string? placeholder = null, int maxLength = -1, Keyboard? keyboard = null, string initialValue = "")
        {
            var page = GetCurrentPage();
            if (page != null)
            {
                return await page.DisplayPromptAsync(title, message, accept, cancel, placeholder, maxLength, keyboard, initialValue);
            }
            return null;
        }
    }
}
