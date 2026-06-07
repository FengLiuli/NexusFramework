using System;
using System.Collections.Generic;
using NexusFramework.DataCarrier;
using UnityEngine;

namespace NexusFramework
{
    #region Architecture State

    /// <summary>
    ///     架构状态枚举
    /// </summary>
    public enum ArchitectureState
    {
        NotInitialized,
        Initializing,
        Initialized,
        Paused,
        Shutting,
        Shutdown
    }

    #endregion
    
    #region Lifecycle Events
    
    /// <summary>
    /// 架构初始化前事件
    /// </summary>
    public struct ArchitectureBeforeInitEvent
    {
        public byte ArchitectureId;
    }

    /// <summary>
    /// 架构初始化完成后事件
    /// </summary>
    public struct ArchitectureAfterInitEvent
    {
        public byte ArchitectureId;
    }

    /// <summary>
    /// 架构暂停前事件
    /// </summary>
    public struct ArchitectureBeforePauseEvent
    {
        public byte ArchitectureId;
    }

    /// <summary>
    /// 架构恢复前事件
    /// </summary>
    public struct ArchitectureBeforeResumeEvent
    {
        public byte ArchitectureId;
    }

    /// <summary>
    /// 架构关闭前事件
    /// </summary>
    public struct ArchitectureBeforeShutdownEvent
    {
        public byte ArchitectureId;
    }

    /// <summary>
    /// 架构关闭后事件
    /// </summary>
    public struct ArchitectureAfterShutdownEvent
    {
        public byte ArchitectureId;
    }
    
    #endregion

    #region Core Interfaces

    /// <summary>
    ///     架构接口，支持多实例
    /// </summary>
    public interface IArchitecture : IDisposable
    {
        // 架构标识
        byte ArchitectureId { get; }
        string ArchitectureType { get; }
        ArchitectureState State { get; }

        // 生命周期管理
        void Initialize();
        void Pause();
        void Resume();
        void Shutdown();

        // 组件注册和获取（保持原有API）
        void RegisterService<T>(T service) where T : IService;
        void RegisterModel<T>(T model) where T : IModel;
        void RegisterUtility<T>(T utility) where T : IUtility;

        T GetService<T>() where T : class, IService;
        T GetModel<T>() where T : class, IModel;
        T GetUtility<T>() where T : class, IUtility;

        // 命令和查询（保持原有API）
        void SendCommand<T>(T command) where T : ICommand;
        TResult SendCommand<TResult>(ICommand<TResult> command);
        TResult SendQuery<TResult>(IQuery<TResult> query);

        // 事件机制
        void SendEvent<T>(T e) where T : struct;
        IUnRegister RegisterEvent<T>(Action<T> onEvent) where T : struct;
        void UnregisterEvent<T>(Action<T> onEvent) where T : struct;

        // 内置CarrierManager
        ICarrierManager GetCarrierManager();
    }

    #endregion

    #region Rule Interfaces 

    /// <summary>
    ///     标识属于某个架构
    /// </summary>
    public interface IBelongToArchitecture
    {
        IArchitecture Architecture { get; set; }
    }
    
    /// <summary>
    ///     可以获取Service
    /// </summary>
    public interface ICanGetService : IBelongToArchitecture
    {
    }

    /// <summary>
    ///     可以获取Model
    /// </summary>
    public interface ICanGetModel : IBelongToArchitecture
    {
    }

    /// <summary>
    ///     可以获取Utility
    /// </summary>
    public interface ICanGetUtility : IBelongToArchitecture
    {
    }

    /// <summary>
    ///     可以发送Command
    /// </summary>
    public interface ICanSendCommand : IBelongToArchitecture
    {
    }

    /// <summary>
    ///     可以发送Query
    /// </summary>
    public interface ICanSendQuery : IBelongToArchitecture
    {
    }

