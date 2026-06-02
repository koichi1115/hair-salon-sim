# Phase 1 設計書 — ヘアカットデモ

## 1. 概要

**ゴール**: マネキンヘッドの髪をハサミで切れるプレイアブルデモ
**期間目安**: 4〜6週間
**成果物**: Windows向けデモビルド (.exe)

---

## 2. 技術調査結果と設計判断

### 2.1 com.unity.demoteam.hair の制約

調査の結果、以下の重要な制約が判明した：

| 項目 | 状況 |
|---|---|
| ランタイムでのストランド分割 | **不可能**（APIなし） |
| ランタイムでのストランド数変更 | **不可能**（ビルド時に固定） |
| ランタイムでのストランド長変更 | グループ単位で一律のみ（per-strandは不可） |
| パーティクル数/ストランド | グループ内で均一（3〜128） |
| 最大ストランド数/グループ | 64,000本 |
| GPUバッファ直接操作 | 可能だが非公式（`_StagingVertex`, `_ParticlePosition`） |

### 2.2 カット実現方式の検討

| 方式 | 概要 | 評価 |
|---|---|---|
| A. GPUバッファ直アクセス | ComputeShaderで`_ParticlePosition`を書き換え、カット点以降のパーティクルを根元に縮退させる | △ 非公式、ソルバーと競合リスク |
| B. カスタムレンダリング | 描画時にカット長を反映するカスタムシェーダーで、カット点以降を非表示にする | ○ ソルバーに干渉しない |
| C. パーティクル位置上書き | 毎フレーム、カット点以降のパーティクルを直前のパーティクル位置にコピーして長さ0に見せる | ○ シンプル、ただし毎フレームコスト |
| **D. ハイブリッド方式（採用）** | カット情報をComputeBufferで管理し、カスタムシェーダーでカット点以降を非表示 + ソルバーのパーティクル位置は残す（物理は継続） | ◎ 描画のみ制御、物理破綻なし |

### 2.3 採用方式: ハイブリッドカット

```
[カットデータ ComputeBuffer]
  per-strand: cutParticleIndex (int)
  初期値: strandParticleCount (= カットなし)

[カスタムシェーダー]
  頂点シェーダーで現在パーティクルindex >= cutParticleIndex なら
  → position を前のパーティクルにクランプ or discard

[カット操作時]
  1. マウスからレイキャスト
  2. ストランドのパーティクル列とレイの最近接点を計算
  3. 最近接パーティクルindexを cutParticleIndex に書き込み
  4. 以降のフレームでシェーダーが自動的に短く描画
```

**メリット**:
- ソルバー（物理演算）に一切干渉しない
- パフォーマンス影響が最小（シェーダー内の分岐のみ）
- カット情報はCPU側のint配列で管理、シンプル

---

## 3. アーキテクチャ

```
Assets/
├── Scenes/
│   └── DemoScene.unity          # メインシーン
├── Scripts/
│   ├── Core/
│   │   ├── HairCutSystem.cs     # カットデータ管理 + ComputeBuffer
│   │   ├── HairCutDetector.cs   # レイキャスト → ストランド交差判定
│   │   └── HairSetup.cs         # HairInstance初期化ヘルパー
│   ├── Tools/
│   │   ├── ScissorTool.cs       # ハサミ操作（入力 → カット実行）
│   │   └── ToolManager.cs       # ツール切替管理（Phase1はハサミのみ）
│   ├── Camera/
│   │   └── OrbitCamera.cs       # オービットカメラ
│   └── UI/
│       └── DemoUI.cs            # リセットボタン等
├── Shaders/
│   └── HairCutLit.shadergraph   # カット対応カスタムヘアシェーダー
├── Data/
│   └── HairPresets/             # ヘアアセット設定プリセット
└── Models/
    └── MannequinHead/           # 頭部モデル + テクスチャ
```

---

## 4. コンポーネント詳細設計

### 4.1 HairCutSystem.cs

