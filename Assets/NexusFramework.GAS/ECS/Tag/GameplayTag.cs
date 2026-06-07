using System;

namespace NexusFramework.GAS.ECS
{
    [Serializable]
    public struct GameplayTag : IEquatable<GameplayTag>
    {
        public readonly int Code;
        public readonly int[] Parents;
        public readonly int[] Children;

        public GameplayTag(int tagCode, int[] parents, int[] children)
        {
            Code = tagCode;
            Parents = parents ?? Array.Empty<int>();
            Children = children ?? Array.Empty<int>();
        }
        
        public bool HasTag(int tag)
        {
            if (Code == tag) return true;
            foreach (var pTag in Parents)
                if (pTag == tag)
                    return true;

            return false;
        }

        public bool HasChildTag(int child)
        {
            foreach (var cTag in Children)
                if (cTag == child)
                    return true;

            return false;
        }
        
        public bool HasParentTag(int tag)
        {
            foreach (var pTag in Parents)
                if (pTag == tag)
                    return true;

            return false;
        }
        
        public bool HasTag(GameplayTag tag)
        {
            if (this == tag) return true;
            foreach (var pTag in Parents)
                if (pTag == tag.Code)
                    return true;

            return false;
        }

        public bool HasChildTag(GameplayTag child)
        {
            foreach (var cTag in Children)
                if (cTag == child.Code)
                    return true;

            return false;
        }
        
        public bool HasParentTag(GameplayTag tag)
        {
            foreach (var pTag in Parents)
                if (pTag == tag.Code)
                    return true;

            return false;
        }
        
        public static bool operator ==(GameplayTag x, GameplayTag y)
        {
            return x.Code == y.Code;
        }

        public static bool operator !=(GameplayTag x, GameplayTag y)
        {
            return x.Code != y.Code;
        }
        
        public bool IsRoot => Parents.Length == 0;
        public bool HasChild => Children.Length > 0;

#if UNITY_EDITOR
        public string Name
        {
            get
            {
                return "tag_todo";
            }
        }
#endif
        public bool Equals(GameplayTag other)
        {
            return Code == other.Code && Equals(Parents, other.Parents) && Equals(Children, other.Children);
        }

        public override bool Equals(object obj)
        {
            return obj is GameplayTag other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Code, Parents, Children);
        }
    }
}