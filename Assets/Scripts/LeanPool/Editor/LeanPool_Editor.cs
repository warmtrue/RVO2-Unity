using UnityEngine;
using UnityEditor;

namespace Lean
{
	[CustomEditor(typeof(LeanPool))]
	public class LeanPool_Editor : Editor
	{
		[MenuItem("GameObject/Lean/Pool", false, 1)]
		public static void CreateLocalization()
		{
			var gameObject = new GameObject(typeof(LeanPool).Name);
			
			UnityEditor.Undo.RegisterCreatedObjectUndo(gameObject, "Create Pool");
			
			gameObject.AddComponent<LeanPool>();
			
			Selection.activeGameObject = gameObject;
		}
		
		// Draw the whole inspector
		public override void OnInspectorGUI()
		{
			var pool = (LeanPool)target;
			
			EditorGUI.BeginChangeCheck();
			{
				EditorGUILayout.Separator();
				
				EditorGUILayout.PropertyField(serializedObject.FindProperty("Prefab"));
				
				EditorGUILayout.PropertyField(serializedObject.FindProperty("Preload"));
				
				EditorGUILayout.PropertyField(serializedObject.FindProperty("Capacity"));
				
				EditorGUILayout.PropertyField(serializedObject.FindProperty("Notification"));
				
				EditorGUILayout.Separator();
				
				EditorGUI.BeginDisabledGroup(true);
				{
					EditorGUILayout.IntField("Total", pool.Total);
					
					EditorGUILayout.IntField("Cached", pool.Cached);
				}
				EditorGUI.EndDisabledGroup();
			}
			if (EditorGUI.EndChangeCheck() == true)
			{
				EditorUtility.SetDirty(target);
			}
			
			serializedObject.ApplyModifiedProperties();
		}
	}
}