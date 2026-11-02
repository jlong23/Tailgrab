namespace Tailgrab.PlayerManagement
{
    public class AlertSoundOption
    {
        public string Name { get; set; }
        public string Value { get; set; }

        public AlertSoundOption(string name, string value)
        {
            Name = name;
            Value = value;
        }

        public override string ToString() => Name;
    }
}