カットの状態管理とGPUへのデータ転送を担当。

```csharp
public class HairCutSystem : MonoBehaviour
{
    // --- 状態 ---
    // per-strand カットインデックス（パーティクル番号）
    // 値 = strandParticleCount → 未カット
    // 値 = N → Nより先のパーティクルは非表示
    private int[] cutIndices;
    private ComputeBuffer cutBuffer;

    // --- 公開API ---
    void Initialize(HairInstance hairInstance);
    void CutStrand(int strandIndex, int particleIndex);
    void CutStrands(int[] strandIndices, int[] particleIndices); // バッチカット
    void ResetAllCuts();
    void PushToGPU(); // cutBufferを更新、シェーダーにバインド
}
```

**データフロー**:
```
CutStrand() → cutIndices[i] = min(current, new) → PushToGPU() → Shader読み取り
```

### 4.2 HairCutDetector.cs

マウス位置からどのストランドのどのパーティクルを切るか判定。

```csharp
public class HairCutDetector : MonoBehaviour
{
    [SerializeField] float cutRadius = 0.005f; // カット判定半径(m)

    // --- 判定ロジック ---
    // 1. Camera.ScreenPointToRay でマウスレイ生成
    // 2. HairInstance から全パーティクル位置を取得
    //    (GPUReadback or CPU側キャッシュ)
    // 3. 各ストランドのパーティクル列（折れ線）とレイの距離を計算
    // 4. cutRadius以内の最近接ストランド＋パーティクルを返す

    public struct CutResult
    {
        public bool hit;
        public int strandIndex;
        public int particleIndex;
        public Vector3 worldPosition;
    }

    public CutResult DetectCut(Ray ray);
    public CutResult[] DetectCutsInArea(Ray ray, float areaRadius); // ドラッグカット用
}
```

**パフォーマンス考慮**:
- 全ストランド×全パーティクルの総当りは重い（10,000 × 32 = 320,000判定）
- **空間分割**（BVH or グリッド）でカリングしてから詳細判定
- Phase 1 では簡易グリッドハッシュで十分（ストランド5,000本程度）
- GPU Readback は AsyncGPUReadback を使用（1フレーム遅延許容）

### 4.3 ScissorTool.cs

プレイヤーの入力をカットアクションに変換。

```csharp
public class ScissorTool : MonoBehaviour
{
    // --- 操作 ---
    // 左クリック: 単発カット（クリック位置のストランドを1本カット）
    // 左ドラッグ: 連続カット（軌跡上のストランドをまとめてカット）

    // --- ビジュアル ---
    // カーソル位置にハサミモデル表示
    // カット時にパーティクルエフェクト（切った髪の破片）
    // カット判定範囲の可視化（デバッグ用、半透明球）

    // --- 手触り調整パラメータ ---
    [SerializeField] float cutInterval = 0.05f;   // ドラッグ時のカット間隔(秒)
    [SerializeField] float cutAreaRadius = 0.01f;  // ドラッグ時の影響範囲(m)
    [SerializeField] bool hapticFeedback = true;   // カメラ微振動
}
```

### 4.4 OrbitCamera.cs

```csharp
public class OrbitCamera : MonoBehaviour
{
    [SerializeField] Transform target;          // マネキンヘッド中心
    [SerializeField] float distance = 0.5f;     // 初期距離(m)
    [SerializeField] float minDistance = 0.2f;
    [SerializeField] float maxDistance = 1.5f;
    [SerializeField] float rotateSpeed = 5f;
    [SerializeField] float zoomSpeed = 0.1f;
    [SerializeField] float minPitch = -30f;     // 下からの覗き込み制限
    [SerializeField] float maxPitch = 80f;      // 真上からの制限

    // 操作: 右ドラッグ=回転、ホイール=ズーム、中ドラッグ=パン
}
```

### 4.5 HairCutLit.shadergraph（カスタムシェーダー）

