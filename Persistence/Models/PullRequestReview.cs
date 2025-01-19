namespace Persistence.Models;

public record PullRequestReview
{
    public required string UserId { get; set; }
    public string? MessageTimestamp { get; set; }
}