    /// <summary>
    ///     可以发送Event
    /// </summary>
    public interface ICanSendEvent : IBelongToArchitecture
    {
    }

    /// <summary>
    ///     可以注册Event
    /// </summary>
    public interface ICanRegisterEvent : IBelongToArchitecture
    {
    }

    /// <summary>
    ///     可以初始化
    /// </summary>
    public interface ICanInit
    {
        bool Initialized { get; set; }
        void Init();
        void Deinit();
    }


    /// <summary>
    ///     可以获取载体 - 数据查询接口
    /// </summary>
    public interface ICanGetCarrier : IBelongToArchitecture
    {
    }

    /// <summary>
    ///     可以创建载体 - 载体创建接口
    /// </summary>
    public interface ICanCreateCarrier : IBelongToArchitecture
    {
    }

    /// <summary>
    ///     可以管理载体特征 - 特征管理接口
    /// </summary>
    public interface ICanManageCarrierTrait : IBelongToArchitecture, ICanGetCarrier
    {
    }

    #endregion

    #region Extension Methods - 为小接口提供扩展方法

    /// <summary>
    ///     ICanGetService扩展方法
    /// </summary>
    public static class CanGetServiceExtension
    {
        public static T GetService<T>(this ICanGetService self) where T : class, IService
        {
            return self.Architecture.GetService<T>();
        }
    }

    /// <summary>
    ///     ICanGetModel扩展方法
    /// </summary>
    public static class CanGetModelExtension
    {
        public static T GetModel<T>(this ICanGetModel self) where T : class, IModel
        {
            return self.Architecture.GetModel<T>();
        }
    }

    /// <summary>
    ///     ICanGetUtility扩展方法
    /// </summary>
    public static class CanGetUtilityExtension
    {
        public static T GetUtility<T>(this ICanGetUtility self) where T : class, IUtility
        {
            return self.Architecture.GetUtility<T>();
        }
    }

    /// <summary>
    ///     ICanSendCommand扩展方法
    /// </summary>
    public static class CanSendCommandExtension
    {
        public static void SendCommand<T>(this ICanSendCommand self, T command) where T : ICommand
        {
            self.Architecture.SendCommand(command);
        }

        public static TResult SendCommand<TResult>(this ICanSendCommand self, ICommand<TResult> command)
        {
            return self.Architecture.SendCommand(command);
        }
    }

    /// <summary>
    ///     ICanSendQuery扩展方法
    /// </summary>
    public static class CanSendQueryExtension
    {
        public static TResult SendQuery<TResult>(this ICanSendQuery self, IQuery<TResult> query)
        {
            return self.Architecture.SendQuery(query);
        }
    }

    /// <summary>
    ///     ICanSendEvent扩展方法
    /// </summary>
    public static class CanSendEventExtension
    {
        public static void SendEvent<T>(this ICanSendEvent self, T e) where T : struct
        {
            self.Architecture.SendEvent(e);
        }
    }

    /// <summary>
    ///     ICanRegisterEvent扩展方法
    /// </summary>
    public static class CanRegisterEventExtension
    {
        public static IUnRegister RegisterEvent<T>(this ICanRegisterEvent self, Action<T> onEvent) where T : struct
        {
            return self.Architecture.RegisterEvent(onEvent);
        }

        public static void UnregisterEvent<T>(this ICanRegisterEvent self, Action<T> onEvent) where T : struct
        {
            self.Architecture.UnregisterEvent(onEvent);
        }
    }

    /// <summary>
    ///     ICanGetCarrier扩展方法 - 载体查询功能
    /// </summary>
    public static class CanGetCarrierExtension
    {
        public static bool IsCarrierValid(this ICanGetCarrier self, CarrierId carrierId)
        {
            return self.Architecture.GetCarrierManager().IsCarrierValid(carrierId);
        }

        public static ushort GetCarrierTypeId(this ICanGetCarrier self, CarrierId carrierId)
        {
            return self.Architecture.GetCarrierManager().GetCarrierTypeId(carrierId);
        }

