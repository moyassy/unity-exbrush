using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.Serialization;
using Moyassy;

// 自分のnamespaceではダメっぽい
namespace UnityEditor
{
	// [CreateAssetMenu(menuName = "Tilemap Brush/Ex Brush")]

	[CustomGridBrush(true, true, true, "Ex Brush")]
	public class ExBrush : GridBrush
	{
		#region Pick

		// Pickされた時に呼ばれる
		// Paintツールで非Editモート時のパレット上をクリックした時などにも呼ばれる
		public override void Pick(GridLayout gridLayout, GameObject brushTarget, BoundsInt position, Vector3Int pickStart)
		{
			base.Pick(gridLayout, brushTarget, position, pickStart);

			// パレット上ではない
			if (!ExPrefabBrush.IsPalette(brushTarget))
			{
				// メモ：cells.Lengthは0にはならない

				// Pickしたタイルが空だった場合、他のTilemapでも試行する
				if (cells[0].tile == null)
				{
					ExBrushTarget[] ts = FindObjectsOfType<ExBrushTarget>();
					foreach (ExBrushTarget t in ts)
					{
						if (t.TilemapTarget && t.involved)
						{
							GridLayout gl = t.GetComponentInParent<GridLayout>();
							base.Pick(gl, t.gameObject, position, pickStart);

							if (cells[0].tile != null) break;
						}
					}
				}
			}

			ExBrushEditor.UpdateActiveTilemapDropdown();
		}
		
		#endregion

		#region Erase

		public override void Erase(GridLayout gridLayout, GameObject brushTarget, Vector3Int position)
		{
			// パレット上の処理はベースクラスに任せる
			if (ExPrefabBrush.IsPalette(brushTarget))
			{
				base.Erase(gridLayout, brushTarget, position);
				return;
			}
			
			ExBrushTarget[] ts = FindObjectsOfType<ExBrushTarget>();
			foreach (ExBrushTarget t in ts)
			{
				if (t.TilemapTarget && t.involved)
				{
					Tilemap tilemap = t.GetComponent<Tilemap>();
					if (tilemap != null)
					{
						//Debug.Log(size);
						for (int y = 0; y < size.y; y++)
						{
							for (int x = 0; x < size.x; x++)
							{
								//base.Erase(gridLayout, brushTarget, new Vector3Int(x, y, 0));
								tilemap.SetTile(position + new Vector3Int(-x, y, 0), null);
							}
						}
					}
				}
			}
		}

		#endregion

		#region Move

		Bounds selectionBounds = new Bounds();
		public Bounds SelectionBounds
		{
			get { return selectionBounds; }
			set { selectionBounds = value; }
		}

		List<Transform> moveTargets = new List<Transform>();
		public List<Transform> MoveTargets { get { return moveTargets; } }

		Vector3 prevMovePos = Vector3.zero;

		public Dictionary<ExBrushTarget, TileBase[]> cellsDict;
		
		// Select確定時に呼ばれる
		public override void Select(GridLayout gridLayout, GameObject brushTarget, BoundsInt position)
		{
			base.Select(gridLayout, brushTarget, position);

			Vector3 center = position.center; center.z = 0f;
			Vector3 size2 = position.size; size2.z = 20000f;
			SelectionBounds = new Bounds(center, size2);
		}

		// Move開始時に呼ばれる
		public override void MoveStart(GridLayout gridLayout, GameObject brushTarget, BoundsInt position)
		{
			// パレット上の処理はベースクラスに任せる
			if (ExPrefabBrush.IsPalette(brushTarget))
			{
				base.MoveStart(gridLayout, brushTarget, position);
				return;
			}

			cellsDict = new Dictionary<ExBrushTarget, TileBase[]>();

			// 1. 複数Tilemapの移動プレビュー
			MoveAllTilemapsRoutine(position, true, true);

			// 2. タイル以外の移動の準備
			MoveGameObjectsRoutine(false, position);
		}
		
		// Move中にずっと呼ばれる
		public override void Move(GridLayout gridLayout, GameObject brushTarget, BoundsInt from, BoundsInt to)
		{
			// パレット上の処理はベースクラスに任せる
			if (ExPrefabBrush.IsPalette(brushTarget))
			{
				base.Move(gridLayout, brushTarget, from, to);
				return;
			}

			// 1. 複数Tilemapの移動プレビュー
			MoveAllTilemapsRoutine(to, true, false);

			// 2. タイル以外の移動
			MoveGameObjectsRoutine(true, to);
		}
		
