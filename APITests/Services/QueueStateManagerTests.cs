using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using SlackBotManager.Persistence;
using SlackBotManager.Persistence.Models;

namespace SlackBotManager.API.Services.Tests
{
    public class QueueStateManagerTests
    {
        private readonly Mock<ILogger<QueueStateManager>> _logger;
        private readonly Mock<IQueueStateStore> _queueStateStore;
        private readonly Mock<ISettingStore> _settingStore;

        private readonly QueueStateManager _queueStateManager;
        private readonly QueueState _queueState;

        private readonly string _requestingUserID = "userId";
        private readonly string _otherUserID = "otherUserId";
        private readonly IEnumerable<string> _branches = ["develop", "release"];
        
        public QueueStateManagerTests()
        {
            _logger = new Mock<ILogger<QueueStateManager>>();
            _queueStateStore = new Mock<IQueueStateStore>();
            _settingStore = new Mock<ISettingStore>();
            _queueStateManager = new(_queueStateStore.Object, _settingStore.Object, _logger.Object);
            _queueState = new();

            SetSettingToReturn(new Setting() { Branches = _branches });
        }

        private void SetQueueStateToReturn(QueueState queueState)
        {
            _queueStateStore.Setup(x => x.Find()).Returns(Task.FromResult<QueueState?>(queueState));
        }

        private void SetSettingToReturn(Setting setting)
        {
            _settingStore.Setup(x => x.Find()).Returns(Task.FromResult<Setting?>(setting));
        }

