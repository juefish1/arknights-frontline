using UnityEngine;
using UnityEngine.InputSystem;

namespace ArknightsFrontline.Input
{
    public static class InputBindingStore
    {
        private const string BindingOverridesKey = "af.input.bindings.v1";

        public static void Save(InputActionAsset asset)
        {
            PlayerPrefs.SetString(BindingOverridesKey, asset.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
        }

        public static void Load(InputActionAsset asset)
        {
            string overridesJson = PlayerPrefs.GetString(BindingOverridesKey, string.Empty);
            if (!string.IsNullOrEmpty(overridesJson))
            {
                asset.LoadBindingOverridesFromJson(overridesJson);
            }
        }
    }
}
