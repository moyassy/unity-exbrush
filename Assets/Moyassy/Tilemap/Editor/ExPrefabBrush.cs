// 1. original code:
//
// https://github.com/Unity-Technologies/2d-extras/blob/master/Assets/Tilemap/Brushes/Prefab%20Brush/Scripts/Editor/PrefabBrush.cs
// The MIT License (MIT)
// Copyright(c) 2016 Unity Technologies

// 2. modified code:
//
// The MIT License (MIT)
// Copyright(c) 2019 moyassy
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of 
// this software and associated documentation files (the "Software"), to deal in
// the Software without restriction, including without limitation the rights to
// use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
// the Software, and to permit persons to whom the Software is furnished to do so,
// subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
// FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
// COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
// IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
// CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;
using Moyassy;

namespace UnityEditor
{
	[CreateAssetMenu(menuName = "Tilemap Brush/Ex Prefab Brush")]
	[CustomGridBrush(false, true, false, "Ex Prefab Brush")]
	public class ExPrefabBrush : GridBrushBase
	{
		/// <summary>
		/// PrefabBrush先生 秘伝のパレット判定方法
		/// </summary>
		public static bool IsPalette(GameObject brushTarget) { return brushTarget.layer == 31; }

		private const float k_PerlinOffset = 100000f;
		public GameObject[] m_Prefabs;
		public float m_PerlinScale = 0.5f;
		public int m_Z;

		public override void Paint(GridLayout grid, GameObject brushTarget, Vector3Int position)
		{
			// Do not allow editing palettes
			if (IsPalette(brushTarget))
				return;

			// 追加箇所１：重なっていたら描画しないようにする
			{
				Transform overlapped = GetObjectInCell(grid, brushTarget.transform, new Vector3Int(position.x, position.y, m_Z));
				if (overlapped != null)
					return;
			}

			int index = Mathf.Clamp(Mathf.FloorToInt(GetPerlinValue(position, m_PerlinScale, k_PerlinOffset)*m_Prefabs.Length), 0, m_Prefabs.Length - 1);
			GameObject prefab = m_Prefabs[index];
			GameObject instance = (GameObject) PrefabUtility.InstantiatePrefab(prefab);
			Undo.RegisterCreatedObjectUndo((Object)instance, "Paint Prefabs");
			if (instance != null)
			{
				instance.transform.SetParent(brushTarget.transform);
				instance.transform.position = grid.LocalToWorld(grid.CellToLocalInterpolated(new Vector3Int(position.x, position.y, m_Z) + new Vector3(.5f, .5f, .5f)));
			}
		}
		
		public override void Erase(GridLayout grid, GameObject brushTarget, Vector3Int position)
		{
			// Do not allow editing palettes
			if (brushTarget.layer == 31)
				return;

			Transform erased = GetObjectInCell(grid, brushTarget.transform, new Vector3Int(position.x, position.y, m_Z));
			if (erased != null)
				Undo.DestroyObjectImmediate(erased.gameObject);
		}
		
		private static Transform GetObjectInCell(GridLayout grid, Transform parent, Vector3Int position)
		{
			int childCount = parent.childCount;
			Vector3 min = grid.LocalToWorld(grid.CellToLocalInterpolated(position));
			Vector3 max = grid.LocalToWorld(grid.CellToLocalInterpolated(position + Vector3Int.one));
			Bounds bounds = new Bounds((max + min)*.5f, max - min);

			for (int i = 0; i < childCount; i++)
			{
				Transform child = parent.GetChild(i);
				if (bounds.Contains(child.position))
					return child;
			}
			return null;
		}

		private static float GetPerlinValue(Vector3Int position, float scale, float offset)
		{
			return Mathf.PerlinNoise((position.x + offset)*scale, (position.y + offset)*scale);
		}
	}

	[CustomEditor(typeof(ExPrefabBrush))]
	public class ExPrefabBrushEditor : GridBrushEditorBase
	{
		private ExPrefabBrush prefabBrush { get { return target as ExPrefabBrush; } }

		private SerializedProperty m_Prefabs;
		private SerializedObject m_SerializedObject;

		protected void OnEnable()
		{
			m_SerializedObject = new SerializedObject(target);
			m_Prefabs = m_SerializedObject.FindProperty("m_Prefabs");
		}

		public override void OnPaintInspectorGUI()
		{
			m_SerializedObject.UpdateIfRequiredOrScript();
			prefabBrush.m_PerlinScale = EditorGUILayout.Slider("Perlin Scale", prefabBrush.m_PerlinScale, 0.001f, 0.999f);
			prefabBrush.m_Z = EditorGUILayout.IntField("Position Z", prefabBrush.m_Z);

			EditorGUILayout.PropertyField(m_Prefabs, true);
			m_SerializedObject.ApplyModifiedPropertiesWithoutUndo();
		}
		
		// 追加箇所２：描画先を適合するExBrushTargetに制限する
		public override GameObject[] validTargets
		{
			get
			{
				List<GameObject> ret = new List<GameObject>();

				ExBrushTarget[] brushTargets = FindObjectsOfType<ExBrushTarget>();
				foreach (ExBrushTarget brushTarget in brushTargets)
				{
					if (brushTarget.type == ExBrushTargetType.ForPrefabBrush)
					{
						if (prefabBrush.m_Prefabs.Length > 0)
						{
							// ※プレハブはm_Prefabsの0番目のみを見て適合を判断する仕様
							GameObject prefab = prefabBrush.m_Prefabs[0];
							if (brushTarget.IsAccepted(prefab))
							{
								ret.Add(brushTarget.gameObject);
							}
						}
					}
				}

				return ret.ToArray();
			}
		}
	}
}
