namespace NexusFramework.GAS.ECS
{
    /// <summary>
    /// 是否刷新Effect的持续时间
    /// </summary>
    public enum DurationRefreshPolicy
    {
        /// <summary>不刷新Effect的持续时间</summary>
        NeverRefresh,

        /// <summary>每次apply成功后刷新Effect的持续时间, denyOverflowApplication如果为True则多余的Apply不会刷新Duration</summary>
        RefreshOnSuccessfulApplication
    }
}