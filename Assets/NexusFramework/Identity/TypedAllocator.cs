using System;
using System.Collections.Generic;

namespace NexusFramework.Identity
{
    /// <summary>
    ///     发号并记录 typeId（discriminator）的分配器。
    ///     服务多类型运行时对象：Carrier 实例、GAS ASC、效果实例等。
    ///     <para>
    ///     typeId 由调用方提供，通常来自某个 <see cref="KeyIndex" />；分配器不负责分配类型。
    ///     类型数量只受 <see cref="int" /> 限制，不把类型编码进 id。
    ///     </para>
    ///     <para>非线程安全；需由使用方在单线程或外部同步下调用。</para>
    /// </summary>
    public sealed class TypedAllocator
    {
        private readonly string mName;
        private readonly IdAllocator mAllocator;
        private readonly List<int> mTypeById; // 下标 = id；0 表示无类型

        public TypedAllocator(string name, int initialCapacity = 0)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("TypedAllocator name cannot be null or empty.", nameof(name));

            mName = name;
            mAllocator = new IdAllocator(initialCapacity);
            mTypeById = new List<int> { 0 };
        }

        public string Name => mName;
        public int Count => mAllocator.Count;
        public int Capacity => mAllocator.Capacity;

        /// <summary>分配一个带类型的实例 id。typeId 非法或容量耗尽返回 0。</summary>
        public int Allocate(int typeId)
        {
            if (typeId <= 0)
                return 0;

            var id = mAllocator.Allocate();
            if (id == 0)
                return 0;

            EnsureTypeCapacity(id);
            mTypeById[id] = typeId;
            return id;
        }

        public bool TryAllocate(int typeId, out int id)
        {
            id = Allocate(typeId);
            return id != 0;
        }

        /// <summary>归还实例 id 及其类型记录。</summary>
        public bool Free(int id)
        {
            if (id <= 0 || id >= mTypeById.Count || mTypeById[id] == 0)
                return false;

            mTypeById[id] = 0;
            return mAllocator.Free(id);
        }

        public bool IsAlive(int id)
        {
            return mAllocator.IsAlive(id)
                   && id > 0
                   && id < mTypeById.Count
                   && mTypeById[id] != 0;
        }

        /// <summary>id → typeId，O(1)。</summary>
        public bool TryGetType(int id, out int typeId)
        {
            typeId = 0;
            if (id <= 0 || id >= mTypeById.Count)
                return false;

            typeId = mTypeById[id];
            return typeId != 0;
        }

        public bool IsType(int id, int typeId)
        {
            return typeId > 0 && TryGetType(id, out var actual) && actual == typeId;
        }

        public void Clear()
        {
            mAllocator.Clear();
            mTypeById.Clear();
            mTypeById.Add(0);
        }

        private void EnsureTypeCapacity(int id)
        {
            while (mTypeById.Count <= id)
                mTypeById.Add(0);
        }
    }
}