        public static IEnumerable<CarrierId> GetCarriersByTypeId(this ICanGetCarrier self, ushort typeId)
        {
            return self.Architecture.GetCarrierManager().GetCarriersByType(typeId);
        }
        
        public static IEnumerable<CarrierId> GetCarriersByTypeName(this ICanGetCarrier self, string typeName)
        {
            var typeId = self.Architecture.GetCarrierManager().GetTypeId(typeName);
            return self.Architecture.GetCarrierManager().GetCarriersByType(typeId);
        }

        public static IEnumerable<CarrierId> FindCarriersWithTrait<T>(this ICanGetCarrier self)
            where T : class, IDataTrait
        {
            return self.Architecture.GetCarrierManager().FindCarriersWithTrait<T>();
        }

        public static IEnumerable<CarrierId> FindCarriersWithTraits(this ICanGetCarrier self, params Type[] traitTypes)
        {
            return self.Architecture.GetCarrierManager().FindCarriersWithTraits(traitTypes);
        }
        
        

        // 只读的特征访问方法（用于Query等只读组件）
        public static T GetTrait<T>(this ICanGetCarrier self, CarrierId carrierId) where T : class, IDataTrait
        {
            return self.Architecture.GetCarrierManager().GetTrait<T>(carrierId);
        }

        public static bool HasTrait<T>(this ICanGetCarrier self, CarrierId carrierId) where T : class, IDataTrait
        {
            return self.Architecture.GetCarrierManager().HasTrait<T>(carrierId);
        }

        public static IEnumerable<IDataTrait> GetAllTraits(this ICanGetCarrier self, CarrierId carrierId)
        {
            return self.Architecture.GetCarrierManager().GetAllTraits(carrierId);
        }

        public static IEnumerable<Type> GetTraitTypes(this ICanGetCarrier self, CarrierId carrierId)
        {
            return self.Architecture.GetCarrierManager().GetTraitTypes(carrierId);
        }

        // 类型注册和查询方法
        public static ushort RegisterCarrierType(this ICanGetCarrier self, string typeName)
        {
            return self.Architecture.GetCarrierManager().RegisterType(typeName);
        }

        public static bool IsCarrierTypeRegistered(this ICanGetCarrier self, string typeName)
        {
            return self.Architecture.GetCarrierManager().IsTypeRegistered(typeName);
        }

        public static ushort GetCarrierTypeId(this ICanGetCarrier self, string typeName)
        {
            return self.Architecture.GetCarrierManager().GetTypeId(typeName);
        }

        public static string GetCarrierTypeName(this ICanGetCarrier self, ushort typeId)
        {
            return self.Architecture.GetCarrierManager().GetTypeName(typeId);
        }
        
    }

    /// <summary>
    ///     ICanCreateCarrier扩展方法 - 载体创建功能
    /// </summary>
    public static class CanCreateCarrierExtension
    {
        public static CarrierId CreateCarrier(this ICanCreateCarrier self, ushort typeId)
        {
            return self.Architecture.GetCarrierManager().CreateCarrier(typeId);
        }

        public static CarrierId CreateCarrier(this ICanCreateCarrier self, string typeName)
        {
            var manager = self.Architecture.GetCarrierManager();
            if (!manager.IsTypeRegistered(typeName)) manager.RegisterType(typeName);
            var typeId = manager.GetTypeId(typeName);
            return manager.CreateCarrier(typeId);
        }

        public static CarrierId CreateCarrier(this IArchitecture self, string typeName)
        {
            var manager = self.GetCarrierManager();
            if (!manager.IsTypeRegistered(typeName)) manager.RegisterType(typeName);
            var typeId = manager.GetTypeId(typeName);
            return manager.CreateCarrier(typeId);
        }

