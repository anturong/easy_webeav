# Easy WebDAV

**⚠️ 试验性工具 — 禁止用于实际生产环境**

这是一个个人学习/试验性质的 WebDAV 文件服务器实现，功能不完善，安全性未经审计，**严禁用于任何实际生产环境或公网部署**。

## 关于密码

本软件不包含任何硬编码密码。首次运行时会自动生成随机管理员密码并写入 `webdav_config.json` 配置文件，控制台或弹窗会显示初始密码。

## 功能

- WebDAV 文件服务（HTTP/HTTPS）
- 多用户管理、读写权限控制
- WPF 图形管理界面
- 自签名 SSL 证书自动生成
- Windows 防火墙规则配置
- 安装/卸载为 Windows 服务

## 快速开始

```bash
dotnet publish src/WebDAVService/WebDAVService.csproj -c Release -r win-x64 -o publish
dotnet publish src/WebDAVConfigurator/WebDAVConfigurator.csproj -c Release -r win-x64 -o publish
publish/WebDAVConfigurator.exe
```

## License

MIT
