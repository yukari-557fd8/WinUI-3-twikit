namespace App2
{
    public sealed class AppViewModels
    {
        public TimelineViewModel Timeline { get; } = new();
        public SearchViewModel Search { get; } = new();
        public NotificationsViewModel Notifications { get; } = new();
        public ListsViewModel Lists { get; } = new();
    }
}
