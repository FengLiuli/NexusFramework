using UnityEngine;
using UnityEngine.UI;
using Unity.Collections;
using Unity.Entities;
using NexusFramework;
using NexusFramework.DataCarrier;
using NexusFramework.GAS;
using NexusFramework.GAS.ECS;
using NexusFramework.GAS.Events;
using NexusFramework.GAS.Models;
using NexusFramework.GAS.Services;

namespace NexusFramework.GAS.Demo
{
    public class DemoSceneManager : MonoBehaviour
    {
        [Header("UI References")]
        public Text logText;
        public int maxLogLines = 10;

        [Header("Unit Settings")]
        public string playerName = "Hero";
        public string enemyName = "Goblin";
        public float playerMaxHp = 500f;
        public float playerMaxMp = 200f;
        public float playerAttack = 30f;
        public float enemyMaxHp = 500f;

        private DemoGameArchitecture _arch;
        private CarrierId _playerCarrier;
        private CarrierId _enemyCarrier;
        private Entity _playerEntity;
        private Entity _enemyEntity;
        private EntityManager _em;
        private AbilityService _abilityService;
        private AttributeService _attributeService;
        private GASEntityMapModel _entityMap;

        private GameObject _playerGo;
        private GameObject _enemyGo;
        private DemoUnitUI _playerUI;
        private DemoUnitUI _enemyUI;

        private Text _logText;
        private string _logLines = "";
        private bool _initialized;
        private int _frameCount;

        void Awake()
        {
            _logText = logText;
            AddLog("Initializing GAS...");
        }

        void Start()
        {
            InitializeGAS();
            CreateUnits();
            SetupUI();
            RegisterEvents();
            _initialized = true;
            AddLog("Demo ready! Q=Fireball W=Heal E=Poison R=Buff");
        }

        void LateUpdate()
        {
            DemoDeferredQueue.Drain();
        }

        void Update()
        {
            if (!_initialized) return;
            _frameCount++;
            if (_frameCount % 10 == 0) RefreshUI();
            HandleInput();
        }

        void OnDestroy()
        {
            if (_arch != null)
            {
                AddLog("Shutting down...");
                _arch.Dispose();
                _arch = null;
            }
        }

        // ── Init ──

        private void InitializeGAS()
        {
            _arch = new DemoGameArchitecture();
            _arch.Initialize();
            _arch.GetCarrierManager().RegisterType("Hero");
            _arch.GetCarrierManager().RegisterType("Enemy");
            _em = _arch.GetService<WorldService>().EntityManager;
            _abilityService = _arch.GetService<AbilityService>();
            _attributeService = _arch.GetService<AttributeService>();
            _entityMap = _arch.GetModel<GASEntityMapModel>();
            AddLog("GAS Architecture initialized.");
        }

        // ── Units ──

        private void CreateUnits()
        {
            _playerGo = CreateUnitGo(playerName, new Vector3(-5f, 0.5f, 0f), Color.blue);
            _playerCarrier = _arch.CreateGASCarrier("Hero", _playerGo);
            _playerEntity = _entityMap.GetGASEntity(_playerCarrier);
            InitAttributes(_playerEntity, playerMaxHp, playerMaxMp, playerAttack);
            _abilityService.GrantAbility(_playerCarrier, 1001, _arch);
            _abilityService.GrantAbility(_playerCarrier, 1002, _arch);
            _abilityService.GrantAbility(_playerCarrier, 1003, _arch);
            _abilityService.GrantAbility(_playerCarrier, 1004, _arch);
            _playerUI = _playerGo.AddComponent<DemoUnitUI>();
            _playerUI.unitDisplayName = playerName;
            _playerUI.Initialize(_playerCarrier, this, playerMaxHp, playerMaxMp);
            AddLog($"Player: {_playerCarrier}");

            _enemyGo = CreateUnitGo(enemyName, new Vector3(5f, 0.5f, 0f), Color.red);
            _enemyCarrier = _arch.CreateGASCarrier("Enemy", _enemyGo);
            _enemyEntity = _entityMap.GetGASEntity(_enemyCarrier);
            InitAttributes(_enemyEntity, enemyMaxHp, 0f, 0f);
            _enemyUI = _enemyGo.AddComponent<DemoUnitUI>();
            _enemyUI.unitDisplayName = enemyName;
            _enemyUI.Initialize(_enemyCarrier, this, enemyMaxHp, 0f);
            AddLog($"Enemy: {_enemyCarrier}");
        }

        private GameObject CreateUnitGo(string name, Vector3 pos, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
            var rb = go.GetComponent<Rigidbody>();
            if (rb == null) rb = go.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;
            go.GetComponent<Renderer>().material.color = color;
            return go;
        }

