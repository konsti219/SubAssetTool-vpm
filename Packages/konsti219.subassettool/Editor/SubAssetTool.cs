using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

using System;
using System.Linq;
using System.IO;

#if UNITY_EDITOR
public class SubAssetTool : EditorWindow, IHasCustomMenu
{
    private string selectedAsset = "";
    private List<UnityEngine.Object> objects = new List<UnityEngine.Object>();
    private List<UnityEngine.Object> removedObjects = new List<UnityEngine.Object>();

    Vector2 MainScroll = Vector2.zero;
    [System.NonSerialized]
    private bool locked = false;
    public Dictionary<UnityEngine.Object, bool> foldoutState = new Dictionary<UnityEngine.Object, bool>();


    [MenuItem("Window/Sub Asset Tool")]
    static void Init()
    {
        var window = (SubAssetTool)EditorWindow.GetWindow(typeof(SubAssetTool));
        window.Show();
    }

    void OnSelectionChange()
    {
        if (Selection.assetGUIDs.Length != 0)
        {
            loadAssets();
            Repaint();
        }
    }

    public void OnGUI()
    {
        MainScroll = EditorGUILayout.BeginScrollView(MainScroll);
        EditorGUI.BeginChangeCheck();


        // Main asset selction
        selectedAsset = EditorGUILayout.TextField("Asset", selectedAsset);
        GUILayout.Space(15);

        // Object display list
        var isLastObject = false;

        // main object
        var mainObject = objects.Find(obj => AssetDatabase.IsMainAsset(obj));
        if (mainObject != null)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.ObjectField(mainObject, typeof(UnityEngine.Object), false);
            if (GUILayout.Button("Remove", GUILayout.MaxWidth(60)))
            {
                if (!removedObjects.Contains(mainObject))
                {
                    removedObjects.Add(mainObject);
                }
                isLastObject = objects.Count == 1;
                AssetDatabase.RemoveObjectFromAsset(mainObject);

                if (isLastObject)
                {
                    AssetDatabase.DeleteAsset(selectedAsset);
                    selectedAsset = "";
                }
                loadAssets();
                Repaint();
            }
            EditorGUILayout.EndHorizontal();
        }

        // sub objects/assets
        foreach (var obj in objects)
        {
            if (obj != null && !AssetDatabase.IsMainAsset(obj))
            {
                EditorGUILayout.BeginHorizontal();

                if (!foldoutState.ContainsKey(obj))
                    foldoutState[obj] = false;
                foldoutState[obj] = EditorGUILayout.Foldout(foldoutState[obj], "More", true);


                EditorGUILayout.ObjectField(obj, typeof(UnityEngine.Object), false);

                GUI.enabled = !AssetDatabase.IsMainAsset(obj);
                GUILayout.Label("Hide", GUILayout.MaxWidth(35));
                if (EditorGUILayout.Toggle(obj.hideFlags.HasFlag(HideFlags.HideInHierarchy), GUILayout.MaxWidth(20)))
                {
                    obj.hideFlags |= HideFlags.HideInHierarchy;
                }
                else
                {
                    obj.hideFlags &= ~HideFlags.HideInHierarchy;
                }
                GUI.enabled = true;

                if (GUILayout.Button("Remove", GUILayout.MaxWidth(60)))
                {
                    if (!removedObjects.Contains(obj))
                    {
                        removedObjects.Add(obj);
                    }

                    // is this is not checked "empty" assets can end up being created. Those are just annoying.
                    isLastObject = AssetDatabase.IsMainAsset(obj) && objects.Count == 1;

                    AssetDatabase.RemoveObjectFromAsset(obj);

                    if (isLastObject)
                    {
                        AssetDatabase.DeleteAsset(selectedAsset);
                        selectedAsset = "";
                    }
                    loadAssets();
                }
                EditorGUILayout.EndHorizontal();


                if (foldoutState[obj])
                {
                    EditorGUILayout.BeginHorizontal();

                    if (!AssetDatabase.IsMainAsset(obj))
                        obj.name = EditorGUILayout.TextField("Name", obj.name);

                    GUI.enabled = !AssetDatabase.IsMainAsset(obj);

                    var newPath = Path.GetDirectoryName(selectedAsset).Replace("\\", "/") + "/" + obj.name + fileExtenion(obj);
                    if (AssetDatabase.LoadMainAssetAtPath(newPath) != null)
                        GUI.enabled = false;

                    if (GUILayout.Button("To Main", GUILayout.MaxWidth(60)))
                    {
                        try
                        {
                            // the previous main asset seems to take the name of the new main. fix that
                            var prevMainName = mainObject.name;

                            AssetDatabase.StartAssetEditing();
                            AssetDatabase.SetMainObject(obj, selectedAsset);
                            AssetDatabase.MoveAsset(selectedAsset, newPath);
                            AssetDatabase.SaveAssets();
                            Selection.activeObject = obj;
                            selectedAsset = newPath;

                            mainObject.name = prevMainName;
                            obj.name = Path.GetFileNameWithoutExtension(newPath);
                            loadAssets();
                        }
                        finally
                        {
                            AssetDatabase.StopAssetEditing();
                        }

                        AssetDatabase.Refresh();
                    }
                    if (GUILayout.Button("Extract", GUILayout.MaxWidth(60)))
                    {
                        obj.hideFlags &= ~HideFlags.HideInHierarchy;
                        AssetDatabase.RemoveObjectFromAsset(obj);
                        AssetDatabase.CreateAsset(obj, newPath);
                        AssetDatabase.SetMainObject(obj, newPath);
                        AssetDatabase.SaveAssets();
                        AssetDatabase.Refresh();
                        loadAssets();
                    }
                    GUI.enabled = true;

                    EditorGUILayout.EndHorizontal();
                }
            }
        }
        GUILayout.Space(15);


