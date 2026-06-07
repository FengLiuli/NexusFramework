namespace NexusFramework.GAS.ECS
{
    /// <summary>
    /// 是否重置Effect的周期计时
    /// </summary>
    public enum PeriodResetPolicy
    {
        /// <summary>不重置Effect的周期计时</summary>
        NeverRefresh,

        /// <summary>每次apply成功后重置Effect的周期计时</summary>
        ResetOnSuccessfulApplication
    }
}