using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.Serialization;

namespace Moyassy
{
	public enum ExBrushTargetType { Tilemap, ForPrefabBrush, ManualObjects }
	public enum ExBrushTargetConditionMode { RejectByLabels, AcceptByLabels }

	public class ExBrushTarget : MonoBehaviour
	{
		public ExBrushTargetType type = ExBrushTargetType.Tilemap;
		
		// falseの時の仕様が固まりきってないので一旦常時trueにした
		//public bool involved = true;
		public bool involved { get { return true; } }
		
		public ExBrushTargetConditionMode conditionMode = ExBrushTargetConditionMode.RejectByLabels;
		
		public List<string> labels;

		public bool TilemapTarget
		{
			get
			{
				return type == ExBrushTargetType.Tilemap;
			}
		}

		public bool ObjectsTarget
		{
			get
			{
				return type == ExBrushTargetType.ForPrefabBrush || type == ExBrushTargetType.ManualObjects;
			}
		}

#if UNITY_EDITOR

		/// <summary>
		/// タイルが適合するかどうか
		/// </summary>
		public bool IsAccepted(TileBase tile)
		{
			if (type != ExBrushTargetType.Tilemap) return false;

			return IsAcceptedCore(tile);
		}

		/// <summary>
		/// プレハブが適合するかどうか
		/// </summary>
		public bool IsAccepted(GameObject prefab)
		{
			if (type != ExBrushTargetType.ForPrefabBrush) return false;

			return IsAcceptedCore(prefab);

		}
		bool IsAcceptedCore(Object o)
		{
			bool ret = false;

			// Objectのラベル一覧を取得する
			List<string> assetLabels = new List<string>(UnityEditor.AssetDatabase.GetLabels(o));

			// Acctept で判定
			if (conditionMode == ExBrushTargetConditionMode.AcceptByLabels)
			{
				ret = false;
				foreach (string label in assetLabels)
				{
					if (labels.IndexOf(label) >= 0) { ret = true; break; }
				}
			}

			// Reject で判定
			else if (conditionMode == ExBrushTargetConditionMode.RejectByLabels)
			{
				ret = true;
				foreach (string label in assetLabels)
				{
					if (labels.IndexOf(label) >= 0) { ret = false; break; }
				}
			}

			return ret;
		}

#endif

	}

}