        // Removed Objects list
        GUILayout.Label("Removed Objects (temp storage)");
        UnityEngine.Object addedObject = null;
        foreach (var obj in removedObjects)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.ObjectField(obj, typeof(UnityEngine.Object), false);

            // these objects should now always be shown
            obj.hideFlags &= ~HideFlags.HideInHierarchy;

            if (GUILayout.Button("Add", GUILayout.MaxWidth(60)))
            {
                AssetDatabase.AddObjectToAsset(obj, selectedAsset);
                addedObject = obj;
            }

            EditorGUILayout.EndHorizontal();
        }
        if (addedObject != null)
        {
            removedObjects.Remove(addedObject);
        }

        GUI.enabled = removedObjects.Count > 0;
        if (GUILayout.Button("Delete Removed Objects"))
        {
            if (EditorUtility.DisplayDialog("Delete Removed Objects?", "Do you really want to delete these Objects? THIS CAN NOT BE UNDONE!", "Delete", "Cancel"))
            {
                removedObjects.Clear();
            }
        }
        GUI.enabled = true;


        // Clean up code
        if (EditorGUI.EndChangeCheck())
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            loadAssets();
        }

        GUILayout.Space(20);
        EditorGUILayout.HelpBox("The Project view might not always update immediatly becasue Unity. Some operations might also produce random Console errors.", MessageType.Info);
        if (GUILayout.Button("Developed by konsti219 - More Tools"))
        {
            Application.OpenURL("https://konsti219.github.io/vcc-tools/");
        }

        EditorGUILayout.EndScrollView();
    }

    // Show the lock button in the header
    private void ShowButton(Rect position)
    {
        EditorGUI.BeginChangeCheck();
        this.locked = GUI.Toggle(position, this.locked, GUIContent.none, "IN LockButton");
        if (EditorGUI.EndChangeCheck())
        {
            loadAssets();
        }
    }

    void IHasCustomMenu.AddItemsToMenu(GenericMenu menu)
    {
        menu.AddItem(new GUIContent("Lock"), this.locked, () =>
        {
            this.locked = !this.locked;
        });
    }

    void loadAssets()
    {
        if (Selection.assetGUIDs.Length != 0 && !locked)
        {
            selectedAsset = AssetDatabase.GUIDToAssetPath(Selection.assetGUIDs[0]);
            objects = AssetDatabase.LoadAllAssetsAtPath(selectedAsset).ToList();
        }

        if (selectedAsset == "")
        {
            objects = new List<UnityEngine.Object>();
        }

        removedObjects = removedObjects.Where(obj => obj != null).ToList();
    }

    string fileExtenion(UnityEngine.Object obj)
    {
        if (obj is Material)
            return ".mat";
        if (obj is Cubemap)
            return ".cubemap";
        // if (obj is EditorSkin)
        //     return ".GUISkin";
        if (obj is AnimationClip)
            return ".anim";
        if (obj is RuntimeAnimatorController)
            return ".controller";

        return ".asset";
    }
}
#endif
