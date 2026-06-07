using System;
using System.Collections.Generic;
using NexusFramework.DataCarrier;
using UnityEngine;

namespace NexusFramework
{
    #region IOC Container - 简单的IOC容器实现

    /// <summary>
    ///     简单的IOC容器
    /// </summary>
    public class IOCContainer
    {
        private readonly Dictionary<Type, ICanInit> mInstances = new();

        public void Register<T>(T instance) where T : ICanInit
        {
            var key = typeof(T);
            mInstances[key] = instance;
        }

        public T Get<T>() where T : class, ICanInit
        {
            var key = typeof(T);
            if (mInstances.TryGetValue(key, out var retInstance)) return retInstance as T;

            return null;
        }

        public void InitAll()
        {
            foreach (var instance in mInstances.Values)
            {
                instance.Init();
                instance.Initialized = true;
            }
        }

        public void DeInitAll()
        {
            foreach (var instance in mInstances.Values)
            {
                instance.Deinit();
                instance.Initialized = false;
            }
        }
    }

    #endregion

    #region Event Service - 简单的事件服务实现

    /// <summary>
    ///     类型事件服务
    /// </summary>
    public class TypeEventService
    {
        private readonly Dictionary<Type, List<object>> mEventListeners = new();

        public IUnRegister Register<T>(Action<T> onEvent) where T : struct
        {
            var type = typeof(T);
            if (!mEventListeners.ContainsKey(type)) mEventListeners[type] = new List<object>();

            mEventListeners[type].Add(onEvent);

            return new CustomUnRegister(() =>
            {
                if (mEventListeners.TryGetValue(type, out var list)) list.Remove(onEvent);
            });
        }

        public void Send<T>(T e) where T : struct
        {
            var type = typeof(T);
            if (mEventListeners.TryGetValue(type, out var list))
                for (var i = list.Count - 1; i >= 0; i--)
                    try
                    {
                        ((Action<T>)list[i])(e);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error handling event {type.Name}: {ex.Message}");
                    }
        }

        public void UnRegister<T>(Action<T> onEvent) where T : struct
        {
            var type = typeof(T);
            if (mEventListeners.TryGetValue(type, out var list)) list.Remove(onEvent);
        }

        public void Clear()
        {
            mEventListeners.Clear();
        }
    }

    #endregion

    #region Architecture Base Class - 架构基类实现

    /// <summary>
    ///     架构基类 - 参考原版QFramework设计，支持多实例
    /// </summary>
    public abstract class Architecture : IArchitecture
    {
        #region 内置CarrierManager

        public ICarrierManager GetCarrierManager()
        {
            return mCarrierManager;
        }

        #endregion

        #region 架构标识和状态

        public byte ArchitectureId { get; protected set; }
        public IArchitecture ParentArchitecture { get; set; }
        public abstract string ArchitectureType { get; }
        public ArchitectureState State { get; private set; } = ArchitectureState.NotInitialized;

        #endregion

        #region 内置组件

        private readonly IOCContainer mContainer = new();
        private readonly TypeEventService mTypeEventService = new();
        private readonly ICarrierManager mCarrierManager = new CarrierManager();
        private bool mInitialized;

        #endregion

        #region 构造函数

        protected Architecture()
        {
            // 无参构造器生成默认ID（用于特殊情况）
            ArchitectureId = 0;
        }

        protected Architecture(byte instanceId)
        {
            // 带参构造器使用指定ID（推荐使用）
            ArchitectureId = instanceId;
        }

        #endregion

        #region 生命周期管理

        public void Initialize()
        {
            if (mInitialized) return;

            State = ArchitectureState.Initializing;

            // 发送初始化前事件
            SendEvent(new ArchitectureBeforeInitEvent { ArchitectureId = ArchitectureId });

            // 注册内置CarrierManager - 明确指定接口类型
            mContainer.Register(mCarrierManager);

            // 调用子类初始化
            OnInit();

            // 初始化所有已注册的Service和Model
            InitializeRegisteredComponents();

            mInitialized = true;
            State = ArchitectureState.Initialized;
            
            // 发送初始化后事件
            SendEvent(new ArchitectureAfterInitEvent { ArchitectureId = ArchitectureId });
        }

