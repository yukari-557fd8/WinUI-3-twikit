namespace App2
{
    public readonly struct TimelineScrollAnchor
    {
        public string TweetDedupKey { get; init; }
        public double OffsetWithinItem { get; init; }

        public bool IsEmpty => string.IsNullOrEmpty(TweetDedupKey);

        public static TimelineScrollAnchor Empty => default;
    }
}
