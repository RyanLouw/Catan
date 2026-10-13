#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Catan.Client.Editor
{
    public static class PrototypeMenu
    {
        [MenuItem("Catan/Create prototype scene")]
        public static void CreateScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            const string path = "Assets/Catan/Scenes/Prototype.unity";
            if (File.Exists(path)) { EditorSceneManager.OpenScene(path); return; }
            Directory.CreateDirectory("Assets/Catan/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Catan offline prototype").AddComponent<CatanPrototype>();
            EditorSceneManager.SaveScene(scene, path);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(path, true) };
            AssetDatabase.Refresh();
        }
    }
}
#endif
