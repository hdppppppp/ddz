# 斗地主（DDZ）Unity 单机版 — 设计方案

> 版本 v1.1（2026-11-08）
> 仓库：https://github.com/hdppppppp/ddz
> 形态：纯单机（无后端），玩家 vs 2 个 AI；**全部构建在 GitHub Actions 云端完成**（GameCI），本机零安装、零编译。

### v1.1 变更
- 构建方案从 Unity Build Automation 改为 **GitHub Actions + GameCI（unity-builder v6）**：公开仓库 Actions 免费，无 Dashboard 依赖。
- 云构建细节：容器内自动激活许可证（`UNITY_EMAIL/PASSWORD` secrets）；game-ci CLI 经预取步骤（504 退避重试）+ `actions/cache` 缓存键 `game-ci-cli-v0.1.72-linux-x64`（与 builder 内部实现一致）。
- 场景不进仓库：`DDZ.Editor.CloudBuild.BuildAndroid` 在云端**代码生成场景**（SceneBootstrapper）后构建，避免手写场景 YAML。


---

## 1. 项目概览

| 项 | 决定 |
|---|---|
| 引擎 | Unity **2022.3 LTS**（如 2022.3.62f1，Build Automation 支持良好） |
| 渲染 | 内置管线（Built-in RP），2D / UGUI |
| UI | UGUI + TextMeshPro（内置包，零外部依赖） |
| 玩法 | 经典 3 人斗地主：**叫分制**（1/2/3 分），2 农民合作 vs 1 地主 |
| 平台 | 首选 **Android（APK）**，附带 Windows x64 构建 |
| 编译 | **Unity Build Automation（原 Cloud Build）**，仓库根 `unity.yaml` 驱动 |
| 代码 | C#，逻辑层与表现层用 Assembly Definition 隔离 |
| 后端 | 无。积分本地持久化（PlayerPrefs / JSON 文件） |

### 设计原则

1. **规则引擎与 Unity 解耦**：`DDZ.Core` 程序集不引用 `UnityEngine`，可在 CI 里用 `dotnet test` 跑单元测试，也可被任何宿主复用。
2. **事件驱动**：逻辑层发出领域事件（发牌/叫分/出牌/结算…），表现层订阅刷新，逻辑层不知道 UI 存在。
3. **数据驱动**：规则常量（顺子最短长度、翅膀是否可拆炸弹等）集中在 `GameConfig`，一处可调。

---

## 2. 玩法规则

### 2.1 牌

- 54 张：3–A、2 各 4 张（花色仅展示用，比牌不分花色），小王、大王各 1。
- 单张大小序：3 < 4 < 5 < 6 < 7 < 8 < 9 < 10 < J < Q < K < A < 2 < 小王 < 大王。

### 2.2 牌型

| 牌型 | 定义 | 备注 |
|---|---|---|
| 单张 | 1 张 | |
| 对子 | 2 张同点 | 王不能成对（两王是王炸） |
| 三张 | 3 张同点 | |
| 三带一 | 三张 + 1 单 | |
| 三带对 | 三张 + 1 对 | |
| 顺子 | ≥5 张连续单张 | 范围 3~A，不含 2 和王 |
| 连对 | ≥3 对连续 | 范围 3~A |
| 飞机不带 | ≥2 组连续三张 | 范围 3~A，2 不能进飞机 |
| 飞机带单 | 连续三张 × n + n 张单 | 翅膀不可拆炸弹/王炸（可配置） |
| 飞机带对 | 连续三张 × n + n 对 | 同上 |
| 四带二 | 4 张 + 2 单，或 4 张 + 2 对 | |
| 炸弹 | 4 张同点 | 大于一切非炸弹 |
| 王炸 | 大王+小王 | 大于一切，出牌即翻倍 |

比牌规则：同牌型同长度才能比；炸弹压非炸弹；王炸压一切；飞机/顺子/连对必须张数完全一致。

### 2.3 流程与状态机

```
Dealing → Bidding → Playing → Settling → (再来一局)
```

| 阶段 | 行为 |
|---|---|
| Dealing | 51 张随机发 3 人（各 17），剩 3 张作底牌 |
| Bidding | 从随机首叫者起轮流叫 1/2/3 分或不叫；叫 3 立即成地主；全部不叫则重发。最高叫分 = 底分，地主拿底牌 |
| Playing | 地主先出，逆时针轮流：出牌（须大过上家）或"不出"；连续两家不出 → 上一出牌者获自由出牌权 |
| Settling | 一方出完即终局，按 §2.4 结算，显示胜利动画与得分，"再来一局" |

### 2.4 计分

