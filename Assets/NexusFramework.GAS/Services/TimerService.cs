using Unity.Entities;
using NexusFramework;
using NexusFramework.GAS.ECS;

namespace NexusFramework.GAS.Services
{
    public class TimerService : AbstractService
    {
        private Entity _timerEntity;

        protected override void OnInit()
        {
            var em = this.GetService<WorldService>().EntityManager;
            _timerEntity = em.CreateSingleton<GlobalTimer>();
        }

        protected override void OnDeinit()
        {
            _timerEntity = Entity.Null;
        }

        public GlobalTimer GetGlobalTimer()
        {
            var em = this.GetService<WorldService>().EntityManager;
            return em.GetComponentData<GlobalTimer>(_timerEntity);
        }

        public int CurrentFrame => GetGlobalTimer().Frame;
        public int CurrentTurn => GetGlobalTimer().Turn;

        /// <summary>
        ///     推进回合数。<see cref="GlobalTimer.Frame"/> 由 SGlobalTimer 每固定步自增，
        ///     但 Turn 代表的是玩法回合，只能由上层显式推进——没有调用则恒为 0，
        ///     此时 <c>TimeUnit.Turn</c> 的 GE 不会自然过期（这是明确语义，不是缺陷）。
        /// </summary>
        public void NextTurn()
        {
            SetTurn(CurrentTurn + 1);
        }

        public void SetTurn(int turn)
        {
            var em = this.GetService<WorldService>().EntityManager;
            var timer = em.GetComponentData<GlobalTimer>(_timerEntity);
            timer.Turn = turn;
            em.SetComponentData(_timerEntity, timer);
        }

        public void ResetTurn()
        {
            SetTurn(0);
        }
    }
}
