using System;

namespace NexusFramework.DataCarrier
{
    /// <summary>
    ///     载体ID - 64位紧凑存储设计
    ///     位域划分：
    ///     - 高8位：框架ID (256个框架)
    ///     - 中16位：类型ID (65536种类型)
    ///     - 低40位：唯一ID (1万亿个实例)
    /// </summary>
    [Serializable]
    public readonly struct CarrierId : IEquatable<CarrierId>
    {
        #region 构造函数

        public CarrierId(byte frameworkId, ushort typeId, ulong uniqueId)
        {
            // if (frameworkId > 0xFF)
            //     throw new ArgumentException("FrameworkId exceeds 8-bit limit");

            // if (typeId > 0xFFFF)
            //     throw new ArgumentException("TypeId exceeds 16-bit limit");

            if (uniqueId > 0x00000FFFFFFFFFF)
                throw new ArgumentException("UniqueId exceeds 40-bit limit");

            RawValue = ((ulong)frameworkId << 56) | ((ulong)typeId << 40) | (uniqueId & 0x00000FFFFFFFFFF);
        }

        private CarrierId(ulong rawValue)
        {
            RawValue = rawValue;
        }

        #endregion

        #region 属性

        /// <summary>
        ///     框架ID (高8位)
        /// </summary>
        public byte FrameworkId => (byte)((RawValue >> 56) & 0xFF);

        /// <summary>
        ///     类型ID (9-24位)
        /// </summary>
        public ushort TypeId => (ushort)((RawValue >> 40) & 0xFFFF);

        /// <summary>
        ///     唯一ID (低40位)
        /// </summary>
        public ulong UniqueId => RawValue & 0x00000FFFFFFFFFF;

        /// <summary>
        ///     原始值
        /// </summary>
        public ulong RawValue { get; }

        /// <summary>
        ///     是否有效
        /// </summary>
        public bool IsValid => RawValue != 0;

        #endregion

        #region 静态实例

        /// <summary>
        ///     无效ID
        /// </summary>
        public static CarrierId Invalid => new(0);

        #endregion

        #region 相等性比较

        public bool Equals(CarrierId other)
        {
            return RawValue == other.RawValue;
        }

        public override bool Equals(object obj)
        {
            return obj is CarrierId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return RawValue.GetHashCode();
        }

        public static bool operator ==(CarrierId left, CarrierId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CarrierId left, CarrierId right)
        {
            return !left.Equals(right);
        }

        #endregion

        #region 字符串表示

        public override string ToString()
        {
            if (!IsValid) return "CarrierId.Invalid";
            return $"CarrierId(F:{FrameworkId},T:{TypeId},ID:{UniqueId})";
        }

        #endregion

        #region 类型转换

        public static implicit operator ulong(CarrierId carrierId)
        {
            return carrierId.RawValue;
        }

        public static explicit operator CarrierId(ulong value)
        {
            return new CarrierId(value);
        }

        #endregion
    }
}