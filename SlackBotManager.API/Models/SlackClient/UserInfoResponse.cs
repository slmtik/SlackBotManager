namespace SlackBotManager.API.Models.SlackClient;

public class UserInfoResponse : BaseResponse
{
    public User User { get; set; }
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
