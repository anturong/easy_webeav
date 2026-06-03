# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Publish

```bash
# Build both projects
dotnet build -c Release

# Publish both to the same directory
dotnet publish src/WebDAVService/WebDAVService.csproj -c Release -r win-x64 -o publish
dotnet publish src/WebDAVConfigurator/WebDAVConfigurator.csproj -c Release -r win-x64 -o publish

# Deploy web templates to all target directories after publish
cp src/WebDAVService/web/*.html publish/web/
# Also copy to the service install dir (where WebDAVService.exe runs from)
cp src/WebDAVService/web/*.html "C:/Users/41456/Desktop/easy webeav/web/"

# Clean
dotnet clean
```

## Architecture

Two .NET 8 projects in one solution (`WebDAVService.sln`), zero external runtime dependencies.

### WebDAVService (Console → Windows Service)
- **Program.cs** — Entry point. `Host.CreateDefaultBuilder` + `UseWindowsService()` + Serilog file logging to `logs/service.log`.
- **Worker.cs** — `BackgroundService`. Loads config via `WebDAVConfig.Load()`, starts `WebDAVServer`.
- **Config.cs** — JSON config loader. Uses `JsonDocument.Parse` with hand-written field mapping (not `JsonSerializer.Deserialize<WebDAVConfig>`) to avoid circular-deserialization bugs. Reads `webdav_config.json` from exe directory. Static factory `Load()`.
- **WebDAVServer.cs** — Core WebDAV server via `HttpListener`. Dual auth: session cookie (browser) + Basic Auth (WebDAV clients). Handles all WebDAV methods: OPTIONS, PROPFIND, GET, HEAD, PUT, DELETE, MKCOL, COPY, MOVE, PROPPATCH, LOCK, UNLOCK. Read-only user enforcement. Health check timer every 20 minutes.
- **AppInfo.cs** — Shared constants (app name, version, author info). Linked as a compile-time shared file from the Configurator project.
- **CertificateHelper.cs** — Self-signed SSL cert creation + `netsh http add sslcert` binding for HTTPS. Cert stored in LocalMachine\My store, bound to port via netsh.

### WebDAVConfigurator (WPF GUI)
- **Program.cs** — Entry + admin elevation. Detects `--install`/`--uninstall`/`--restart` args, reruns with `runas` for UAC via `Process.Start(Verb="runas")`.
- **MainWindow.xaml + MainWindow.xaml.cs** — WPF dark-theme UI with three cards (settings, user management, service controls) and a color-coded log console (RichTextBox, 500-line cap). Uses `System.Windows.Forms.FolderBrowserDialog` via WinForms interop for folder picking.
- **ServiceHelper.cs** — Service lifecycle via `sc.exe`. Creates service with auto-start, description, failure recovery (3x restart/5s). Cleans up SSL cert binding on uninstall.
- **FirewallHelper.cs** — `netsh advfirewall` rule management for service ports.
- **ConfigHelper.cs** — JSON config model (`WebDAVConfigData`) matching `WebDAVService`'s schema. Uses `JsonSerializer.Deserialize<T>` for reading/writing `webdav_config.json`.

### Web Templates (WebDAVService/web/*.html)
Three HTML files served by `WebDAVServer`, cached in static fields with lazy loading:
- **browse.html** — Full SPA file manager (~750 lines). Client-side JavaScript makes PROPFIND/MKCOL/DELETE/MOVE/PUT requests with Basic Auth (`btoa()` encoding). Features: list/grid views, breadcrumbs, search, upload progress, context menus, action sheets, drag-drop, toast notifications, file type labels, hash-based color generation for unknown extensions, image/video preview modal. Font Awesome 6.5.0 CDN for icons.
- **login.html** — Standalone login form (POST, fallback for when JS is disabled). Also supports JSON POST from SPA.
- **logout.html** — Confirmation page with link back to `/login`.

