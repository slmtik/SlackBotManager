namespace SlackBotManager.API.Models.SlackClient;

public class UserInfoResponse : BaseResponse
{
    public User? User { get; set; }
}

public class User
{
    public Profile? Profile { get; set; }
    public bool IsAdmin { get; set; }
}

public class Profile
{
    public string? DisplayName { get; set; }
    public string? Image_24 { get; set; }
}