- 得分 = 底分（叫分）× 倍数。
- 倍数初始 1：每出一个**炸弹或王炸 ×2**；**春天**（地主赢且农民没出过牌）×2；**反春**（农民赢且地主只出过 1 手）×2。
- 分配：地主 ±2 份，农民各 ±1 份。
- 本地积分：`PlayerPrefs`/JSON 存 3 个座位累计分，主界面显示。

---

## 3. 系统架构

```
┌─────────────────────── 表现层 DDZ.Presentation (MonoBehaviour) ───────────────────────┐
│  BoardView(牌桌)  HandView(手牌)  SeatView×2(AI头像/剩牌数)  ActionBar(出/不出/提示)     │
│  BiddingView  BottomCardsView(底牌)  ResultDialog  ScoreBoard      全部订阅 GameEvents │
└──────────────────────────────────────▲───────────────────────────────────────────────┘
                                       │ C# event / 事件总线
┌──────────────────────────────────────┴───────── 逻辑层 DDZ.Core (纯 C#, 无 Unity) ─────┐
│  GameController（状态机：Dealing→Bidding→Playing→Settling）                             │
│  ├─ Dealer        发牌洗牌                                                             │
│  ├─ PatternRecognizer  牌型识别（张数+连续性+同点分组）                                    │
│  ├─ PlayValidator 合法性/比牌校验                                                        │
│  ├─ BidAI / PlayAI   叫分与出牌 AI（也是"提示"功能的实现）                                 │
│  └─ Scorer       计分                                                                   │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

**回合控制**：`GameController` 每次只推进一步（`Tick()` 或事件回调式 API），AI 的动作在下一帧通过协程/延迟回放，保证节奏感与 UI 动画可插入；人类玩家的动作由 UI 调 `controller.PlayCards(...)` / `Bid(...)` 提交，非法输入在 `PlayValidator` 拒绝并提示。

### 3.1 核心数据模型

```csharp
// Rank: Three..Two, SmallJoker, BigJoker（比牌只看 Rank）
struct Card { int Id; Rank Rank; }            // Id 0..53 保证牌唯一、可复制可排序
sealed class PlayerState { int Seat; List<Card> Hand; bool IsLandlord; bool HasPlayed; }
sealed class RoundContext { Card[]? LastPlay; int LastSeat; int FreeSeat; int Multiplier; ... }
```

`PatternRecognizer`：输入 ≤20 张牌 → 输出 `HandPattern { Type, MainRank, Length, Cards }`（同一组牌只取最高优先级牌型；比牌用 `(Type, Length, MainRank)` 三元组比较 + 炸/王炸特殊规则）。

### 3.2 AI 设计（单机体验核心）

**叫分 AI**：手牌强度分 = 大王8 + 小王6 + 每个2×4 + 每个A×3 + 每个K×1.5 + 炸弹+8、王炸+10。
阈值：≥22 → 叫3；≥16 → 叫2；≥11 → 叫1；否则不叫。参数集中在 `BidAI`，可调。

**出牌 AI**（贪心 + 规则启发，够用、可升级）：

1. **手牌分解**：把手牌贪心切成最少"组合数"（优先长顺子/连对/飞机，其次保留炸弹与王炸不拆）。
2. **自由出牌**：从分解结果选——队友/对手压力判断：优先出最长且最小主牌的组合；单张出最小；只剩 1~2 手时直接打完。
3. **跟牌**：枚举可压上家的最小同型组合（含拆牌代价排序：不拆 > 拆对 > 拆三 > 用炸弹）；找不到则"不出"。
4. **农民配合**：队友出牌且牌不小（单 ≥A、对 ≥2）时不压；对手剩牌 ≤2 时优先封死（出大的）。
5. **炸弹时机**：仅当对手剩牌 ≤2、或自己接下来 1 手能走完、或下家是对手且已到高危时才炸。

`HintProvider` 直接复用 PlayAI 的枚举逻辑给玩家"提示"按钮。

### 3.3 UI 布局（横竖屏：竖屏优先，兼容横屏）

- 底部：玩家手牌（横向重叠排布，选中上浮），出牌/不出/提示按钮。
- 中央：最近出牌展示区（3 个座位方向）、倒计时圈（AI 思考节拍 0.8~1.5s）。
- 顶部：底牌 3 张（叫地主后翻开）、倍数、局数。
- 左右：AI 头像框、剩牌数、地主标识、"不出"气泡。
- 结算：弹窗显示三方得分明细（底分×倍数×份数），春天/炸弹动画文案。
- **美术方案**：卡面用 UGUI 程序化绘制（点数文字+花色符号+色块），不依赖外部图集；后续可无痛替换为真图。背景纯色渐变 + 简单 UI 装饰。

---

## 4. 代码目录

```
ddz/
├── unity.yaml                    # 云编译配置（见 §5）
├── .gitignore                    # Unity 官方模板（Library/ Temp/ Logs/ Obj/ Builds/ …）
├── docs/design.md                # 本文档
├── Assets/
│   ├── Scripts/
│   │   ├── Core/                 # DDZ.Core.asmdef（无 UnityEngine 引用）
│   │   │   ├── Model/            # Card, Rank, Deck, PlayerState, GameConfig
│   │   │   ├── Pattern/          # PatternType, HandPattern, PatternRecognizer
│   │   │   ├── Rules/            # PlayValidator（合法性+比牌）
│   │   │   ├── AI/               # BidAI, PlayAI, HintProvider
│   │   │   └── Flow/             # GameController(状态机), GameEvents, Scorer
│   │   └── Presentation/         # DDZ.Presentation.asmdef（引用 Core）
│   │       ├── Views/            # CardView, HandView, SeatView, BoardView
│   │       ├── Controllers/      # GamePresenter（订阅事件→驱动 UI）
│   │       └── UI/               # ActionBar, BiddingView, ResultDialog, ScoreBoard
│   ├── Scenes/Game.unity
│   ├── Prefabs/                  # CardPrefab, SeatPrefab, Dialogs
│   └── Tests/
│       └── CoreTests/            # EditMode NUnit（牌型识别/校验/AI 冒烟）
├── Packages/manifest.json        # 仅内置包（UGUI/TMP/2D）
└── ProjectSettings/
```

---

## 5. 云端编译（关键要求：不本地编译）

方案：**Unity Build Automation（原 Unity Cloud Build）**，Personal 计划免费额度即可。

1. **仓库侧**：提交 `unity.yaml`（见下），Build Automation 检测到后自动建立构建目标。
2. **云端侧（需要你在 Unity Dashboard 完成一次性授权）**：
   - 登录 [Unity Dashboard](https://dashboard.unity3d.com) → Projects → New project → **Connect GitHub 仓库 `hdppppppp/ddz`**（OAuth 授权）。
   - 添加 Build Automation，它会读取 `unity.yaml`，自动选 Unity 2022.3 LTS 版本云上编译。
3. **产物**：Android APK / Windows x64 zip，网页下载；支持每次 push 自动构建。

`unity.yaml`（示意，Dashboard 校验后微调字段名）：

```yaml
editorVersion: 2022.3.62f1
unityLicensingServiceVersion: release
projectPath: .
targets:
  android:
    name: Android
    enabled: true
    options:
      bundleIdentifier: com.hdppppppp.ddz
      buildType: apk
      exportOptions: null
  windows:
    name: Windows
    enabled: true
    options:
      buildType: x64
