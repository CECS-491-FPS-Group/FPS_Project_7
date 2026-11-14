using UnityEngine;
using UnityEngine.InputSystem;

// Thanh's owner controls/rebinding intent, adapted without modifying vendor assets.
public static class LocalInputBindings
{
    public const string PreferenceKey = "input.bindings.v1";
    public static InputActionAsset Create()
    {
        var template = Resources.Load<InputActionAsset>("GameplayInput");
        if (template == null) throw new System.InvalidOperationException("Missing GameplayInput resource.");
        var instance = Object.Instantiate(template);
        string json = PlayerPrefs.GetString(PreferenceKey, "");
        if (json.Length <= 32768 && json.Length > 0)
            try { instance.LoadBindingOverridesFromJson(json); }
            catch (System.Exception) { Debug.LogWarning("[Input] Invalid saved bindings; using defaults."); }
        return instance;
    }
    public static void Save(InputActionAsset asset)
    {
        PlayerPrefs.SetString(PreferenceKey, asset.SaveBindingOverridesAsJson());
        PlayerPrefs.Save();
    }
}
