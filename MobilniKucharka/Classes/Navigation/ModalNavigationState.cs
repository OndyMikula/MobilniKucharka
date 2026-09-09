namespace MobilniKucharka.Classes.Navigation
{
    // Předá cílovou "route" nové, jednorázové instanci BlazorWebView (BlazorModalPage) - stejný
    // jednorázový vzor jako App.PendingImportGuid. ModalEntry.razor si tuhle hodnotu přečte jednou
    // v OnInitialized a rovnou podle ní vykreslí správnou stránku přes DynamicComponent - žádný
    // Router, žádný NavigationManager.NavigateTo() se v týhle WebView vůbec nepoužije.
    public static class ModalNavigationState
    {
        public static string? PendingRoute { get; set; }
    }
}