```
// 入力
StructuredBuffer<int> _CutIndices;  // per-strand カットインデックス
int _StrandParticleCount;
int _StrandCount;

// 頂点シェーダー内ロジック（疑似コード）
int strandIdx = vertexID / strandParticleCount;  // memory layout依存
int particleIdx = vertexID % strandParticleCount;

if (particleIdx >= _CutIndices[strandIdx])
{
    // カット点以降 → 直前のパーティクル位置にクランプ
    // （完全にdiscardするとstrip描画が崩れるため、長さ0に縮退させる）
    position = previousParticlePosition;
}
```

※ ShaderGraphのCustom Functionノードで実装。com.unity.demoteam.hairの`HairVertex.shadersubgraph`を拡張する形で組み込む。

---

## 5. データ仕様

### 5.1 ヘアアセット設定

| パラメータ | 値 | 備考 |
|---|---|---|
| ストランド数 | 5,000本（初期）→ 10,000本（最適化後） | パフォーマンス見て調整 |
| パーティクル数/ストランド | 32 | カット解像度に影響（32段階の長さ） |
| 髪の長さ | 25cm（ミディアム） | カットの変化が分かりやすい |
| 髪の太さ | 0.05mm | リアルな値 |
| メモリレイアウト | Interleaved | GPU効率優先 |
| LOD | 有効（3段階） | パフォーマンス確保 |

### 5.2 物理パラメータ初期値

| パラメータ | 値 | 備考 |
|---|---|---|
| ソルバー | GaussSeidel | 安定性重視 |
| サブステップ | 2 | 精度とパフォーマンスのバランス |
| 重力 | (0, -9.81, 0) | 標準 |
| 剛性 (stiffness) | 0.8 | 髪が立ちすぎず垂れすぎず |
| 減衰 (damping) | 0.5 | 揺れが収まる速度 |
| 衝突摩擦 | 0.3 | 頭部との衝突時 |
| 距離制約 (LRA) | 有効 | ストランドの伸びを防止 |

### 5.3 頭部モデル要件

| 項目 | 要件 |
|---|---|
| ポリゴン数 | 5,000〜20,000 tri |
| 頭皮メッシュ | 独立サブメッシュとして分離（ヘアルート配置用） |
| UV | 頭皮部分に展開済み |
| フォーマット | FBX |
| ライセンス | 商用利用可 |

候補:
- Unity Asset Store "Mannequin Head" 系アセット
- Sketchfab CC0 ヘッドモデル
- Blender で簡易作成（最終手段）

---

## 6. 処理フロー

### 6.1 初期化

```
1. HairAsset ロード（プリベイク済みストランドデータ）
2. HairInstance 生成 → ソルバー初期化
3. HairCutSystem 初期化
   - cutIndices[strandCount] を strandParticleCount で埋める
   - ComputeBuffer 作成 → シェーダーにバインド
4. HairCutDetector 初期化
   - パーティクル位置の空間ハッシュ構築
5. OrbitCamera セットアップ
```

### 6.2 毎フレーム（カット操作中）

```
1. Input: マウス左ボタン押下/ドラッグ検出
2. ScissorTool: Camera.ScreenPointToRay でレイ生成
3. HairCutDetector: レイ vs ストランド交差判定
   3a. AsyncGPUReadback で最新パーティクル位置取得（1f遅延）
   3b. 空間ハッシュでカリング → 近傍ストランドのみ詳細判定
   3c. CutResult 返却
4. HairCutSystem: CutStrand(strandIdx, particleIdx)
   4a. cutIndices更新
   4b. PushToGPU()
5. Shader: カット反映（即座に描画に反映）
6. VFX: カットパーティクルエフェクト再生（任意）
```

### 6.3 リセット

```
1. DemoUI: リセットボタン押下
2. HairCutSystem.ResetAllCuts()
   - cutIndices を全て strandParticleCount にリセット
   - PushToGPU()
3. 即座に全髪が元の長さに戻る
```

---

## 7. 開発順序とマイルストーン

