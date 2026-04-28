namespace Aethra;

public sealed class InputBindingSetting
{
    public InputBindingSetting(string category, string gesture, string command, string description, string source)
    {
        Category = category;
        Gesture = gesture;
        Command = command;
        Description = description;
        Source = source;
    }

    public string Category { get; set; }

    public string Gesture { get; set; }

    public string Command { get; set; }

    public string Description { get; set; }

    public string Source { get; set; }
}
