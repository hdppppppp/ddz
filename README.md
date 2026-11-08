# DDZ 斗地主（Cocos Creator 单机版）

经典三人斗地主（叫分制），纯单机：玩家 + 2 个 AI，无后端。

> 2026-11-08 起从 Unity 切换到 Cocos Creator（Unity 免费许可在 CI 无头环境激活被
> Unity 服务端反复拒绝，详见 `unity-main` 分支留档）。Cocos 无任何许可证验证，
> 编辑器官方直链可脚本化下载，全流程 GitHub Actions 云构建，本机零安装。

- 引擎：**Cocos Creator 3.8.6**（MIT 许可，Windows/macOS 编辑器）
- 构建：GitHub Actions **windows runner** → 编辑器 CLI 生成 Android 工程 → gradle 出 APK
- 规则引擎：`assets/Scripts/Core`（TypeScript，零引擎依赖，vitest 单测）
- 缓存提速：编辑器 zip、NDK、项目 library 三层 actions/cache

## CI 流水线

| Job | Runner | 内容 |
|---|---|---|
| core-tests | ubuntu | vitest 规则引擎测试 |
| cocos-build | windows | 下载编辑器(缓存) → CLI 构建工程 → stage=make 编译 APK → Artifact |
| release | ubuntu | 打 `v*` tag 自动发 GitHub Release |

## 目录

```
assets/Scripts/Core/     规则引擎（Card/HandPattern/PlayValidator/GameController/AI）
assets/Scripts/GameRoot.ts  牌桌 UI（纯代码构建，无预制体依赖）
assets/Scenes/game.scene 主场景
ci/build-config-android.json  Cocos 构建配置
tests/                   vitest 单元测试
docs/                    设计文档
```

## 里程碑

M1 工程骨架+云构建 → M2 规则引擎(已完成 TS 移植) → M3 AI(已完成) → M4 UI → M5 结算积分 → M6 发布
