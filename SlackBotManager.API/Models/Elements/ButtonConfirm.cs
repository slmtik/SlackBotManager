using System.Text.Json.Serialization;

namespace SlackBotManager.API.Models.Elements
{
    public class ButtonConfirm
    {
        public PlainTextObject Title { get; set; }
        public PlainTextObject Text { get; set; }
        public PlainTextObject Confirm { get; set; }
        public PlainTextObject Deny { get; set; }
        public string? Style { get; set; }

        [JsonConstructor]
        private ButtonConfirm() : this(string.Empty, string.Empty, string.Empty, string.Empty)
        {
            
        }

        public ButtonConfirm(PlainTextObject title, PlainTextObject text, PlainTextObject confirm, PlainTextObject deny)
        {
            Title = title;
            Text = text;
            Confirm = confirm;
            Deny = deny;
        }

        public ButtonConfirm(string title, string text, string confirm, string deny) : this(new PlainTextObject(title),
                                                                                            new PlainTextObject(text),
                                                                                            new PlainTextObject(confirm),
                                                                                            new PlainTextObject(deny))
        {
            
        }
    }
}