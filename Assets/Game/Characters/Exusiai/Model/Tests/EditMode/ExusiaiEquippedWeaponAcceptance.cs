using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Actual delivered prefab and scene, with no primitive or mocked weapon.
public static class ExusiaiEquippedWeaponAcceptance
{
    private const string Model = "Assets/Game/Characters/Exusiai/Model";
    private const string Weapon = "Assets/Game/Weapons/ExusiaiVector";
    private static void Require(bool value, string reason)
    { if (!value) throw new InvalidOperationException(reason); }

    private static void Check(GameObject actor)
    {
        var animator = actor.GetComponentInChildren<Animator>();
        var mount = actor.GetComponentsInChildren<Transform>(true).Single(t => t.name == "WeaponMount");
        Require(mount.parent == animator.GetBoneTransform(HumanBodyBones.LeftHand), "Primary hand changed");
        var expected = AssetDatabase.LoadAssetAtPath<GameObject>(Weapon + "/Models/Exusiai_Vector.fbx")
            .GetComponentInChildren<MeshFilter>().sharedMesh;
        var renderers = mount.GetComponentsInChildren<MeshRenderer>(true);
        Require(renderers.Length == 1 && renderers[0].GetComponent<MeshFilter>().sharedMesh == expected,
            "Approved Vector mesh is not equipped");
        Require(renderers[0].sharedMaterial == AssetDatabase.LoadAssetAtPath<Material>(Weapon + "/Materials/Exusiai_Vector.mat"),
            "Equipped weapon material is not the shared approved material");
        var weapon = mount.Find("Exusiai_Vector");
        Require(weapon && weapon.localScale == Vector3.one, "Weapon instance missing or rescaled");
        var original = PrefabUtility.GetCorrespondingObjectFromOriginalSource(weapon.gameObject);
        Require(original && AssetDatabase.GetAssetPath(original) == Weapon + "/Prefabs/Exusiai_Vector.prefab",
            "Weapon lost its nested prefab connection");
        var primary = weapon.Find("PrimaryGrip");
        var support = weapon.Find("SupportGrip");
        var muzzle = weapon.Find("Muzzle");
        Require(primary && support && muzzle, "Weapon grip/muzzle points missing");
        Require(Vector3.Distance(primary.position, mount.parent.position) < 0.06f, "Primary wrist separated from grip");
        Require(Mathf.Abs(support.localPosition.z - 0.11f) < 0.001f && Mathf.Abs(muzzle.localPosition.z - 0.3681f) < 0.001f,
            "Weapon markers still describe the old prop");
        Require(!mount.GetComponentsInChildren<Transform>(true).Any(t => t.name.StartsWith("TestWeapon_")),
            "Obsolete primitive weapon survived");
        Require(mount.GetComponentsInChildren<Collider>(true).Length == 0, "Unexpected gameplay collider");
    }

    public static void Run()
    {
        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Model + "/Prefabs/Exusiai_Game_Humanoid.prefab");
            Require(prefab, "Formal prefab missing");
            var actor = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Check(actor);
            UnityEngine.Object.DestroyImmediate(actor);
            Require(!AssetDatabase.LoadAssetAtPath<Material>(Model + "/Animations/Exusiai_TestWeapon.mat"),
                "Obsolete prop material still exists");
            EditorSceneManager.OpenScene(Model + "/Scenes/Preview_Game_Humanoid.unity");
            actor = UnityEngine.Object.FindFirstObjectByType<ExusiaiPresentation>().gameObject;
            Check(actor);
            string report = "{\"passed\":true,\"shared_vector_mesh\":true,\"shared_vector_material\":true," +
                "\"nested_weapon_prefab\":true,\"left_primary_hand\":true,\"obsolete_prop_removed\":true,\"scene_verified\":true}";
            File.WriteAllText("equipped-weapon-acceptance.json", report);
            Debug.Log("EXUSIAI_EQUIPPED_WEAPON_OK " + report);
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