        public void Pause()
        {
            if (State != ArchitectureState.Initialized) return;
            
            // 发送暂停前事件
            SendEvent(new ArchitectureBeforePauseEvent { ArchitectureId = ArchitectureId });
            
            State = ArchitectureState.Paused;
            OnPause();
        }

        public void Resume()
        {
            if (State != ArchitectureState.Paused) return;
            
            // 发送恢复前事件
            SendEvent(new ArchitectureBeforeResumeEvent { ArchitectureId = ArchitectureId });
            
            State = ArchitectureState.Initialized;
            OnResume();
        }

        public void Shutdown()
        {
            if (State == ArchitectureState.Shutdown) return;

            // 发送关闭前事件
            SendEvent(new ArchitectureBeforeShutdownEvent { ArchitectureId = ArchitectureId });
            
            State = ArchitectureState.Shutting;
            OnShutdown();

            // 清理所有组件
            DeinitializeAllComponents();
            mTypeEventService.Clear();
            mCarrierManager?.Dispose();

            State = ArchitectureState.Shutdown;
            mInitialized = false;
            
            // 发送关闭后事件
            SendEvent(new ArchitectureAfterShutdownEvent { ArchitectureId = ArchitectureId });
        }

        public void Dispose()
        {
            Shutdown();
        }

        #endregion

        #region 子类重写的生命周期方法

        protected abstract void OnInit();

        protected virtual void OnInitialized()
        {
        }

        protected virtual void OnPause()
        {
        }

        protected virtual void OnResume()
        {
        }

        protected virtual void OnShutdown()
        {
        }

        #endregion

        #region 组件注册和获取（保持原有API）

        public void RegisterService<TService>(TService service) where TService : IService
        {
            service.Architecture = this;
            mContainer.Register(service);

            if (mInitialized)
            {
                service.Init();
                service.Initialized = true;
            }
        }

        public void RegisterModel<TModel>(TModel model) where TModel : IModel
        {
            model.Architecture = this;
            mContainer.Register(model);

            if (mInitialized)
            {
                model.Init();
                model.Initialized = true;
            }
        }

        public void RegisterUtility<TUtility>(TUtility utility) where TUtility : IUtility
        {
            mContainer.Register(utility);
        }

        public TService GetService<TService>() where TService : class, IService
        {
            return mContainer.Get<TService>();
        }

        public TModel GetModel<TModel>() where TModel : class, IModel
        {
            return mContainer.Get<TModel>();
        }

        public TUtility GetUtility<TUtility>() where TUtility : class, IUtility
        {
            return mContainer.Get<TUtility>();
        }

        #endregion

        #region 命令和查询（保持原有API）

        public void SendCommand<T>(T command) where T : ICommand
        {
            command.Architecture = this;
            ExecuteCommand(command);
        }

        public TResult SendCommand<TResult>(ICommand<TResult> command)
        {
            command.Architecture = this;
            return ExecuteCommand(command);
        }

        public TResult SendQuery<TResult>(IQuery<TResult> query)
        {
            query.Architecture = this;
            return query.Do();
        }

        #endregion

        #region 事件机制

        public void SendEvent<T>(T e) where T : struct
        {
            mTypeEventService.Send(e);
        }

        public IUnRegister RegisterEvent<T>(Action<T> onEvent) where T : struct
        {
            return mTypeEventService.Register(onEvent);
        }

        public void UnregisterEvent<T>(Action<T> onEvent) where T : struct
        {
            mTypeEventService.UnRegister(onEvent);
        }

        #endregion

        #region 私有方法

        private void ExecuteCommand<T>(T command) where T : ICommand
        {
            try
            {
                command.Execute();
            }
            catch (Exception e)
            {
                Debug.LogError($"Command {typeof(T).Name} execution failed: {e.Message}");
                throw;
            }
        }

        private TResult ExecuteCommand<TResult>(ICommand<TResult> command)
        {
            try
            {
                return command.Execute();
            }
            catch (Exception e)
            {
                Debug.LogError($"Command {command.GetType().Name} execution failed: {e.Message}");
                throw;
            }
        }

        private void InitializeRegisteredComponents()
        {
            mContainer.InitAll();
        }

        private void DeinitializeAllComponents()
        {
            mContainer.DeInitAll();
        }

        #endregion
    }

    #endregion
}