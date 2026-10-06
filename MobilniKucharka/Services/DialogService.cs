namespace MobilniKucharka.Services
{
    public enum DialogKind { Alert, Confirm, Prompt, Amount, ActionSheet }

    public class DialogRequest
    {
        public DialogKind Kind { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public string Accept { get; init; } = "OK";
        public string Cancel { get; init; } = "Zrušit";
        public string? Placeholder { get; init; }
        public int MaxLength { get; init; } = -1;
        public bool Numeric { get; init; }
        public string InitialValue { get; init; } = string.Empty;
        public IReadOnlyList<string> Units { get; init; } = [];
        public string InitialUnit { get; init; } = string.Empty;
        public string? SecondPlaceholder { get; init; }
        public string SecondInitialValue { get; init; } = string.Empty;
        public IReadOnlyList<string> Buttons { get; init; } = [];
        public string? Destruction { get; init; }
        public string? Suffix { get; init; }
        public string? SecondSuffix { get; init; }
    }

    public class DialogResponse
    {
        public bool Accepted { get; init; }
        public string Text { get; init; } = string.Empty;
        public string Unit { get; init; } = string.Empty;
        public string SecondText { get; init; } = string.Empty;
        public string Choice { get; init; } = string.Empty;
    }

    // Implementuje DialogHost.razor
    public interface IDialogHost
    {
        Task<DialogResponse> ShowAsync(DialogRequest request);
    }

    public interface IDialogService
    {
        Task ShowAlertAsync(string title, string message, string cancel = "OK");
        Task<bool> ShowConfirmAsync(string title, string message, string accept, string cancel);
        Task<string?> ShowActionSheetAsync(string title, string cancel, string? destruction, params string[] buttons);
        Task<string?> ShowPromptAsync(string title, string message, string accept = "OK", string cancel = "Zrušit", string? placeholder = null, int maxLength = -1, Keyboard? keyboard = null, string initialValue = "", string? suffix = null);
        Task<(string Text, string Unit, string SecondText)?> ShowAmountPromptAsync(string title, string message, string[] units, string accept = "OK", string cancel = "Zrušit", string? placeholder = null, string initialValue = "", string initialUnit = "", string? secondPlaceholder = null, string secondInitialValue = "", string? secondSuffix = null);
        void RegisterHost(IDialogHost host);
        void UnregisterHost(IDialogHost host);
    }

    public class DialogService : IDialogService
    {
        private readonly List<IDialogHost> _hosts = [];
        private readonly Lock _lock = new();

        public void RegisterHost(IDialogHost host)
        {
            lock (_lock) _hosts.Add(host);
        }

        public void UnregisterHost(IDialogHost host)
        {
            lock (_lock) _hosts.Remove(host);
        }

        // Poslední zaregistrovaný = nejvrchnější WebView
        private IDialogHost? CurrentHost
        {
            get
            {
                lock (_lock) return _hosts.Count > 0 ? _hosts[^1] : null;
            }
        }

        private static Page? GetCurrentPage()
        {
            var windows = Application.Current?.Windows;
            var window = windows is { Count: > 0 } ? windows[0] : null;

            if (window?.Page is NavigationPage navPage)
                return navPage.CurrentPage;
            return window?.Page;
        }

        public async Task ShowAlertAsync(string title, string message, string cancel = "OK")
        {
            var host = CurrentHost;
            if (host != null)
            {
                await host.ShowAsync(new DialogRequest { Kind = DialogKind.Alert, Title = title, Message = message, Accept = cancel });
                return;
            }

            var page = GetCurrentPage();
            if (page != null)
            {
                await page.DisplayAlertAsync(title, message, cancel);
            }
        }

        public async Task<bool> ShowConfirmAsync(string title, string message, string accept, string cancel)
        {
            var host = CurrentHost;
            if (host != null)
            {
                var response = await host.ShowAsync(new DialogRequest { Kind = DialogKind.Confirm, Title = title, Message = message, Accept = accept, Cancel = cancel });
                return response.Accepted;
            }

            var page = GetCurrentPage();
            if (page != null)
            {
                return await page.DisplayAlertAsync(title, message, accept, cancel);
            }
            return false;
        }

        // Vrací text vybraného tlačítka, při zrušení null
        public async Task<string?> ShowActionSheetAsync(string title, string cancel, string? destruction, params string[] buttons)
        {
            var host = CurrentHost;
            if (host != null)
            {
                var response = await host.ShowAsync(new DialogRequest
                {
                    Kind = DialogKind.ActionSheet,
                    Title = title,
                    Cancel = cancel,
                    Destruction = destruction,
                    Buttons = buttons
                });
                return response.Accepted ? response.Choice : null;
            }

            var page = GetCurrentPage();
            if (page != null)
            {
                return await page.DisplayActionSheetAsync(title, cancel, destruction, buttons);
            }
            return null;
        }

        public async Task<string?> ShowPromptAsync(string title, string message, string accept = "OK", string cancel = "Zrušit", string? placeholder = null, int maxLength = -1, Keyboard? keyboard = null, string initialValue = "", string? suffix = null)
        {
            var host = CurrentHost;
            if (host != null)
            {
                var response = await host.ShowAsync(new DialogRequest
                {
                    Kind = DialogKind.Prompt,
                    Title = title,
                    Message = message,
                    Accept = accept,
                    Cancel = cancel,
                    Placeholder = placeholder,
                    MaxLength = maxLength,
                    Numeric = keyboard == Keyboard.Numeric,
                    InitialValue = initialValue,
                    Suffix = suffix
                });
                return response.Accepted ? response.Text : null;
            }

            var page = GetCurrentPage();
            if (page != null)
            {
                return await page.DisplayPromptAsync(title, message, accept, cancel, placeholder, maxLength, keyboard, initialValue);
            }
            return null;
        }

        public async Task<(string Text, string Unit, string SecondText)?> ShowAmountPromptAsync(string title, string message, string[] units, string accept = "OK", string cancel = "Zrušit", string? placeholder = null, string initialValue = "", string initialUnit = "", string? secondPlaceholder = null, string secondInitialValue = "", string? secondSuffix = null)
        {
            var host = CurrentHost;
            if (host == null) return null;

            var response = await host.ShowAsync(new DialogRequest
            {
                Kind = DialogKind.Amount,
                Title = title,
                Message = message,
                Accept = accept,
                Cancel = cancel,
                Placeholder = placeholder,
                Numeric = true,
                InitialValue = initialValue,
                Units = units,
                InitialUnit = initialUnit,
                SecondPlaceholder = secondPlaceholder,
                SecondInitialValue = secondInitialValue,
                SecondSuffix = secondSuffix
            });

            if (!response.Accepted) return null;
            return (response.Text, response.Unit, response.SecondText);
        }
    }
}