### M1: プロジェクトセットアップ（3日）

| # | タスク | 完了条件 |
|---|---|---|
| 1 | Unity 6 プロジェクト作成（URP） | プロジェクトが開ける |
| 2 | com.unity.demoteam.hair 導入 | サンプルシーンが動く |
| 3 | Git LFS + .gitignore | 大容量ファイルがLFS管理される |

### M2: マネキンヘッド + ヘア（5日）

| # | タスク | 完了条件 |
|---|---|---|
| 4 | 頭部モデル調達 + インポート | シーンに頭部が表示される |
| 5 | 頭皮メッシュ分離・UV確認 | サブメッシュとして独立 |
| 6 | HairAsset 作成（Mesh配置, 5,000本） | 頭に髪が生えて物理で揺れる |
| 7 | 物理パラメータ調整 | 自然な髪の動き |
| 8 | ビジュアル調整（色・太さ・光沢） | 見た目がそれっぽい |
| 9 | HairBoundary 設定（頭部衝突） | 髪が頭を貫通しない |

### M3: カメラ（2日）

| # | タスク | 完了条件 |
|---|---|---|
| 10 | OrbitCamera 実装 | 右ドラッグで回転、ホイールでズーム |
| 11 | カメラ制限・イージング | 操作が気持ちいい |

### M4: カットシステム（10日）← 最重要

| # | タスク | 完了条件 |
|---|---|---|
| 12 | HairCutSystem 実装 | cutIndices管理 + ComputeBuffer転送 |
| 13 | カスタムシェーダー作成 | cutIndex以降のパーティクルが非表示になる |
| 14 | HairCutDetector 実装（総当り版） | レイキャストでストランドを検出できる |
| 15 | ScissorTool 実装（単発クリック） | クリックで1本切れる |
| 16 | 空間ハッシュ最適化 | 5,000本でも60fps維持 |
| 17 | ドラッグカット実装 | ドラッグ軌跡で連続カット |
| 18 | カットの手触り調整 | 切れ味・範囲・フィードバックが心地よい |

### M5: UI + 仕上げ（3日）

| # | タスク | 完了条件 |
|---|---|---|
| 19 | ハサミカーソル表示 | マウス位置にハサミが追従 |
| 20 | リセットボタン | ワンクリックで髪が元に戻る |
| 21 | パフォーマンス計測 | 60fps @ 5,000ストランド確認 |
| 22 | デモビルド | .exe が単体で動く |

---

## 8. パフォーマンス目標

| 指標 | 目標 |
|---|---|
| FPS | 60fps 以上（GTX 1060相当で） |
| ストランド数 | 5,000本（最低）、10,000本（目標） |
| カット検出レイテンシ | < 16ms（1フレーム以内） |
| GPU Readback 遅延 | 1フレーム（許容） |
| メモリ | < 500MB（VRAM込み） |

---

## 9. 技術リスクと対策

### リスク1: カスタムシェーダーがcom.unity.demoteam.hairと統合できない
**影響**: 髪が描画されない or カット表現が崩れる
**対策**:
- まずデフォルトシェーダーのソースを読んで構造を把握
- ShaderGraphのCustom Functionノードで最小限の変更を加える
- 最悪、描画パイプラインを自前に切り替える（BuiltinStrips → カスタムジオメトリ）

### リスク2: AsyncGPUReadback のパフォーマンス
**影響**: パーティクル位置の取得が遅い → カット判定が不正確
**対策**:
- Readback頻度を毎フレームではなくカット操作中のみに限定
- パーティクル位置をCPU側でも簡易シミュレーション（前フレーム＋重力）して補間

### リスク3: 5,000本ストランドでのカット検出が重い
**影響**: フレームレート低下
**対策**:
- 空間ハッシュグリッド（セルサイズ = cutRadius × 2）
- カーソル近傍のセルのみ検索（O(1)に近い）
- それでも重ければComputeShaderで判定をGPU側に移す