        // 添加指定ID创建载体的扩展方法
        public static bool CreateCarrier(this ICanCreateCarrier self, ushort typeId, ulong uniqueId)
        {
            return self.Architecture.GetCarrierManager().CreateCarrier(typeId, uniqueId);
        }

        public static bool CreateCarrier(this ICanCreateCarrier self, string typeName, ulong uniqueId)
        {
            var manager = self.Architecture.GetCarrierManager();
            if (!manager.IsTypeRegistered(typeName)) manager.RegisterType(typeName);
            var typeId = manager.GetTypeId(typeName);
            return manager.CreateCarrier(typeId, uniqueId);
        }

        public static bool CreateCarrier(this IArchitecture self, string typeName, ulong uniqueId)
        {
            var manager = self.GetCarrierManager();
            if (!manager.IsTypeRegistered(typeName)) manager.RegisterType(typeName);
            var typeId = manager.GetTypeId(typeName);
            return manager.CreateCarrier(typeId, uniqueId);
        }

        public static bool DestroyCarrier(this ICanCreateCarrier self, CarrierId carrierId)
        {
            return self.Architecture.GetCarrierManager().DestroyCarrier(carrierId);
        }
    }

    /// <summary>
    ///     ICanManageCarrierTrait扩展方法 - 特征管理功能
    /// </summary>
    public static class CanManageCarrierTraitExtension
    {
        public static bool AddTrait<T>(this ICanManageCarrierTrait self, CarrierId carrierId, T trait)
            where T : class, IDataTrait
        {
            return self.Architecture.GetCarrierManager().AddTrait(carrierId, trait);
        }

        public static bool RemoveTrait<T>(this ICanManageCarrierTrait self, CarrierId carrierId)
            where T : class, IDataTrait
        {
            return self.Architecture.GetCarrierManager().RemoveTrait<T>(carrierId);
        }

        public static IEnumerable<IDataTrait> GetAllTraits(this ICanManageCarrierTrait self, CarrierId carrierId)
        {
            return self.Architecture.GetCarrierManager().GetAllTraits(carrierId);
        }

        public static IEnumerable<Type> GetTraitTypes(this ICanManageCarrierTrait self, CarrierId carrierId)
        {
            return self.Architecture.GetCarrierManager().GetTraitTypes(carrierId);
        }
    }

    #endregion

    #region Event Service Support

    /// <summary>
    ///     事件注销接口
    /// </summary>
    public interface IUnRegister
    {
        void UnRegister();
    }

    /// <summary>
    ///     简单的事件注销实现
    /// </summary>
    public class CustomUnRegister : IUnRegister
    {
        private Action mOnUnRegister;

        public CustomUnRegister(Action onUnRegister)
        {
            mOnUnRegister = onUnRegister;
        }

        public void UnRegister()
        {
            mOnUnRegister?.Invoke();
            mOnUnRegister = null;
        }
    }

    public interface IEasyEvent
    {
        IUnRegister Register(Action onEvent);
    }

    public class EasyEvent : IEasyEvent
    {
        private Action mOnEvent = () => { };

        public IUnRegister Register(Action onEvent)
        {
            mOnEvent += onEvent;
            return new CustomUnRegister(() => { UnRegister(onEvent); });
        }

        public IUnRegister RegisterWithACall(Action onEvent)
        {
            onEvent.Invoke();
            return Register(onEvent);
        }

        public void UnRegister(Action onEvent)
        {
            mOnEvent -= onEvent;
        }

        public void Trigger()
        {
            mOnEvent?.Invoke();
        }
    }

    public class EasyEvent<T> : IEasyEvent
    {
        private Action<T> mOnEvent = _ => { };

        IUnRegister IEasyEvent.Register(Action onEvent)
        {
            return Register(Action);

            void Action(T _)
            {
                onEvent();
            }
        }

        public IUnRegister Register(Action<T> onEvent)
        {
            mOnEvent += onEvent;
            return new CustomUnRegister(() => { UnRegister(onEvent); });
        }