        private void InitAttributes(Entity e, float hp, float mp, float attack)
        {
            if (!_em.HasBuffer<BEAttrSet>(e)) return;
            var buf = _em.GetBuffer<BEAttrSet>(e);
            int n = (hp > 0 ? 1 : 0) + (mp > 0 ? 1 : 0) + (attack > 0 ? 1 : 0);
            if (n == 0) return;
            var attrs = new NativeArray<CAttributeData>(n, Allocator.Persistent);
            int i = 0;
            if (hp > 0) attrs[i++] = new CAttributeData { Code = 1, BaseValue = hp, CurrentValue = hp, IsClampMin = true, MinValue = 0f, IsClampMax = true, MaxValue = 9999f };
            if (mp > 0) attrs[i++] = new CAttributeData { Code = 2, BaseValue = mp, CurrentValue = mp, IsClampMin = true, MinValue = 0f, IsClampMax = true, MaxValue = 9999f };
            if (attack > 0) attrs[i] = new CAttributeData { Code = 3, BaseValue = attack, CurrentValue = attack };
            buf.Add(new BEAttrSet { Code = 1, Attributes = attrs });
        }

        // ── UI ──

        private void SetupUI() { if (_logText == null) _logText = logText; RefreshUI(); }

        private void RefreshUI()
        {
            if (_playerCarrier.IsValid && _playerUI != null)
            {
                _playerUI.UpdateHp(_attributeService.GetCurrentValue(_playerCarrier, 1, 1), playerMaxHp);
                _playerUI.UpdateMp(_attributeService.GetCurrentValue(_playerCarrier, 1, 2), playerMaxMp);
            }
            if (_enemyCarrier.IsValid && _enemyUI != null)
                _enemyUI.UpdateHp(_attributeService.GetCurrentValue(_enemyCarrier, 1, 1), enemyMaxHp);
        }

        // ── Events ──

        private void RegisterEvents()
        {
            _arch.RegisterEvent<GASAttributeChangedEvent>(e =>
            {
                string un = e.CarrierId.Equals(_playerCarrier) ? playerName : enemyName;
                string an = e.AttrCode switch { 1 => "HP", 2 => "MP", 3 => "ATK", _ => $"Attr" };
                AddLog($"{un} {an}: {e.OldValue:F0} → {e.NewValue:F0}");
            });
            _arch.RegisterEvent<GASAbilityActivatedEvent>(e =>
            {
                string n = e.AbilityCode switch { 1001 => "Fireball", 1002 => "Heal", 1003 => "PoisonStrike", 1004 => "PowerBuff", _ => $"#{e.AbilityCode}" };
                AddLog($"{(e.Success ? "✓" : "✗")} {n}");
            });
        }

        // ── Input ──

        void HandleInput()
        {
            if (Input.GetKeyDown(KeyCode.Q)) TryActivate(1001);
            if (Input.GetKeyDown(KeyCode.W)) TryActivate(1002);
            if (Input.GetKeyDown(KeyCode.E)) TryActivate(1003);
            if (Input.GetKeyDown(KeyCode.R)) TryActivate(1004);
            if (Input.GetKeyDown(KeyCode.F1)) PrintDebug();
        }

        void TryActivate(int code)
        {
            if (!_playerCarrier.IsValid) return;
            if (_abilityService.IsActive(_playerCarrier, code))
            {
                AddLog("On cooldown!");
                return;
            }
            bool ok = _abilityService.TryActivate(_playerCarrier, code);
            AddLog(ok ? $">>> Skill#{code}" : $"XXX Fail");
        }

        void PrintDebug()
        {
            float hp = _attributeService.GetCurrentValue(_playerCarrier, 1, 1);
            float mp = _attributeService.GetCurrentValue(_playerCarrier, 1, 2);
            float atk = _attributeService.GetCurrentValue(_playerCarrier, 1, 3);
            AddLog($"Hero: HP={hp:F0}/{playerMaxHp} MP={mp}/{playerMaxMp} ATK={atk:F0}");
            float eHp = _attributeService.GetCurrentValue(_enemyCarrier, 1, 1);
            AddLog($"Goblin: HP={eHp:F0}/{enemyMaxHp}");
        }

        // ── Log ──

        void AddLog(string msg)
        {
            Debug.Log($"[Demo] {msg}");
            _logLines = $"[{Time.frameCount}] {msg}\n" + _logLines;
            if (_logText != null) _logText.text = _logLines;
        }

        public CarrierId GetPlayerCarrier() => _playerCarrier;
        public CarrierId GetEnemyCarrier() => _enemyCarrier;

        /// <summary>供 Editor 测试：模拟技能快捷键</summary>
        public void SimulateQ() => TryActivate(1001);
        public void SimulateW() => TryActivate(1002);
        public void SimulateE() => TryActivate(1003);
        public void SimulateR() => TryActivate(1004);
        public void SimulateF1() => PrintDebug();
    }
}
