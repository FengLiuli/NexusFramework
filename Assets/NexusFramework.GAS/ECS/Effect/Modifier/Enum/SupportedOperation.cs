using System;

namespace NexusFramework.GAS.ECS
{
    [Flags]
    public enum SupportedOperation : byte
    {
        None = 0,
        Add = 1 << GEOperation.Add,
        Minus = 1 << GEOperation.Minus,
        Multiply = 1 << GEOperation.Multiply,
        Divide = 1 << GEOperation.Divide,
        Override = 1 << GEOperation.Override,
        All = Add | Minus | Multiply | Divide | Override
    }
}