        public void UnRegister(Action<T> onEvent)
        {
            mOnEvent -= onEvent;
        }


        public void Trigger(T t)
        {
            mOnEvent?.Invoke(t);
        }
    }

    public class EasyEvent<T, K> : IEasyEvent
    {
        private Action<T, K> mOnEvent = (_, _) => { };

        IUnRegister IEasyEvent.Register(Action onEvent)
        {
            return Register(Action);

            void Action(T _, K __)
            {
                onEvent();
            }
        }

        public IUnRegister Register(Action<T, K> onEvent)
        {
            mOnEvent += onEvent;
            return new CustomUnRegister(() => { UnRegister(onEvent); });
        }

        public void UnRegister(Action<T, K> onEvent)
        {
            mOnEvent -= onEvent;
        }

        public void Trigger(T t, K k)
        {
            mOnEvent?.Invoke(t, k);
        }
    }

    public class EasyEvent<T, K, S> : IEasyEvent
    {
        private Action<T, K, S> mOnEvent = (_, _, _) => { };

        IUnRegister IEasyEvent.Register(Action onEvent)
        {
            return Register(Action);

            void Action(T _, K __, S ___)
            {
                onEvent();
            }
        }

        public IUnRegister Register(Action<T, K, S> onEvent)
        {
            mOnEvent += onEvent;
            return new CustomUnRegister(() => { UnRegister(onEvent); });
        }

        public void UnRegister(Action<T, K, S> onEvent)
        {
            mOnEvent -= onEvent;
        }

        public void Trigger(T t, K k, S s)
        {
            mOnEvent?.Invoke(t, k, s);
        }
    }

    public class EasyEvents
    {
        private static readonly EasyEvents mGlobalEvents = new();

        private readonly Dictionary<Type, IEasyEvent> mTypeEvents = new();

        public static T Get<T>() where T : IEasyEvent
        {
            return mGlobalEvents.GetEvent<T>();
        }

        public static void Register<T>() where T : IEasyEvent, new()
        {
            mGlobalEvents.AddEvent<T>();
        }

        public void AddEvent<T>() where T : IEasyEvent, new()
        {
            mTypeEvents.Add(typeof(T), new T());
        }

        public T GetEvent<T>() where T : IEasyEvent
        {
            return mTypeEvents.TryGetValue(typeof(T), out var e) ? (T)e : default;
        }

        public T GetOrAddEvent<T>() where T : IEasyEvent, new()
        {
            var eType = typeof(T);
            if (mTypeEvents.TryGetValue(eType, out var e)) return (T)e;

            var t = new T();
            mTypeEvents.Add(eType, t);
            return t;
        }
    }

    #endregion

    #region BindableProperty

    public interface IBindableProperty<T> : IReadonlyBindableProperty<T>
    {
        new T Value { get; set; }
        void SetValueWithoutEvent(T newValue);
    }

    public interface IReadonlyBindableProperty<T> : IEasyEvent
    {
        T Value { get; }

        IUnRegister RegisterWithInitValue(Action<T> action);
        void UnRegister(Action<T> onValueChanged);
        IUnRegister Register(Action<T> onValueChanged);
    }

    public class BindableProperty<T> : IBindableProperty<T>
    {
        private readonly EasyEvent<T> mOnValueChanged = new();

        protected T mValue;

        public BindableProperty(T defaultValue = default)
        {
            mValue = defaultValue;
        }

        public static Func<T, T, bool> Comparer { get; set; } = (a, b) => a.Equals(b);

        public T Value
        {
            get => GetValue();
            set
            {
                if (value == null && mValue == null) return;
                if (value != null && Comparer(value, mValue)) return;

                SetValue(value);
                mOnValueChanged.Trigger(value);
            }
        }

        public void SetValueWithoutEvent(T newValue)
        {
            mValue = newValue;
        }

