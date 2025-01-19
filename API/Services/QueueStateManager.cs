using Persistence.Interfaces;
using Persistence.Models;

namespace API.Services;

public class QueueStateManager(IQueueStateStore queueStateStore, ISettingStore settingStore, ILogger<QueueStateManager> logger)
{
    private readonly IQueueStateStore _queueStateStore = queueStateStore;
    private readonly ISettingStore _settingStore = settingStore;
    private readonly ILogger<QueueStateManager> _logger = logger;

    public async Task<IEnumerable<string>> GetAvailableBranches(string userId)
    {
        var queue = await _queueStateStore.Find() ?? new();
        var setting = await _settingStore.Find() ?? new();

        if (!setting.Branches.Any() || (!queue.ReviewInCreation?.UserId.Equals(userId) ?? false))
            return [];

        if (queue.ReviewQueue.Count == 0)
            return setting.Branches;

        var reviews = queue.Peek();
        return setting.Branches
                      .Where(b => !reviews.ContainsKey(b) && (queue.ReviewInCreation?.UserId.Equals(userId) ?? true));
    }

    public async Task<PullRequestReview> StartCreation(string userId)
    {
        var queue = await _queueStateStore.Find() ?? new();
        if (queue.ReviewInCreation is null)
        {
            _logger.LogInformation("User Id {UserId} is starting the creation", userId);

            queue.ReviewInCreation = new() { UserId = userId };
            await _queueStateStore.Save(queue);
        }
        else if (queue.ReviewInCreation.UserId.Equals(userId))
            _logger.LogInformation("Restoring creation for {UserId}", userId);
        else
            throw new InvalidOperationException("You can start creation only if the queue is empty");

        return queue.ReviewInCreation;
    }

    public async Task UpdateCreation(string userId, string messageTimestamp)
    {
        var queue = await _queueStateStore.Find() ?? new();

        if (!(queue.ReviewInCreation?.UserId.Equals(userId) ?? false))
            throw new InvalidOperationException("There is no review in creation for this user");

        queue.ReviewInCreation.MessageTimestamp = messageTimestamp;
        await _queueStateStore.Save(queue);

        _logger.LogInformation("User Id {UserId} has updated the creation with {MessageTimestamp}", userId, messageTimestamp);
    }

    public async Task CancelCreation(string userId, bool adminOverride = false)
    {
        var queue = await _queueStateStore.Find() ?? new();

        if (!(queue.ReviewInCreation?.UserId.Equals(userId) ?? false) && !adminOverride)
            throw new InvalidOperationException("There is no review in creation for this user");

        queue.ReviewInCreation = null;
        await _queueStateStore.Save(queue);

        _logger.LogInformation("Creation is canceled");
    }

    public async Task<PullRequestReview?> GetReviewInCreation()
    {
        var queue = await _queueStateStore.Find() ?? new();
        return queue.ReviewInCreation;
    }

    public async Task FinishCreation(string userId, string messageTimestamp, IEnumerable<string> branches)
    {
        var queue = await _queueStateStore.Find() ?? new();

        if (!(queue.ReviewInCreation?.UserId.Equals(userId) ?? false))
            throw new InvalidOperationException("Can't finish creation, there is no review in creation for this user");

        var createdReview = queue.ReviewInCreation with { MessageTimestamp = messageTimestamp };
        foreach (var branch in branches)
            queue.Enqueue(branch, createdReview);

        queue.ReviewInCreation = null;
        await _queueStateStore.Save(queue);

        _logger.LogInformation("Creation is finished");
    }

    public async Task FinishReview(string messageTimestamp, IEnumerable<string> branches)
    {
        var queue = await _queueStateStore.Find() ?? new();

        var reviewsToFinish = queue.Peek().Where(kvp => kvp.Value.MessageTimestamp!.Equals(messageTimestamp)).ToDictionary();
        if (reviewsToFinish is null || reviewsToFinish.Count == 0)
            throw new InvalidOperationException("Can't finish reviews, invalid message stamp");

        if (reviewsToFinish.Count != branches.Count() || reviewsToFinish.Keys.Except(branches).Any())
            throw new InvalidOperationException("Can't finish reviews, invalid branches");

        foreach (var branch in branches)
            if (queue.Peek(branch).MessageTimestamp!.Equals(messageTimestamp))
                queue.Dequeue(branch);

        await _queueStateStore.Save(queue);
    }

    public async Task<bool> IsCreationAllowed()
    {
        var queue = await _queueStateStore.Find() ?? new();
        return queue.CreationIsAllowed;
    }

    public async Task UpdateCreationAllowance(bool newAllowance)
    {
        var queue = await _queueStateStore.Find() ?? new();
        queue.CreationIsAllowed = newAllowance;
        await _queueStateStore.Save(queue);
    }
}
