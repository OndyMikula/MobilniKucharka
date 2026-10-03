using Microsoft.AspNetCore.Components;

namespace MobilniKucharka.Components.Shared
{
    public class ActionMenuGroup
    {
        public List<ActionMenuItem> Items { get; set; } = [];
    }

    public class ActionMenuItem
    {
        public string Label { get; set; } = string.Empty;
        public bool IsDestructive { get; set; }
        public EventCallback OnClick { get; set; }
    }
}