        public IUnRegister Register(Action<T> onValueChanged)
        {
            return mOnValueChanged.Register(onValueChanged);
        }

        public IUnRegister RegisterWithInitValue(Action<T> onValueChanged)
        {
            onValueChanged(mValue);
            return Register(onValueChanged);
        }

        public void UnRegister(Action<T> onValueChanged)
        {
            mOnValueChanged.UnRegister(onValueChanged);
        }

        IUnRegister IEasyEvent.Register(Action onEvent)
        {
            return Register(Action);

            void Action(T _)
            {
                onEvent();
            }
        }

        public BindableProperty<T> WithComparer(Func<T, T, bool> comparer)
        {
            Comparer = comparer;
            return this;
        }

        protected virtual void SetValue(T newValue)
        {
            mValue = newValue;
        }

        protected virtual T GetValue()
        {
            return mValue;
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }

    internal class ComparerAutoRegister
    {
#if UNITY_5_6_OR_NEWER
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void AutoRegister()
        {
            BindableProperty<int>.Comparer = (a, b) => a == b;
            BindableProperty<float>.Comparer = Mathf.Approximately;
            BindableProperty<double>.Comparer = (a, b) => Math.Abs(a - b) < Double.Epsilon;
            BindableProperty<string>.Comparer = (a, b) => a == b;
            BindableProperty<long>.Comparer = (a, b) => a == b;
            BindableProperty<Vector2>.Comparer = (a, b) => a == b;
            BindableProperty<Vector3>.Comparer = (a, b) => a == b;
            BindableProperty<Vector4>.Comparer = (a, b) => a == b;
            BindableProperty<Color>.Comparer = (a, b) => a == b;
            BindableProperty<Color32>.Comparer =
                (a, b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
            BindableProperty<Bounds>.Comparer = (a, b) => a == b;
            BindableProperty<Rect>.Comparer = (a, b) => a == b;
            BindableProperty<Quaternion>.Comparer = (a, b) => a == b;
            BindableProperty<Vector2Int>.Comparer = (a, b) => a == b;
            BindableProperty<Vector3Int>.Comparer = (a, b) => a == b;
            BindableProperty<BoundsInt>.Comparer = (a, b) => a == b;
            BindableProperty<RangeInt>.Comparer = (a, b) => a.start == b.start && a.length == b.length;
            BindableProperty<RectInt>.Comparer = (a, b) => a.Equals(b);
        }
#endif
    }

    #endregion

    #region Component Interfaces

    /// <summary>
    ///     Controller接口 - 表现层
    ///     可以获取Service、Model、发送Command、Query，可以注册Event
    ///     可以获取载体、创建载体、管理载体特征（完整的数据载体访问权限）
    /// </summary>
    public interface IController : IBelongToArchitecture, ICanSendCommand, ICanGetService, ICanGetModel,
        ICanSendEvent, ICanRegisterEvent, ICanSendQuery, ICanGetUtility,
        ICanGetCarrier, ICanCreateCarrier, ICanManageCarrierTrait
    {
    }

    /// <summary>
    ///     Service接口 - 服务层
    ///     可以获取Model、Service，可以发送Event，可以初始化
    ///     可以获取载体、创建载体、管理载体特征（完整的数据载体访问权限）
    /// </summary>
    public interface IService : IBelongToArchitecture,  ICanGetModel, ICanGetUtility,
        ICanRegisterEvent, ICanSendEvent, ICanGetService, ICanInit,
        ICanGetCarrier, ICanCreateCarrier, ICanManageCarrierTrait
    {
    }

    /// <summary>
    ///     Model接口 - 数据层
    ///     只能获取Utility，发送Event，可以初始化
    /// </summary>
    public interface IModel : IBelongToArchitecture,  ICanGetUtility, ICanSendEvent, ICanInit
    {
    }

    /// <summary>
    ///     Utility接口 - 工具层
    /// </summary>
    public interface IUtility : ICanInit
    {
    }

