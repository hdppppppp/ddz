# Unity 许可证配置记录

## 结论（2026-11-08）

- 账号 `kiwipro@qq.com` 开启了 2FA，无头激活必须提供 TOTP 种子，否则 Unity API 返回
  `Invalid Credential (143.002)`（误导性错误码，实际是缺二次验证）。
- Personal 席位**不支持**手动 .ulf 文件激活（license.unity3d.com/manual 明确声明），
  只能用 邮箱+密码+TOTP 在线占座。
- Personal 席位无需在网页上"启用"，首次 CI/编辑器占座时自动启用。

## 当前 Secrets 配置

| Secret | 内容 |
|---|---|
| UNITY_EMAIL | kiwipro@qq.com |
| UNITY_PASSWORD | （密码） |
| UNITY_TOTP_SEED | （2FA TOTP 种子，CI 自动算验证码） |

workflow 中 unity-builder 步骤的 env 需要同时带上这三个变量。