```

**规则测试 CI（补充）**：`DDZ.Core` 是纯 .NET 代码，另加 GitHub Actions `dotnet test` 跑规则引擎单元测试（不碰 Unity、不需要许可证），保证"云端可编译 + 云端可测试"。

---

## 6. 里程碑

| 阶段 | 内容 | 验收 |
|---|---|---|
| M1 | 仓库骨架：.gitignore / 工程 / asmdef / unity.yaml，推送 GitHub，**云编译首次出包** | Build Automation 出任意平台包 |
| M2 | 规则引擎：发牌、牌型识别、比牌校验、状态机 + 单元测试（Actions 绿） | `dotnet test` 全绿 |
| M3 | AI：叫分、出牌、提示 | AI 对局 100 局无卡死/非法牌 |
| M4 | UI 整合：手牌交互、出牌动画节拍、叫分流程、底牌 | 人机对局完整跑通 |
| M5 | 结算/积分/春天炸弹倍数/音效占位/再来一局 | 完整游戏体验 |
| M6 | 打磨与发布：图标、竖屏适配、包名版本号，云编译正式包发 GitHub Release | APK 可安装运行 |

---

## 7. 风险与待定项

| 项 | 说明 | 默认决定 |
|---|---|---|
| Unity Dashboard 授权 | 需要你网页登录授权 GitHub + 创建 Build Automation 项目（一次性） | 到 M1 时我来引导，你只需网页点击 |
| 云编译时长限制 | Personal 计划并发 1、排队可能慢 | M1 就打通链路，后续 push 自动出包不阻塞开发 |
| 美术 | 无美术资源 | 程序化卡面，纯色风格 |
| 横竖屏 | 先竖屏 | 后续按需加横屏 |
| 规则分歧点 | 翅膀能否拆炸弹、四带二带几张 | 均做成 `GameConfig` 常量 |