		// Moveが終わったタイミングで呼ばれる
		public override void MoveEnd(GridLayout gridLayout, GameObject brushTarget, BoundsInt position)
		{
			// パレット上の処理はベースクラスに任せる
			if (ExPrefabBrush.IsPalette(brushTarget))
			{
				base.MoveEnd(gridLayout, brushTarget, position);
				return;
			}

			// 1. 複数Tilemapの移動
			MoveAllTilemapsRoutine(position, false, false);

			// 2. タイル以外の移動
			MoveGameObjectsRoutine(true, position);
		}

		// 複数Tilemapの移動ルーチン
		void MoveAllTilemapsRoutine(BoundsInt position, bool preview, bool moveStart = false)
		{
			ExBrushTarget[] ts = FindObjectsOfType<ExBrushTarget>();
			foreach (ExBrushTarget t in ts)
			{
				if (t.TilemapTarget && t.involved)
				{
					Tilemap tilemap = t.GetComponent<Tilemap>();
					tilemap.ClearAllEditorPreviewTiles();

					GridLayout gl = t.GetComponentInParent<GridLayout>();
					if (gl != null)
					{
						if (moveStart)
						{
							// position位置のタイルをカットしてcellsDictに格納する
							cellsDict[t] = GetCells(tilemap, position, true);
						}
						SetCells(tilemap, position, cellsDict[t], preview);
					}
				}
			}
		}

		// GameObjectの移動ルーチン
		void MoveGameObjectsRoutine(bool move, BoundsInt pos)
		{
			if (move)
			{
				// 前回座標と今回座標の差分addを各GameObjectにシンプルに足していく
				Vector3 add = pos.position - prevMovePos;
				add.z = 0;
				foreach (Transform tr in MoveTargets)
				{
					tr.position += add;
				}

				// SelectionBounds の更新
				Bounds b = SelectionBounds;
				b.center += add;
				SelectionBounds = b;
			}

			prevMovePos = pos.position;
		}

		/// <summary>
		/// １つのTilemapについて、タイル群を取得する
		/// </summary>
		TileBase[] GetCells(Tilemap tilemap, BoundsInt position, bool cut)
		{
			int cellCount = position.size.x * position.size.y;
			TileBase[] ret = new TileBase[cellCount];
			//Debug.Log(ret.Length);
			int i = 0;
			for (int y = position.yMin; y < position.yMax; y++)
			{
				for (int x = position.xMin; x < position.xMax; x++)
				{
					//Debug.Log(x + ", " + y);
					ret[i] = tilemap.GetTile(new Vector3Int(x, y, 0));
					if (cut)
					{
						tilemap.SetTile(new Vector3Int(x, y, 0), null);
					}
					i++;
				}
			}
			return ret;
		}

		/// <summary>
		/// １つのTilemapについて、タイル群をセットする。
		/// <paramref name="preview"/>を指定した場合はプレビューセットとする
		/// </summary>
		void SetCells(Tilemap tilemap, BoundsInt position, TileBase[] cells, bool preview)
		{
			int i = 0;
			for (int y = position.yMin; y < position.yMax; y++)
			{
				for (int x = position.xMin; x < position.xMax; x++)
				{
					if (preview) tilemap.SetEditorPreviewTile(new Vector3Int(x, y, 0), cells[i]);
					else tilemap.SetTile(new Vector3Int(x, y, 0), cells[i]);
					i++;
				}
			}
		}
		
		#endregion
	}

	[CustomEditor(typeof(ExBrush))]
	public class ExBrushEditor : GridBrushEditor
	{
		private ExBrush generalBrush { get { return target as ExBrush; } }
		
		// アンドゥをカスタマイズする
		public override void RegisterUndo(GameObject brushTarget, GridBrushBase.Tool tool)
		{
			// パレット上の処理はベースクラスに任せる
			if (ExPrefabBrush.IsPalette(brushTarget))
			{
				base.RegisterUndo(brushTarget, tool);
				return;
			}
			
			switch (tool)
			{
				// Eraseツールでは、全Tilemapを登録する（ベースクラスでは１つのTilemapしか登録されないので）
				case GridBrushBase.Tool.Erase:
				{
					ExBrushTarget[] ts = FindObjectsOfType<ExBrushTarget>();
					foreach (ExBrushTarget t in ts)
					{
						if (t.TilemapTarget && t.involved)
						{
							base.RegisterUndo(t.gameObject, tool);
						}
					}
					break;
				}
				// Moveツールでは、全TilemapだけでなくNPCやコイン等のGameObjectも登録する
				case GridBrushBase.Tool.Move:
				{
					generalBrush.MoveTargets.Clear();

					ExBrushTarget[] ts = FindObjectsOfType<ExBrushTarget>();
					foreach (ExBrushTarget t in ts)
					{
						if (!t.involved) continue;

						if (t.TilemapTarget)
						{
							base.RegisterUndo(t.gameObject, tool);
						}
						else if (t.ObjectsTarget)
						{
							int c = t.transform.childCount;
							for (int i = 0; i < c; i++)
							{
								Transform tr = t.transform.GetChild(i);
								Vector3 pos = tr.position;
								if (generalBrush.SelectionBounds.Contains(pos))
								{
									// 第1引数名がbrushTargetなので不安だが、
									// 普通のGameObjectを登録しても問題なく動く
									base.RegisterUndo(tr.gameObject, tool);
									
									generalBrush.MoveTargets.Add(tr);
								}
							}
						}
					}

					break;
				}
				default:
				{ 
					base.RegisterUndo(brushTarget, tool);
					break;
				}
			}
		}

