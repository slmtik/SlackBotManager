using System.Collections.ObjectModel;

namespace SlackBotManager.Persistence.Models;

public record QueueState : StoreItemBase
{
    public Dictionary<string, Collection<PullRequestReview>> ReviewQueue { get; init; } = [];
    public PullRequestReview? ReviewInCreation { get; set; }

    public Dictionary<string, PullRequestReview> Peek() => ReviewQueue.Where(kvp => kvp.Value.Count > 0)
                                                                      .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.First());

    public PullRequestReview Peek(string branch) => ReviewQueue[branch].First();
    
    public void Enqueue(string branch, PullRequestReview review)
    {
        if (!ReviewQueue.ContainsKey(branch))
            ReviewQueue[branch] = [];
        ReviewQueue[branch].Add(review);
    }

    public void Dequeue(string branch)
    {
        if (ReviewQueue[branch].Count > 0)
            ReviewQueue[branch].RemoveAt(0);
    }
}
