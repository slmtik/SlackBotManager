namespace Persistence.Models;

public class ReminderSetting
{
    public bool Enabled { get; set; } = false;
    public int TimeToRemindInMinutes { get; set; } = 60;
    public string? RemindingChannelId { get; set; }
    public string? WorkDayStart { get; set; } = "10:00";
    public string? WorkDayEnd { get; set; } = "18:30";
    public string? MessageTemplate { get; set; } = "<!here>";
}
