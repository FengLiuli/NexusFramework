using System;
using UnityEngine;

namespace NexusFramework.DataCarrier
{
    /// <summary>
    ///     数据特征接口 - ECS中的Component概念
    /// </summary>
    public interface IDataTrait
    {
        /// <summary>
        ///     特征类型名称
        /// </summary>
        string TraitTypeName { get; }

        /// <summary>
        ///     克隆特征数据
        /// </summary>
        IDataTrait Clone();

        /// <summary>
        ///     序列化为JSON
        /// </summary>
        string ToJson();

        /// <summary>
        ///     从JSON反序列化
        /// </summary>
        void FromJson(string json);
    }

    /// <summary>
    ///     数据特征抽象基类
    /// </summary>
    [Serializable]
    public abstract class DataTrait : IDataTrait
    {
        public virtual string TraitTypeName => GetType().Name;

        public virtual IDataTrait Clone()
        {
            var json = ToJson();
            var clone = (IDataTrait)Activator.CreateInstance(GetType());
            clone.FromJson(json);
            return clone;
        }

        public virtual string ToJson()
        {
            return JsonUtility.ToJson(this);
        }

        public virtual void FromJson(string json)
        {
            JsonUtility.FromJsonOverwrite(json, this);
        }
    }
    
    [Serializable]
    public class ScriptableObjectDataTrait : ScriptableObject, IDataTrait
    {
        public virtual string TraitTypeName => GetType().Name;

        public virtual IDataTrait Clone()
        {
            var clone = ScriptableObject.CreateInstance(GetType()) as ScriptableObjectDataTrait;
            if (clone != null)
            {
                clone.name = name;
                // 使用JsonUtility进行深度复制
                var json = ToJson();
                clone.FromJson(json);
            }
            return clone;
        }

        public virtual string ToJson()
        {
            return JsonUtility.ToJson(this);
        }

        public virtual void FromJson(string json)
        {
            JsonUtility.FromJsonOverwrite(json, this);
        }
    }

    /// <summary>
    ///     基础属性特征
    /// </summary>
    [Serializable]
    public class BasicInfoTrait : DataTrait
    {
        public string name;
        public string description;
        public string iconPath;
        public bool isActive;
        
        public BasicInfoTrait(string name, string description = "")
        {
            this.name = name;
            this.description = description;
            isActive = true;
        }
    }
}