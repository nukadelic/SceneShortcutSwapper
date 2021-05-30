using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

namespace UnityEditor
{
    public class EditorSceneSwapper : EditorWindow
    {
        [InitializeOnLoadMethod]
        static void HookShortcuts()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Unhook;
            HookGlobalEvent(true);
        }

        static void Unhook()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= Unhook;
            HookGlobalEvent(false);
        }
        static void HookGlobalEvent(bool add)
        {
            var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
            // thanks to : https://github.com/pjc0247/UnityHack
            var info = typeof(EditorApplication).GetField("globalEventHandler", flags);
            var value = (EditorApplication.CallbackFunction)info.GetValue(null);
            if (add) value += HandleUnityEvent;
            else value -= HandleUnityEvent;
            info.SetValue(null, value);
        }

        static void HandleUnityEvent()
        {
            var e = Event.current;

            if (e == null || !e.isKey) return;
            if (e.type != EventType.KeyDown) return;
            if (!e.alt) return;

            var code = (int)e.keyCode;
            if (code < 48 || code > 57) return;

            var key_index = code == 48 ? 0 : code - 49;
            var settings = EditorSceneSwapperSettings.instance;
            if (key_index > settings.m_Items.Count - 1) return;

            EditorSceneManager.OpenScene(settings.m_Items[key_index].GetPath());
        }

        [MenuItem("Tools/Scene Swapper")]
        public static void ShowWindow() => GetWindow<EditorSceneSwapper>
            ("Tools / Scene Swapper").minSize = new Vector2(400, 200);

        string[] GetBuildScenes()
        {
            List<string> scenes = new List<string>();

            int overflow = 100, count = 0;

            while (overflow-- > 0)
            {
                try
                {
                    var path = SceneUtility.GetScenePathByBuildIndex(count++);
                    if (string.IsNullOrEmpty(path)) break;
                    //int start = path.LastIndexOf("/");
                    //Debug.Log(start + " " + path.Length + " " + path);
                    //string name = path.Substring(start, path.Length - start);
                    scenes.Add(path);
                }
                catch (System.Exception e) { e.ToString(); break; }
            }

            return scenes.ToArray();
        }

        Vector2 scroll = Vector2.zero;

        string[] scenes;

        void OnGUI()
        {
            bool save = false;

            if (scenes == null) scenes = GetBuildScenes();

            var settings = EditorSceneSwapperSettings.instance;

            GUILayout.Space(10);

            EditorGUILayout.HelpBox("Assign shortcut to open a scene build index or scene asset", MessageType.Info);

            GUILayout.Space(10);

            if (settings.m_Items != null && settings.m_Items.Count > 0)
            {
                using (var scope = new GUILayout.ScrollViewScope(scroll))
                {
                    scroll = scope.scrollPosition;

                    int index = 0; int remove = -1;

                    foreach (var item in settings.m_Items)
                    {
                        EditorGUI.BeginChangeCheck();

                        using (new GUILayout.HorizontalScope())
                        {
                            GUILayout.Space(10);

                            GUILayout.Label("Alt+" + (index + 1), GUILayout.Width(55));

                            item.useBuildIndex = GUILayout.Toggle(item.useBuildIndex, item.useBuildIndex ? "Build Index" : "Scene");

                            GUILayout.Space(10);

                            if (item.useBuildIndex)
                            {
                                if (item.buildIndex > scenes.Length - 1) item.buildIndex = scenes.Length - 1;

                                item.buildIndex = EditorGUILayout.IntSlider(item.buildIndex, 0, scenes.Length - 1);
                            }
                            else
                            {
                                var value = EditorGUILayout.ObjectField(item.sceneAssetReference, typeof(SceneAsset), true);

                                item.sceneAssetReference = (SceneAsset)value;
                            }

                            GUILayout.Space(5);

                            if (GUILayout.Button("[ x ]", GUILayout.Width(35)))
                            {
                                remove = index;
                            }

                            GUILayout.Space(10);
                        }

                        ++index;

                        if (EditorGUI.EndChangeCheck()) save = true;
                    }

                    if (remove > -1)
                    {
                        settings.m_Items.RemoveAt(remove);

                        save = true;
                    }
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No items, Press [ + ] to create", MessageType.Info);

                GUILayout.FlexibleSpace();
            }
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("[ + ]", GUILayout.Width(35)))
                {
                    if (settings.m_Items == null) settings.m_Items = new List<EditorSceneSwapperSettings.Item>();

                    settings.m_Items.Add(new EditorSceneSwapperSettings.Item { useBuildIndex = true, buildIndex = settings.m_Items.Count });
                    save = true;
                }

                GUILayout.Space(10);
            }

            GUILayout.Space(10);

            if (save) settings.Save();
        }
    }

    [FilePath("ProjectSettings/EditorSceneSwapperSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    class EditorSceneSwapperSettings : ScriptableSingleton<EditorSceneSwapperSettings>
    {
        [System.Serializable]
        public class Item
        {
            public bool useBuildIndex = false;
            public int buildIndex = 0;
            public SceneAsset sceneAssetReference;

            public string GetPath()
            {
                if (useBuildIndex) return SceneUtility.GetScenePathByBuildIndex(buildIndex);

                return AssetDatabase.GetAssetPath(sceneAssetReference);
            }
        }

        [SerializeField]
        public List<Item> m_Items;

        public void Save() => Save(true);
    }
}
