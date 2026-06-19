using UnityEngine;
using UnityEngine.UI;
using NexusFramework.DataCarrier;

namespace NexusFramework.GAS.Demo
{
    /// <summary>
    /// Demo 单位 UI —— 显示 HP / MP / 名称
    /// 挂载在 Unit GameObject 上，作为 World Space Canvas
    /// </summary>
    public class DemoUnitUI : MonoBehaviour
    {
        [Header("UI References")]
        public Slider hpBar;
        public Slider mpBar;
        public Text unitName;
        public Text statusText;

        [Header("Settings")]
        public string unitDisplayName = "Unit";
        public Color hpHighColor = Color.green;
        public Color hpLowColor = Color.red;

        private CarrierId _carrierId;
        private DemoSceneManager _sceneManager;

        private float _maxHp = 500f;
        private float _maxMp = 200f;

        public CarrierId CarrierId => _carrierId;

        public void Initialize(CarrierId carrierId, DemoSceneManager manager, float maxHp, float maxMp)
        {
            _carrierId = carrierId;
            _sceneManager = manager;
            _maxHp = maxHp;
            _maxMp = maxMp;

            if (unitName != null)
                unitName.text = unitDisplayName;
        }

        public void UpdateHp(float currentHp, float maxHp)
        {
            _maxHp = maxHp;
            if (hpBar != null)
            {
                hpBar.value = currentHp / maxHp;
                // 颜色渐变：绿色(高) → 黄色(中) → 红色(低)
                var fill = hpBar.fillRect?.GetComponentInChildren<Image>();
                if (fill != null)
                {
                    float t = currentHp / maxHp;
                    fill.color = Color.Lerp(hpLowColor, hpHighColor, t);
                }
            }
        }

        public void UpdateMp(float currentMp, float maxMp)
        {
            _maxMp = maxMp;
            if (mpBar != null)
                mpBar.value = maxMp > 0 ? currentMp / maxMp : 0f;
        }

        public void UpdateStatus(string status)
        {
            if (statusText != null)
                statusText.text = status;
        }
    }
}