    /// <summary>
    ///     Command接口 - 命令
    ///     可以获取Service、Model，可以发送Event、Command、Query
    ///     可以获取载体、创建载体、管理载体特征（完整的数据载体访问权限）
    /// </summary>
    public interface ICommand : IBelongToArchitecture,  ICanGetService, ICanGetModel, ICanGetUtility,
        ICanSendEvent, ICanSendCommand, ICanSendQuery,
        ICanGetCarrier, ICanCreateCarrier, ICanManageCarrierTrait
    {
        void Execute();
    }

    /// <summary>
    ///     Command接口（带返回值）
    ///     可以获取Service、Model，可以发送Event、Command、Query
    ///     可以获取载体、创建载体、管理载体特征（完整的数据载体访问权限）
    /// </summary>
    public interface ICommand<TResult> : IBelongToArchitecture,  ICanGetService, ICanGetModel,
        ICanGetUtility, ICanSendEvent, ICanSendCommand, ICanSendQuery,
        ICanGetCarrier, ICanCreateCarrier, ICanManageCarrierTrait
    {
        TResult Execute();
    }

    /// <summary>
    ///     Query接口 - 查询
    ///     可以获取Model、Service，可以发送Query
    ///     只能获取载体和查询载体特征（只读访问，不能创建或修改）
    /// </summary>
    public interface IQuery<TResult> : IBelongToArchitecture,  ICanGetModel, ICanGetService,
        ICanSendQuery, ICanGetUtility, ICanGetCarrier
    {
        TResult Do();
    }

    #endregion

    #region Abstract Base Classes - 抽象基类实现

    /// <summary>
    ///     Service抽象基类
    /// </summary>
    public abstract class AbstractService : IService
    {
        public bool Initialized { get; set; }

        void ICanInit.Init()
        {
            OnInit();
        }

        void ICanInit.Deinit()
        {
            OnDeinit();
        }

        protected abstract void OnDeinit();
        protected abstract void OnInit();

        public IArchitecture Architecture { get; set; }
    }

    /// <summary>
    ///     Model抽象基类
    /// </summary>
    public abstract class AbstractModel :  IModel
    {
        public IArchitecture Architecture { get; set; }

        public bool Initialized { get; set; }

        void ICanInit.Init()
        {
            OnInit();
        }

        public void Deinit()
        {
            OnDeinit();
        }

        protected virtual void OnDeinit()
        {
        }

        protected abstract void OnInit();
    }

    /// <summary>
    ///     Model抽象基类
    /// </summary>
    public abstract class AbstractModelSO : ScriptableObject, IModel
    {
        public IArchitecture Architecture { get; set; }

        public bool Initialized { get; set; }

        void ICanInit.Init()
        {
            OnInit();
        }

        public void Deinit()
        {
            OnDeinit();
        }

        protected virtual void OnDeinit()
        {
        }

        protected abstract void OnInit();
    }

    /// <summary>
    ///     Command抽象基类
    /// </summary>
    public abstract class AbstractCommand : ICommand
    {
        void ICommand.Execute()
        {
            OnExecute();
        }

        protected abstract void OnExecute();

        public IArchitecture Architecture { get; set; }
    }

    /// <summary>
    ///     Command抽象基类（带返回值）
    /// </summary>
    public abstract class AbstractCommand<TResult> : ICommand<TResult>
    {
        public IArchitecture Architecture { get; set; }

        TResult ICommand<TResult>.Execute()
        {
            return OnExecute();
        }

        protected abstract TResult OnExecute();
    }

    /// <summary>
    ///     Query抽象基类
    /// </summary>
    public abstract class AbstractQuery<T> : IQuery<T>
    {
        public T Do()
        {
            return OnDo();
        }

        public IArchitecture Architecture { get; set; }

        protected abstract T OnDo();
    }
    
    #endregion
}