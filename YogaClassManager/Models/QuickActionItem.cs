namespace YogaClassManager.Models
{
    public class QuickActionItem
    {
        public QuickActionItem(Command command, string label, string icon)
        {
            Command = command;
            Label = label;
            Icon = icon;
        }

        public Command Command { get; set; }
        public string Label { get; set; }
        public string Icon { get; set; }
    }
}
