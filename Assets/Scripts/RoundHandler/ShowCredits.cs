using FishNet.Object;
using TMPro;
using UnityEngine;

public class ShowCredits : MonoBehaviour
{
    public GameObject roundHandler;
    private NetworkObject _player;
    private TextMeshProUGUI _displayText;
    private TrackPlayerCurrency _currencyTracker;
    private bool _requested;

    private void Awake()
    {
        _player = GetComponentInParent<NetworkObject>();
        _displayText = GetComponent<TextMeshProUGUI>();
    }

    private void Update()
    {
        if (_displayText == null) return;
        if (_player == null || !_player.IsClientInitialized || !_player.IsOwner)
        {
            _displayText.text = string.Empty;
            _requested = false;
            return;
        }

        if (_currencyTracker == null)
        {
            _requested = false;
            _currencyTracker = roundHandler != null
                ? roundHandler.GetComponent<TrackPlayerCurrency>() : TrackPlayerCurrency.instance;
        }
        if (_currencyTracker == null)
        {
            _displayText.text = string.Empty;
            return;
        }

        if (!_requested) _requested = _currencyTracker.RequestLocalBalance();
        _displayText.text = _currencyTracker.HasLocalBalance ? "$" + _currencyTracker.LocalBalance : string.Empty;
    }
}