		// 一部の条件の時のみ、プレビューの処理を無効化する
		public override void OnPaintSceneGUI(GridLayout gridLayout, GameObject brushTarget, BoundsInt position, GridBrushBase.Tool tool, bool executing)
		{
			// パレット上の処理はベースクラスに任せる
			if (brushTarget.layer == 31)
			{
				base.OnPaintSceneGUI(gridLayout, brushTarget, position, tool, executing);
				return;
			}
			
			if (tool == GridBrushBase.Tool.Move)
			{
				if (executing)
				{
					// Moveの実行中はExBrush側でプレビューを弄っているので、ここでは何もしない
					// （ここでbase.OnPaintSceneGUI()を呼び出すと左下に１マスだけ選択タイルがプレビューされるという不具合が起こる）
				}
				else
				{
					base.OnPaintSceneGUI(gridLayout, brushTarget, position, tool, executing);

					// 移動中にEscキーを押してキャンセル(Revert)するとプレビューのクリア漏れが起こるが、
					// これはデフォルトブラシでも同じ挙動だったので保留
				}
			}
			else
			{
				base.OnPaintSceneGUI(gridLayout, brushTarget, position, tool, executing);
			}
		}
		
		/// <summary>
		/// 使用中のツール
		/// </summary>
		GridBrushBase.Tool? activeTool = null;

		/// <summary>
		/// 描画系ツールかどうか
		/// </summary>
		bool paintTool
		{
			get
			{
				if (activeTool == null) return false;
				return activeTool == GridBrushBase.Tool.Box ||
					   activeTool == GridBrushBase.Tool.Paint ||
					   activeTool == GridBrushBase.Tool.FloodFill;
			}
		}

		public override void OnToolActivated(GridBrushBase.Tool tool)
		{
			base.OnToolActivated(tool);
			activeTool = tool;
			UpdateActiveTilemapDropdown();
		}

		public override void OnToolDeactivated(GridBrushBase.Tool tool)
		{
			base.OnToolDeactivated(tool);
			activeTool = null;
		}

		// これをオーバーライドすると、Active Tilemap のドロップダウンの内容をカスタマイズできる
		public override GameObject[] validTargets
		{
			get
			{
				List<GameObject> ret = new List<GameObject>();

				ExBrushTarget[] brushTargets = FindObjectsOfType<ExBrushTarget>();
				foreach (ExBrushTarget brushTarget in brushTargets)
				{
					if (brushTarget.TilemapTarget)
					{
						// 描画ツールの場合はタイルに適合するExBrushTargetに制限する
						if (paintTool)
						{
							if (generalBrush.cellCount > 0)
							{
								TileBase selectedTile = generalBrush.cells[0].tile;
								if (brushTarget.IsAccepted(selectedTile))
								{
									ret.Add(brushTarget.gameObject);
								}
							}
						}
						// それ以外では全てのTilemapを含める
						else
						{
							ret.Add(brushTarget.gameObject);
						}
					}
				}

				ret.Sort((a, b) => string.Compare(a.name, b.name));

				return ret.ToArray();
			}
		}

		/// <summary>
		/// validTargets の値を変えても Active Tilemap ドロップダウンの内容がすぐに変わらない困った仕様なので、
		/// 全TilemapのGameObjectのアクティブ状態を２回トグルさせると内容が更新されるという裏技
		/// </summary>
		public static void UpdateActiveTilemapDropdown()
		{
			Tilemap[] tilemaps = FindObjectsOfType<Tilemap>();
			foreach (Tilemap tilemap in tilemaps)
			{
				GameObject go = tilemap.gameObject;
				go.SetActive(!go.activeSelf);
				go.SetActive(!go.activeSelf);

				// 多分もうちょっとスマートなやり方がありそう
			}
		}
	}
}