Templates use `{{APP_NAME}}`, `{{AUTHOR}}`, `{{AUTHOR_URL}}`, `{{VERSION}}` placeholders replaced server-side. Templates are cached in static fields (`_browseTemplate` etc.) — service restart required to pick up changes.

## Key Design Decisions

- **Zero external dependencies** — No nssm.exe, .bat, Python. Service via `sc.exe` + `ServiceController`.
- **UAC elevation** — Single prompt via `Process.Start(Verb="runas")` with `--command` args.
- **Config sharing** — Both projects read/write `webdav_config.json` from exe directory. Service uses hand-parsed JSON (avoiding circular deserialization), Configurator uses `JsonSerializer.Deserialize<T>`.
- **HttpListener binding** — `0.0.0.0` → `+` for HttpListener prefix (`http://+:port/`). Cert creation falls back to `localhost` when host is `+`. Binding to `+` requires admin rights. Use `localhost` to avoid admin URL ACL.
- **SPA auth flow** — Browser GET to any directory serves the SPA template without requiring auth. All data operations (PROPFIND, MKCOL, DELETE, etc.) are made by client-side JS via Basic Auth. Login page sets a session cookie for convenience but JS uses Base64-encoded Basic Auth headers directly.
- **Session management** — In-memory `ConcurrentDictionary` with 24h expiry. Logout clears session + expires cookie.
- **Read-only enforcement** — Per-user read-only flag checked server-side before write operations (PUT, DELETE, MKCOL, MOVE, COPY, PROPPATCH).
- **Template caching** — Static fields with null-coalescing assignment (`_template ??= LoadWebTemplate("file.html")`). No cache invalidation — requires service restart.
- **Health check** — Timer runs every 20 min, logs uptime, directory status, cert validity.
- **Port conflict** — Old service processes linger. Use a different port or clean via Configurator uninstall.
- **AppInfo sharing** — `AppInfo.cs` lives in WebDAVService project, linked from WebDAVConfigurator csproj via `<Compile Include="..\WebDAVService\AppInfo.cs" Link="AppInfo.cs" />`.
- **WPF/WinForms interop naming conflicts** — Both `UseWPF` and `UseWindowsForms` set in csproj (for `FolderBrowserDialog`). This causes ambiguity between `System.Windows.MessageBox` and `System.Windows.Forms.MessageBox`, and between `System.Windows.Media.Brushes` and `System.Drawing.Brushes`. Always use fully qualified names: `System.Windows.MessageBox.Show()` and `System.Windows.Media.Brushes.LightGreen`.
- **System.IO not in implicit usings** — WPF projects with `UseWPF=true` do not auto-import `System.IO`. Files using `File`, `Path`, or `FileNotFoundException` must add `using System.IO;` explicitly.

## Web Template Development

When modifying `browse.html`:
1. Edit the source file at `src/WebDAVService/web/browse.html`
2. Rebuild and publish, then copy to deployment dirs (see Build & Publish)
3. Restart the service to pick up the new template (static field cache)

Key SPA client-side patterns:
- `Dav` object handles all WebDAV communication (PROPFIND XML parsing, fetch/XHR)
- `U.fico()` maps file extensions to icons + hash-based HSL colors for unknown types
- `A` object manages app state: file list, breadcrumbs, search, selection, preview
- All authenticated requests use `btoa(username + ":" + password)` for Basic Auth
- The SPA has its own login view that POSTs JSON to `/login` to create a session cookie

## Common Issues

- **Service won't start**: Check `logs/service.log`. Missing `UseWindowsService()` causes immediate exit.
- **Port conflict**: Old service process still running. Configurator → Restart Service, or `sc stop WebDAVService`.
- **Stack overflow on startup**: Config loading loop — ensure JSON deserialization doesn't call config class constructor (use static factory).
- **HTTPS fails**: Check logs for cert binding errors. Requires admin for `netsh http add sslcert`. Self-signed cert triggers browser warning.
- **Web template changes not showing**: Templates are cached in static fields. Service restart required.
- **Mkdir/delete fails**: Check if user account is configured as read-only (`readonly: true` in config).
