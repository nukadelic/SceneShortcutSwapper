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
            if( Application.isPlaying ) return;

            var e = Event.current;

            if (e == null || !e.isKey) return;
            if (e.type != EventType.KeyDown) return;
            if (!e.alt) return;

            if( e.keyCode == KeyCode.BackQuote )
            {
                if( HasOpenInstances<EditorSceneSwapper>() ) 
                    GetWindow<EditorSceneSwapper>().Close();
                else ShowWindow();
                return;
            }

            var code = (int)e.keyCode;
            if (code < 48 || code > 57) return;

            var key_index = code == 48 ? 0 : code - 49;
            var settings = EditorSceneSwapperSettings.instance;
            if (key_index > settings.m_Items.Count - 1) return;

            //PrefabUtility , StageUtility , PreviewSceneStage
            //bool match = ((Experimental.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage()?.scene ?? null) == EditorSceneManager.GetActiveScene());
            //Debug.Log( "Active " + Experimental.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() + " " + match );
            //var filthy = Experimental.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage()?.scene.isDirty ?? false;

            if ( EditorSceneManager.GetActiveScene().isDirty ) 
            {
                if( settings.autoSave )
                {
                    EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                    Debug.Log("Scene autosaved, change in: " + Title );
                }
                else
                {
                    EditorUtility.DisplayDialog( "Active scene is dirty", "Save your changes before swapping scene or enable autosaving in " + Title, "close" );
                    return;
                }
            }

            if( settings.m_Items[ key_index ].itemType == EditorSceneSwapperSettings.ItemType.Prefab )
            {
                AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath( settings.m_Items[key_index].prefabPath, typeof( GameObject ) ));
            }
            else
            {
                EditorSceneManager.OpenScene(settings.m_Items[key_index].GetPath());
            }

        }

        static string Title = "Tools / Scene Swapper";

        [MenuItem("Tools/Scene Swapper")]
        public static void ShowWindow() => GetWindow<EditorSceneSwapper>( Title ).minSize = new Vector2(400, 200);
        
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
            GUILayout.Space(10);

            bool dirty = false;

            if (scenes == null) scenes = GetBuildScenes();

            var settings = EditorSceneSwapperSettings.instance;

            var autoSave = EditorGUILayout.ToggleLeft(" Auto save scene changes on hot swap", settings.autoSave);
            if (autoSave != settings.autoSave)
            {
                settings.autoSave = autoSave;

                settings.hintDismiss = false;

                dirty = true;
            }
            GUILayout.Space(10);

            if ( ! settings.hintDismiss )
            {
                using( new GUILayout.HorizontalScope() )
                {
                    EditorGUILayout.HelpBox("Assign shortcut to open a scene build index or scene asset\nAlt+` to open this window", MessageType.Info);

                    if( GUILayout.Button("x", GUILayout.Width( 22 ) , GUILayout.Height( 36 ) ) ) settings.hintDismiss = true;
                }
            }
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

                            item.itemType = (EditorSceneSwapperSettings.ItemType) EditorGUILayout.EnumPopup(item.itemType);

                            //item.useBuildIndex = GUILayout.Toggle(item.useBuildIndex, item.useBuildIndex ? "Build Index" : "Scene");

                            GUILayout.Space(10);

                            //if (item.useBuildIndex)
                            if(item.itemType == EditorSceneSwapperSettings.ItemType.BuildIndex)
                            {
                                if (item.buildIndex > scenes.Length - 1) item.buildIndex = scenes.Length - 1;

                                item.buildIndex = EditorGUILayout.IntSlider(item.buildIndex, 0, scenes.Length - 1);
                            }
                            else if(item.itemType == EditorSceneSwapperSettings.ItemType.SceneAsset)
                            {
                                var value = EditorGUILayout.ObjectField(item.sceneAssetReference, typeof(SceneAsset), false );

                                item.sceneAssetReference = (SceneAsset)value;
                            }
                            else if(item.itemType == EditorSceneSwapperSettings.ItemType.Prefab)
                            {
                                var value = EditorGUILayout.ObjectField( item.objectAssetReference, typeof( GameObject ), false );

                                var path = AssetDatabase.GetAssetPath( value );

                                item.objectAssetReference = path == null ? null : ( GameObject ) value;
                                item.prefabPath = path;
                            }

                            GUILayout.Space(5);

                            if (GUILayout.Button("[ x ]", GUILayout.Width(35)))
                            {
                                remove = index;
                            }

                            GUILayout.Space(10);
                        }

                        ++index;

                        if (EditorGUI.EndChangeCheck()) dirty = true;
                    }

                    if (remove > -1)
                    {
                        settings.m_Items.RemoveAt(remove);

                        dirty = true;
                    }
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No items, Press [ + ] to create", MessageType.Info);

                GUILayout.FlexibleSpace();
            }
            if( settings.m_Items != null && settings.m_Items.Count > 9 )
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("Can't add more items.");
                GUILayout.Space(10);
            }
            else using (new GUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("[ + ]", GUILayout.Width(35)))
                {
                    if (settings.m_Items == null) settings.m_Items = new List<EditorSceneSwapperSettings.Item>();

                    settings.m_Items.Add(new EditorSceneSwapperSettings.Item 
                    { 
                        itemType = EditorSceneSwapperSettings.ItemType.BuildIndex, 
                        buildIndex = settings.m_Items.Count 
                    });
                    dirty = true;
                }

                GUILayout.Space(10);
            }

            GUILayout.Space(10);

            if (dirty) settings.Save();
        }
    }

    [FilePath("ProjectSettings/EditorSceneSwapperSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    class EditorSceneSwapperSettings : ScriptableSingleton<EditorSceneSwapperSettings>
    {
        public enum ItemType { BuildIndex, SceneAsset, Prefab }

        [System.Serializable]
        public class Item
        {
            public ItemType itemType;
            public int buildIndex = 0;
            public SceneAsset sceneAssetReference;
            public GameObject objectAssetReference;
            public string prefabPath;

            public string GetPath()
            {
                if ( itemType == ItemType.BuildIndex ) return SceneUtility.GetScenePathByBuildIndex( buildIndex );

                if( itemType == ItemType.Prefab ) return prefabPath;

                return AssetDatabase.GetAssetPath( sceneAssetReference );
            }
        }

        public bool autoSave = false;
        public bool hintDismiss = false;

        [SerializeField]
        public List<Item> m_Items;

        public void Save() => Save(true);
    }
}
