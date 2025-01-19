using System.Text.Json.Serialization;

namespace Slack.Models.Elements;

public class ButtonConfirm(PlainText title, PlainText text, PlainText confirm, PlainText deny)
{
    public PlainText Title { get; set; } = title;
    public PlainText Text { get; set; } = text;
    public PlainText Confirm { get; set; } = confirm;
    public PlainText Deny { get; set; } = deny;
    public string? Style { get; set; }

    [JsonConstructor]
    private ButtonConfirm() : this(string.Empty, string.Empty, string.Empty, string.Empty)
    {
        
    }

    public ButtonConfirm(string title, string text, string confirm, string deny) : 
        this(new PlainText(title), new PlainText(text), new PlainText(confirm), new PlainText(deny))
    {
        
    }
}