using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Adapts Thanh's rebinding/menu prototype without its global Time.timeScale pause.
public sealed class LocalBindingsMenu : MonoBehaviour
{
    private static LocalBindingsMenu _open;
    private InputActionAsset _asset;
    private bool _ownsAsset;
    private InputActionRebindingExtensions.RebindingOperation _operation;
    private readonly List<Button> _rows = new();
    private readonly List<System.Action> _refresh = new();
    public static bool Rebinding => _open != null && _open._operation != null;
    public static void CloseFor(InputActionAsset asset) { if (_open != null && _open._asset == asset) Destroy(_open.gameObject); }

    public static void Install(Scene scene)
    {
        if (scene.name != "MainMenu") return;
        foreach (var root in scene.GetRootGameObjects())
            foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
                if (canvas.name == "SettingsMenuCanvas")
                {
                    var button = IntegrationUi.Button(canvas.transform, "Controls / Rebind", Vector2.zero, () => Show(null));
                    var rect = (RectTransform)button.transform;
                    rect.anchorMin = rect.anchorMax = new Vector2(1, 0);
                    rect.anchoredPosition = new Vector2(-180, 45);
                }
    }
    public static void Show(InputActionAsset asset)
    {
        if (_open != null) return;
        var go = IntegrationUi.Canvas("Controls", 130);
        _open = go.AddComponent<LocalBindingsMenu>();
        _open._ownsAsset = asset == null;
        _open._asset = asset != null ? asset : LocalInputBindings.Create();
        _open.Build();
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
    }
    private void Build()
    {
        var panel = IntegrationUi.Panel(transform, "Bindings", new Vector2(850, 660));
        IntegrationUi.Text(panel, "Keyboard / mouse controls — select a binding", new Vector2(0, 300), new Vector2(800, 40), 24);
        int row = 0;
        foreach (InputAction action in _asset.FindActionMap("Player").actions)
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var binding = action.bindings[i];
                if (binding.isComposite || action.name == "Look" || !binding.groups.Contains("KeyboardMouse")) continue;
                int index = i;
                string name = action.name + (binding.isPartOfComposite ? " " + binding.name : "");
                int column = row / 9, line = row % 9; row++;
                var button = IntegrationUi.Button(panel, name, new Vector2(column == 0 ? -205 : 205, 235 - line * 50),
                    () => Rebind(action, index), new Vector2(390, 40));
                _rows.Add(button);
                var label = button.GetComponentInChildren<TMPro.TMP_Text>();
                _refresh.Add(() => label.text = name + ": " + action.GetBindingDisplayString(index));
            }
        IntegrationUi.Button(panel, "Reset bindings", new Vector2(-205, -265), () =>
        { Cancel(); _asset.RemoveAllBindingOverrides(); LocalInputBindings.Save(_asset); Refresh(); }, new Vector2(390, 40));
        IntegrationUi.Button(panel, "Close / cancel", new Vector2(205, -265), () => Destroy(gameObject), new Vector2(390, 40));
        Refresh();
    }
    private void Rebind(InputAction action, int index)
    {
        if (_operation != null) return;
        _asset.Disable();
        foreach (var row in _rows) row.interactable = false;
        _operation = action.PerformInteractiveRebinding(index).WithExpectedControlType("Button")
            .WithControlsExcluding("<Pointer>/position").WithControlsExcluding("<Pointer>/delta")
            .WithControlsExcluding("<Gamepad>").WithCancelingThrough("<Keyboard>/escape").WithTimeout(10)
            .OnCancel(_ => Completed(false)).OnComplete(_ => Completed(true));
        _operation.Start();
    }
    private void Completed(bool save)
    {
        _operation?.Dispose(); _operation = null;
        if (save) LocalInputBindings.Save(_asset);
        foreach (var row in _rows) row.interactable = true;
        Refresh();
    }
    private void Cancel() { _operation?.Cancel(); }
    private void Refresh() { foreach (var refresh in _refresh) refresh(); }
    private void OnDestroy()
    {
        Cancel(); _operation?.Dispose(); _operation = null;
        if (_ownsAsset && _asset != null) Destroy(_asset);
        if (_open == this) _open = null;
    }
}
