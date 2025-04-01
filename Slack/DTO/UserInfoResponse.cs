namespace Slack.DTO;

public class UserInfoResponse : SlackResponse
{
    public required User User { get; set; }
}

public class User
{
    public required Profile Profile { get; set; }
    public bool IsAdmin { get; set; }
}

public class Profile
{
    public required string DisplayName { get; set; }
    public required string Image_24 { get; set; }
}