        [Fact]
        public async Task GetAvailableBranches_EmptyQueue()
        {
            var expected = _branches;
            var actual = await _queueStateManager.GetAvailableBranches(_requestingUserID);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task GetAvailableBranches_BranchInCreationRestore()
        {
            _queueState.ReviewInCreation = new() { UserId = _requestingUserID, MessageTimestamp = "messageTimestamp" };
            SetQueueStateToReturn(_queueState);

            var expected = _branches;   
            var actual = await _queueStateManager.GetAvailableBranches(_requestingUserID);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task GetAvailableBranches_RequestingUserHasCreated()
        {
            _queueState.Enqueue("develop", new() { UserId = _requestingUserID });
            SetQueueStateToReturn(_queueState);

            var expected = _branches.Except(["develop"]);
            var actual = await _queueStateManager.GetAvailableBranches(_requestingUserID);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task CanStartCreation_OtherUserIsCreating()
        {
            _queueState.ReviewInCreation = new() { UserId = _otherUserID };
            SetQueueStateToReturn(_queueState);

            var actual = await _queueStateManager.GetAvailableBranches(_requestingUserID);

            Assert.Empty(actual);
        }

        [Fact]
        public async Task CanStartCreation_OtherUserHasCreated()
        {
            _queueState.Enqueue("develop", new() { UserId = _otherUserID });
            SetQueueStateToReturn(_queueState);

            var expected = _branches.Except(["develop"]);
            var actual = await _queueStateManager.GetAvailableBranches(_requestingUserID);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task StartCreation_EmptyQueue()
        {
            var expected = new PullRequestReview() { UserId = _requestingUserID };
            var actual = await _queueStateManager.StartCreation(_requestingUserID);

            Assert.Equal(expected, actual);

            _queueStateStore.Verify(x => x.Save(It.IsAny<QueueState>()));
        }

        [Fact]
        public async Task StartCreation_RequestingUserIsCreating()
        {
            _queueState.ReviewInCreation = new() { UserId = _requestingUserID, MessageTimestamp = "messageTimestamp" };
            SetQueueStateToReturn(_queueState);
            
            var expected = _queueState.ReviewInCreation;
            var actual = await _queueStateManager.StartCreation(_requestingUserID);

            Assert.Equal(expected, actual);

            _queueStateStore.Verify(x => x.Save(It.IsAny<QueueState>()), Times.Never);
        }

        [Fact]
        public async Task StartCreation_OtherUserIsCreating()
        {
            _queueState.ReviewInCreation = new() { UserId = _otherUserID };
            SetQueueStateToReturn(_queueState);

            await Assert.ThrowsAsync<InvalidOperationException>(() => _queueStateManager.StartCreation(_requestingUserID));
        }

        [Fact]
        public async Task SetCreationMessageTimestamp_RequestingUserIsCreating()
        {
            _queueState.ReviewInCreation = new() { UserId = _requestingUserID, MessageTimestamp = "messageTimestamp" };
            SetQueueStateToReturn(_queueState);
            var messageTimestamp = "timestamp";

            await _queueStateManager.UpdateCreation(_requestingUserID, messageTimestamp);

            var expected = messageTimestamp;
            var actual = _queueState.ReviewInCreation.MessageTimestamp;

            Assert.Equal(expected, actual);
            _queueStateStore.Verify(x => x.Save(It.IsAny<QueueState>()), Times.Once);
        }

        [Fact]
        public async Task SetCreationMessageTimestamp_EmptyQueue()
        {
            SetQueueStateToReturn(_queueState);
            var messageTimestamp = "timestamp";

            await Assert.ThrowsAsync<InvalidOperationException>(() => _queueStateManager.UpdateCreation(_requestingUserID, messageTimestamp));
        }

        [Fact]
        public async Task SetCreationMessageTimestamp_OtherUserIsCreating()
        {
            _queueState.ReviewInCreation = new() { UserId = _otherUserID };
            SetQueueStateToReturn(_queueState);
            var messageTimestamp = "timestamp";

            await Assert.ThrowsAsync<InvalidOperationException>(() => _queueStateManager.UpdateCreation(_requestingUserID, messageTimestamp));
        }

        [Fact]
        public async Task CancelCreation_EmptyQueue()
        {
            SetQueueStateToReturn(_queueState);

            await Assert.ThrowsAsync<InvalidOperationException>(() => _queueStateManager.CancelCreation(_requestingUserID));
        }

        [Fact]
        public async Task CancelCreation_RequestingUser()
        {
            _queueState.ReviewInCreation = new() { UserId = _requestingUserID };
            SetQueueStateToReturn(_queueState);

            await _queueStateManager.CancelCreation(_requestingUserID);

            var expected = 0;
            var actual = _queueState.ReviewQueue.Count;

            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task CancelCreation_OtherUser()
        {
            _queueState.ReviewInCreation = new() { UserId = _otherUserID };
            SetQueueStateToReturn(_queueState);

            await Assert.ThrowsAsync<InvalidOperationException>(() => _queueStateManager.CancelCreation(_requestingUserID));
        }

        [Fact]
        public async Task CancelCreation_OtherUserAdminOverride()
        {
            _queueState.ReviewInCreation = new() { UserId = _otherUserID };
            SetQueueStateToReturn(_queueState);

            await _queueStateManager.CancelCreation(_requestingUserID, true);

            var expected = 0;
            var actual = _queueState.ReviewQueue.Count;

            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task FinishCreation_EmptyQueue()
        {
            var messageTimestamp = "timestamp";
            SetQueueStateToReturn(_queueState);

            await Assert.ThrowsAsync<InvalidOperationException>(() => _queueStateManager.FinishCreation(_requestingUserID, messageTimestamp, _branches));
        }

        [Fact]
        public async Task FinishCreation_RequestingUser()
        {
            var creationMessageTimestamp = "timestamp";
            var finishMessageTimestamp = "timestamp2";
            var creationReview = new PullRequestReview()
            {
                UserId = _requestingUserID,
                MessageTimestamp = creationMessageTimestamp
            };

            _queueState.ReviewInCreation = creationReview;
            SetQueueStateToReturn(_queueState);

            await _queueStateManager.FinishCreation(_requestingUserID, finishMessageTimestamp, ["develop"]);

            var expected = creationReview with { MessageTimestamp = finishMessageTimestamp };
            var actual = _queueState.Peek("develop");

            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task FinishCreation_OtherUser()
        {
            var creationMessageTimestamp = "timestamp";
            var finishMessageTimestamp = "timestamp2";

            _queueState.ReviewInCreation = new() { UserId = _otherUserID, MessageTimestamp = creationMessageTimestamp };
            SetQueueStateToReturn(_queueState);

            await Assert.ThrowsAsync<InvalidOperationException>(() => _queueStateManager.FinishCreation(_requestingUserID, finishMessageTimestamp, ["develop"]));
        }

        [Fact]
        public async Task FinishReview_IncorrectMessageTimestamp()
        {
            var messageTimestamp = "timestamp";
            SetQueueStateToReturn(_queueState);

            await Assert.ThrowsAsync<InvalidOperationException>(() => _queueStateManager.FinishReview(messageTimestamp, []));
        }

        [Fact]
        public async Task FinishReview_RequestingUser()
        {
            var messageTimestamp = "timestamp";
            var review = new PullRequestReview()
            {
                UserId = _requestingUserID,
                MessageTimestamp = messageTimestamp
            };

            _queueState.Enqueue("develop", review);
            SetQueueStateToReturn(_queueState);

            await _queueStateManager.FinishReview(messageTimestamp, ["develop"]);

            var expected = 0;
            var actual = _queueState.ReviewQueue["develop"].Count;

            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task FinishReview_IncorrectBranches()
        {
            var messageTimestamp = "timestamp";
            var review = new PullRequestReview()
            {
                UserId = _requestingUserID,
                MessageTimestamp = messageTimestamp
            };

            _queueState.Enqueue("develop", review);
            _queueState.Enqueue("rel", review);
            SetQueueStateToReturn(_queueState);

            await Assert.ThrowsAsync<InvalidOperationException>(() => _queueStateManager.FinishReview(messageTimestamp, ["release"]));
        }
    }
}