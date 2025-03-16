using Core.ApiClient;

namespace Jira.DTO;

public class TokenResponse : BaseResponse
{
    public required string AccessToken { get; set; }
    public required int ExpiresIn { get; set; }
}
