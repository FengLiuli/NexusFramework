using System.Collections.Generic;

namespace NexusFramework.GAS.ECS
{
    /// <summary>
    ///     时间轴播放器：按逻辑帧推进片段，在区间边界触发任务的 Begin/Tick/Finish。
    ///     <para>
    ///         帧源为 ECS 的 <see cref="GlobalTimer"/>，与 GE 的 <c>TimeUnit.Frame</c> 计时
    ///         共用同一时间轴——技能冷却与时间轴因此天然对齐。
    ///         原版用 <c>Time.deltaTime</c> 换算帧号（并硬编码 60fps），
    ///         会导致时间轴与 GE 在帧率波动时错位。
    ///     </para>
    /// </summary>
    public class ALTimelinePlayer
    {
        private class RuntimeTaskClip
        {
            public int StartFrame;
            public int EndFrame;
            public AbilityTaskBase Task;
        }

        private readonly ALTimeline _alTimeline;
        private readonly List<RuntimeTaskClip> _cache = new();

        /// <summary>相对播放起点的当前帧。从 -1 起是为了让第 0 帧也被 Tick 到。</summary>
        private int _currentFrame = -1;

        /// <summary>播放起始的绝对帧号，首次 Tick 时锁定。</summary>
        private int _startFrame;

        private bool _started;

        public ALTimelinePlayer(ALTimeline alTimeline)
        {
            _alTimeline = alTimeline;
        }

        public bool IsPlaying { get; private set; }

        public XParamTimeline Param => _alTimeline.Data;

        private int LifeTime => Param?.LifeTime ?? 1;

        /// <summary>按当前配置重建片段缓存。参数变更后调用。</summary>
        public void InitData()
        {
            _cache.Clear();
            if (Param?.Tracks == null)
                return;

            foreach (var track in Param.Tracks)
            {
                if (track?.TaskClips == null) continue;

                foreach (var clip in track.TaskClips)
                {
                    var task = clip.InstantiateTask(_alTimeline);
                    if (task == null) continue;

                    _cache.Add(new RuntimeTaskClip
                    {
                        StartFrame = clip.StartTime,
                        EndFrame = clip.EndTime,
                        Task = task
                    });
                }
            }
        }

        public void Play()
        {
            _currentFrame = -1;
            _started = false;
            IsPlaying = true;
        }

        public void Stop()
        {
            if (!IsPlaying) return;

            foreach (var clip in _cache)
                clip.Task?.Dispose();

            IsPlaying = false;
        }

        /// <summary>
        ///     按全局逻辑帧推进。具备追帧能力：若一次推进跨过多帧（如卡顿后回补），
        ///     中间帧的片段事件不会被跳过。
        /// </summary>
        public void Tick(GlobalTimer timer)
        {
            if (!IsPlaying) return;

            if (!_started)
            {
                _startFrame = timer.Frame;
                _started = true;
            }

            var targetFrame = timer.Frame - _startFrame;

            while (_currentFrame < targetFrame)
            {
                _currentFrame++;
                TickFrame(_currentFrame);
            }

            if (_currentFrame >= LifeTime)
            {
                _currentFrame++; // 确保不重复触发片段的结束回调
                OnPlayEnd();
            }
        }

        private void OnPlayEnd()
        {
            IsPlaying = false;

            if (!Param.ManualEndAbility)
                _alTimeline.TryEndSelf();
        }

        private void TickFrame(int frame)
        {
            foreach (var clip in _cache)
            {
                if (frame == clip.StartFrame)
                    clip.Task?.Begin(frame);

                if (frame >= clip.StartFrame && frame <= clip.EndFrame)
                    clip.Task?.Tick(frame);

                if (frame == clip.EndFrame)
                    clip.Task?.Finish(frame);
            }
        }
    }
}
