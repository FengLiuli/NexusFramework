using System;
using System.Collections.Generic;
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
    ///     带运行时 id 引用的 Trait。序列化时把 id 转成稳定 key（id 不进入存档），
    ///     读档时按 key 重建运行时 id。
    ///     <para>
    ///     实现方必须保证 <see cref="CaptureIdRefs" /> 与 <see cref="ApplyIdRefs" /> 成对：
    ///     Capture 按固定顺序添加引用并把对应字段置 0，Apply 按同一顺序写回 Id。
    ///     </para>
    /// </summary>
    public interface IIdRefTrait
    {
        /// <summary>
        ///     按固定顺序收集本 Trait 的 id 引用，并把对应字段临时置 0，
        ///     确保 <see cref="IDataTrait.ToJson" /> 不会写出运行时 id。
        ///     框架在序列化完成后调用 <see cref="ApplyIdRefs" /> 恢复对象。
        /// </summary>
        void CaptureIdRefs(List<IdRef> refs);

        /// <summary>
        ///     按 <see cref="CaptureIdRefs" /> 的顺序，把 refs 中的 <see cref="IdRef.Id" />
        ///     写回 Trait 字段。序列化后用于恢复；读档后用于写入按 key 解析出的 id。
        /// </summary>
        void ApplyIdRefs(IReadOnlyList<IdRef> refs);
    }

    /// <summary>Trait 内的一处运行时 id 引用。</summary>
    public struct IdRef
    {
        /// <summary><see cref="NexusFramework.Identity.IdRegistry" /> 域名，如 <c>item.definition</c>。</summary>
        public string Domain;

        /// <summary>运行时 id。</summary>
        public int Id;

        /// <summary>存档中的稳定 key；序列化时由框架填充。</summary>
        public string Key;
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

        /// <summary>无参构造：存档反序列化经 <see cref="Activator.CreateInstance(Type)" /> 重建实例。</summary>
        public BasicInfoTrait()
        {
            isActive = true;
        }

        public BasicInfoTrait(string name, string description = "")
        {
            this.name = name;
            this.description = description;
            isActive = true;
        }
    }
}