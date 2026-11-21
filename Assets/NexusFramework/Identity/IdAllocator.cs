using System;

namespace NexusFramework.Identity
{
    /// <summary>
    ///     纯整数发号与回收；不持有、不校验、不解释任何字符串。
    ///     <para>
    ///     槽位连续，范围 [1, <see cref="Capacity" />]；0 恒为无效。
    ///     <see cref="Free" /> 后槽位进入空闲表，后续 <see cref="Allocate" /> 以 LIFO 顺序复用
    ///     （最后释放的最先被复用）。因此旧 id 很可能立刻指向新对象，悬垂引用风险
    ///     高于延迟复用策略；使用方不得在对象销毁后继续持有 id。
    ///     不提供代际检测。
    ///     </para>
    ///     <para>非线程安全；需由使用方在单线程或外部同步下调用。</para>
    /// </summary>
    public sealed class IdAllocator
    {
        private bool[] mAlive; // 下标 = 槽位；0 不使用
        private int[] mFree;
        private int mFreeCount;
        private int mCapacity;
        private int mCount;

        public IdAllocator(int initialCapacity = 0)
        {
            var capacity = Math.Max(0, initialCapacity);
            mAlive = new bool[capacity + 1];
            mFree = new int[Math.Max(4, capacity / 4 + 4)];
        }

        /// <summary>取一个空闲槽位；无空闲则扩展。容量耗尽返回 0。</summary>
        public int Allocate()
        {
            if (mFreeCount > 0)
            {
                var reused = mFree[--mFreeCount];
                mAlive[reused] = true;
                mCount++;
                return reused;
            }

            if (mCapacity == int.MaxValue)
                return 0;

            var next = ++mCapacity;
            EnsureAliveCapacity(next);
            mAlive[next] = true;
            mCount++;
            return next;
        }

        public bool TryAllocate(out int id)
        {
            id = Allocate();
            return id != 0;
        }

        /// <summary>归还槽位。重复 Free、Free(0)、越界 id 返回 false。</summary>
        public bool Free(int id)
        {
            if (id <= 0 || id > mCapacity || !mAlive[id])
                return false;

            mAlive[id] = false;
            PushFree(id);
            mCount--;
            return true;
        }

        public bool IsAlive(int id)
        {
            return id > 0 && id <= mCapacity && mAlive[id];
        }

        /// <summary>活跃数。</summary>
        public int Count => mCount;

        /// <summary>已发过的最大槽位，单调不减。</summary>
        public int Capacity => mCapacity;

        public void Clear()
        {
            Array.Clear(mAlive, 0, mAlive.Length);
            mFreeCount = 0;
            mCapacity = 0;
            mCount = 0;
        }

        private void PushFree(int id)
        {
            if (mFreeCount == mFree.Length)
                Array.Resize(ref mFree, mFree.Length * 2);
            mFree[mFreeCount++] = id;
        }

        private void EnsureAliveCapacity(int id)
        {
            if (id < mAlive.Length)
                return;

            var size = mAlive.Length;
            while (size <= id)
                size = size <= 0 ? 4 : size * 2;
            Array.Resize(ref mAlive, size);
        }
    }
}
