using UnityEngine;
using Unity.Entities;
using NexusFramework.GAS.ECS;

namespace NexusFramework.GAS.Demo
{
    /// <summary>
    /// Demo Log Cue —— 在 OnActivate 时输出日志到 Console
    /// </summary>
    public class DemoLogCue : GameplayCueBase<XParamString>
    {
        public override void InitParameters(XParam xParam)
        {
            // 由基类自动处理 Parameter 赋值
        }

        public override void OnActivate(float time)
        {
            Debug.Log($"[DemoCue] {Parameter?.Value ?? "DemoLogCue activated"} at time {time:F2}");
        }

        public override void OnDeactivate(float time)
        {
            Debug.Log($"[DemoCue] Deactivated at time {time:F2}");
        }
    }

    /// <summary>
    /// Demo Color Cue —— 激活时改变目标 GameObject 颜色，失活时恢复
    /// </summary>
    public class DemoColorCue : GameplayCueBase<XParamFloat>
    {
        private Renderer _targetRenderer;
        private Color _originalColor;
        private bool _hasRenderer;

        public override void InitParameters(XParam xParam)
        {
            // 由基类自动处理 Parameter 赋值
        }

        public override void OnActivate(float time)
        {
            var go = GetTargetAscGameObject();
            if (go == null) return;

            _targetRenderer = go.GetComponentInChildren<Renderer>();
            if (_targetRenderer != null)
            {
                _hasRenderer = true;
                _originalColor = _targetRenderer.material.color;
                var hue = Parameter != null ? Mathf.Repeat(Parameter.Value * 0.1f, 1f) : 0.5f;
                _targetRenderer.material.color = Color.HSVToRGB(hue, 0.8f, 1f);
                Debug.Log($"[DemoColorCue] Changed color of {go.name} to hue={hue:F2}");
            }
        }

        public override void OnDeactivate(float time)
        {
            if (_hasRenderer && _targetRenderer != null)
            {
                _targetRenderer.material.color = _originalColor;
                Debug.Log($"[DemoColorCue] Restored original color");
            }
        }
    }
}
