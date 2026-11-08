# DDZ 斗地主（Unity 单机版）

经典三人斗地主（叫分制），纯单机：玩家 + 2 个 AI，无后端。

- 引擎：Unity 2022.3.76f1（内置渲染管线，UGUI）
- 构建：**全部在 GitHub Actions 云端完成**（GameCI），本机不编译
- 规则引擎：`Assets/Scripts/Core`（纯 C#，无 Unity 依赖，CI 用 dotnet test 验证）
- 测试：`dotnet-tests/`（xUnit）+ 后续 EditMode 测试

## CI 流水线

| Job | 内容 | Runner |
|---|---|---|
| core-tests | `dotnet test` 规则引擎测试 | ubuntu-latest（公开仓库免费） |
| unity-build | 激活许可证 → 云端构建 Android APK → 上传 Artifact | ubuntu-latest + GameCI 镜像 |

APK 在每次 push 到 `main` 后自动构建，到该次 run 的 **Artifacts** 页下载。

## 目录

```
Assets/Scripts/Core/          规则引擎（牌、牌型、校验、AI、流程）
Assets/Scripts/Presentation/  UGUI 表现层
Assets/Scripts/Editor/        云端构建入口（GameCI buildMethod 调用）
dotnet-tests/                 规则引擎单元测试
docs/design.md                设计方案
```

## 里程碑

M1 骨架+云构建 ✅ → M2 规则引擎 → M3 AI → M4 UI → M5 结算积分 → M6 发布
