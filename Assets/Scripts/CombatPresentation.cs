using System.Collections.Generic;
using FishNet.Object;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Local presentation only. Network results are delivered by HitscanShooter.
public sealed class CombatPresentation : MonoBehaviour
{
    public Camera playerCamera;
    public Canvas playerCanvas;
    public GameObject bloodImpact;
    public GameObject solidImpact;
    [Min(0f)] public float recoilKick = 1.5f;
    [Min(0f)] public float recoilReturnSpeed = 8f;
    [Min(0.01f)] public float hitFlashDuration = 0.15f;
    [Min(0.1f)] public float impactLifetime = 4f;
    [Min(1)] public int maxImpacts = 16;

    private NetworkObject _player;
    private Health _health;
    private Transform _recoilPivot;
    private RectTransform _hud;
    private TMP_Text _hpText;
    private GameObject _hitFlash;
    private float _recoil;
    private float _flashUntil;
    private readonly Queue<GameObject> _effects = new();

    private bool LocalOwner => _player != null && _player.IsClientInitialized && _player.IsOwner;

    private void Awake()
    {
        _player = GetComponent<NetworkObject>();
        _health = GetComponent<Health>();
        if (playerCamera != null)
        {
            Transform cameraTransform = playerCamera.transform;
            _recoilPivot = new GameObject("RecoilPivot").transform;
            _recoilPivot.SetParent(cameraTransform.parent, false);
            _recoilPivot.localPosition = cameraTransform.localPosition;
            _recoilPivot.localRotation = cameraTransform.localRotation;
            cameraTransform.SetParent(_recoilPivot, false);
            cameraTransform.localPosition = Vector3.zero;
            cameraTransform.localRotation = Quaternion.identity;
        }
    }

    private void Update()
    {
        if (!LocalOwner)
        {
            if (_hud != null) _hud.gameObject.SetActive(false);
            _recoil = 0f;
            _flashUntil = 0f;
            if (_recoilPivot != null) _recoilPivot.localRotation = Quaternion.identity;
            return;
        }
        if (_hud == null) BuildHud();
        if (_hud == null) return;
        _hud.gameObject.SetActive(true);
        _hpText.text = _health != null ? $"HP: {_health.CurrentHealth} / {_health.maxHp}" : "";
        if (_health != null && !_health.CanAct)
        {
            _recoil = 0f;
            _flashUntil = 0f;
            if (_recoilPivot != null) _recoilPivot.localRotation = Quaternion.identity;
            int remaining = Mathf.CeilToInt((float)_health.RespawnSecondsRemaining);
            _hpText.text = remaining > 0 ? $"Dead - respawning in {remaining}s" : "Dead - waiting for spawn";
        }
        _hitFlash.SetActive(Time.unscaledTime < _flashUntil);
    }

    // Called after Starter Assets mouse look, before the shooter samples its camera ray.
    public void PrepareAim(float pitchLimit)
    {
        if (!LocalOwner || _recoilPivot == null) return;
        _recoil = Mathf.MoveTowards(_recoil, 0f, recoilReturnSpeed * Time.unscaledDeltaTime);
        float basePitch = Mathf.DeltaAngle(0f, _recoilPivot.parent.localEulerAngles.x);
        float displayedPitch = Mathf.Clamp(basePitch - _recoil, -pitchLimit, pitchLimit);
        _recoilPivot.localRotation = Quaternion.Euler(displayedPitch - basePitch, 0f, 0f);
    }

    public void AcceptedShot(bool damagedPlayer)
    {
        if (!LocalOwner || (_health != null && !_health.CanAct)) return;
        _recoil = Mathf.Min(_recoil + recoilKick, 6f);
        if (damagedPlayer) _flashUntil = Time.unscaledTime + hitFlashDuration;
    }

    public void ShowImpact(bool blood, Vector3 point, Vector3 normal)
    {
        GameObject prefab = blood ? bloodImpact : solidImpact;
        if (prefab == null) return;
        while (_effects.Count > 0 && _effects.Peek() == null) _effects.Dequeue();
        while (_effects.Count >= Mathf.Max(1, maxImpacts)) Destroy(_effects.Dequeue());
        GameObject effect = Instantiate(prefab, point + normal * 0.015f,
            Quaternion.LookRotation(blood ? normal : -normal));
        _effects.Enqueue(effect);
        Destroy(effect, blood ? 2f : Mathf.Max(0.1f, impactLifetime));
    }

    private void BuildHud()
    {
        if (playerCanvas == null) return;
        _hud = new GameObject("CombatHUD", typeof(RectTransform)).GetComponent<RectTransform>();
        _hud.SetParent(playerCanvas.transform, false);
        _hud.anchorMin = Vector2.zero;
        _hud.anchorMax = Vector2.one;
        _hud.offsetMin = _hud.offsetMax = Vector2.zero;
        AddLine("CrosshairHorizontal", new Vector2(12f, 2f), 0f, Color.white, _hud);
        AddLine("CrosshairVertical", new Vector2(2f, 12f), 0f, Color.white, _hud);
        RectTransform flash = new GameObject("ConfirmedHit", typeof(RectTransform)).GetComponent<RectTransform>();
        flash.SetParent(_hud, false);
        flash.anchorMin = flash.anchorMax = new Vector2(0.5f, 0.5f);
        flash.anchoredPosition = Vector2.zero;
        AddLine("DiagonalA", new Vector2(22f, 2f), 45f, Color.yellow, flash);
        AddLine("DiagonalB", new Vector2(22f, 2f), -45f, Color.yellow, flash);
        _hitFlash = flash.gameObject;
        _hitFlash.SetActive(false);
        TMP_Text template = playerCanvas.GetComponentInChildren<TMP_Text>(true);
        _hpText = new GameObject("HealthText", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        _hpText.transform.SetParent(_hud, false);
        if (template != null) _hpText.font = template.font;
        _hpText.fontSize = 28f;
        _hpText.color = Color.white;
        _hpText.raycastTarget = false;
        _hpText.alignment = TextAlignmentOptions.BottomLeft;
        RectTransform rect = _hpText.rectTransform;
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(24f, 24f);
        rect.sizeDelta = new Vector2(420f, 45f);
    }

    private static void AddLine(string name, Vector2 size, float angle, Color color, Transform parent)
    {
        Image line = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        line.transform.SetParent(parent, false);
        line.color = color;
        line.raycastTarget = false;
        line.rectTransform.anchorMin = line.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        line.rectTransform.anchoredPosition = Vector2.zero;
        line.rectTransform.sizeDelta = size;
        line.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void OnDestroy()
    {
        if (_hud != null) Destroy(_hud.gameObject);
        foreach (GameObject effect in _effects) if (effect != null) Destroy(effect);
    }
}
