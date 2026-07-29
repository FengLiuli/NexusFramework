using System;

namespace NexusFramework.DataCarrier
{
    /// <summary>
    ///     仅在所属 CarrierManager 生命周期内有效的 Tag 槽位。
    /// </summary>
    [Serializable]
    public readonly struct RuntimeTagSlot : IEquatable<RuntimeTagSlot>
    {
        internal RuntimeTagSlot(int value)
        {
            Value = value;
        }

        internal int Value { get; }

        public bool IsValid => Value > 0;

        public bool Equals(RuntimeTagSlot other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is RuntimeTagSlot other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public static bool operator ==(RuntimeTagSlot left, RuntimeTagSlot right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RuntimeTagSlot left, RuntimeTagSlot right)
        {
            return !left.Equals(right);
        }
    }
}
