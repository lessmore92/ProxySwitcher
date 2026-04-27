# Proxy Switcher

A lightweight Windows desktop utility to quickly switch between multiple proxy configurations with a single action. It applies both system-wide proxy settings (via Registry) and environment variables (`HTTP_PROXY`, `HTTPS_PROXY`, `ALL_PROXY`) for command-line tools.

![Build](https://img.shields.io/badge/platform-Windows%2010%2F11-blue)
![.NET](https://img.shields.io/badge/dotnet-net10.0--windows-blue)

---

## Overview

**Proxy Switcher** is built for developers, DevOps, QA engineers, and anyone working across multiple network environments who needs to toggle proxy settings frequently. Instead of digging through Windows Settings and environment variables, you manage profiles and switch them instantly from the system tray or a compact UI.

---

## Features

- **Multiple Profiles** — Create any number of proxy profiles with name, host, port, and optional scope.
- **System Proxy Control** — Applies / clears Windows Registry proxy settings and notifies the system to pick up changes.
- **Environment Variables** — Sets or clears `HTTP_PROXY`, `HTTPS_PROXY`, and `ALL_PROXY` at both process and user scope.
- **One-Click Switching** — Activate or disable a profile instantly.
- **System Tray** — Fully operable from the tray without opening the window.
- **On-Demand UAC Elevation** — Runs with standard privileges by default; requests elevation only when applying system registry changes.
- **Persistent Storage** — Profiles and the active profile are saved in `%APPDATA%\ProxySwitcher`.
- **Minimize to Tray** — Closing the window hides to tray; the app keeps running in the background.

---

## Architecture

```text
┌─────────────────────────────────────────────────────────────┐
│                        UI Layer                              │
│  MainWindow (WPF)  ◄──►  ProfileFormWindow (WPF)            │
└─────────────────────────────────────────────────────────────┘
                              │
┌─────────────────────────────────────────────────────────────┐
│                      Application                             │
│                           (App.xaml.cs + NotifyIcon Tray)   │
└─────────────────────────────────────────────────────────────┘
                              │
┌─────────────────────────────────────────────────────────────┐
│                   Services / Business Logic                  │
│  ProfileManager (Profiles + Active state)                    │
│  ProxySwitcher (Activation / Deactivation coordinator)     │
│  RegistryProxyHandler (HKCU\Internet Settings)             │
│  EnvironmentProxyHandler (Process + User env vars)           │
│  ElevationHelper (UAC elevation on demand)                   │
└─────────────────────────────────────────────────────────────┘
                              │
┌─────────────────────────────────────────────────────────────┐
│                     Data / Persistence                       │
│  ProfileRepository (JSON + text files in %APPDATA%)        │
└─────────────────────────────────────────────────────────────┘
```

---

## Project Structure

| File | Purpose |
|------|---------|
| `App.xaml` / `App.xaml.cs` | Application startup, shared services, NotifyIcon / tray menu |
| `MainWindow.xaml` / `.xaml.cs` | Main desktop UI listing profiles with Create / Edit / Delete / Activate / Disable actions |
| `UI/ProfileFormWindow.xaml` / `.xaml.cs` | Dialog to create or edit a proxy profile |
| `Models/ProxyProfile.cs` | Profile entity with validation |
| `Models/ProxySettings.cs` | Current system proxy state representation |
| `Data/ProfileRepository.cs` | Reads/writes `profiles.json` and `active_profile.txt` to `%APPDATA%` |
| `Services/ProfileManager.cs` | In-memory profile collection management with change events |
| `Services/ProxySwitcher.cs` | Orchestrates activating / deactivating proxies across handlers |
| `Services/RegistryProxyHandler.cs` | Reads and modifies `ProxEnable` / `ProxyServer` in the Windows Registry |
| `Services/EnvironmentProxyHandler.cs` | Sets `HTTP_PROXY`, `HTTPS_PROXY`, `ALL_PROXY` at process and user scope |
| `Services/ElevationHelper.cs` | Detects admin rights and requests UAC elevation (`runas`) when needed |
| `app.manifest` | Application manifest (`asInvoker`; high-DPI compatible; long-path aware) |

---

## Requirements

- **OS**: Windows 10 / 11 (x64)
- **.NET**: `net10.0-windows` (SDK included)
- **IDE**: Visual Studio or VS Code (with C# / WPF workload)

---

## Getting Started

### Clone & Build

```powershell
git clone https://github.com/lessmore92/ProxySwitcher.git
```

### Run (Debug)

```powershell
dotnet run --project ProxySwitcher.csproj
```

### Publish (single-file, self-contained EXE)

```powershell
dotnet publish ProxySwitcher.csproj `
  -c Release `
  --self-contained true `
  -r win-x64 `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o ./publish
```

Output:
```text
publish/
├── ProxySwitcher.exe   # Ready-to-run single-file executable
└── ProxySwitcher.pdb
```

No runtime installation is required on target machines because the app is **self-contained**.

---

## Usage

### First Launch

1. Run `ProxySwitcher.exe`.
2. The app starts in the system tray (taskbar notification area) with no main window.
3. Right-click the tray icon to:
   - View current status
   - See profiles
   - Disable proxy
   - Open / Exit

### Managing Profiles

- **Open Proxy Switcher** from the tray menu (or double-click the tray icon).
- Click **Create New** to add a profile.
- Enter:
  - Profile Name
  - Proxy Host (IP or domain)
  - Proxy Port (1–65535)
  - Whether to apply to **System Proxy Settings**
  - Whether to set **Environment Variables**

### Switching Profiles

- In the main window, select a profile and click **Activate**, or double-click the profile.
- From the tray menu, directly click any profile name to activate it instantly.

### Disable Proxy

- Click **Disable Proxy** in the tray menu, or click the **Disable Proxy** button in the main window.

---

## Data Storage

All user data is stored locally on the machine under:

```text
%APPDATA%\ProxySwitcher
├── profiles.json
└── active_profile.txt
```

No cloud services are used.

---

## Admin Elevation

Modifying the Windows system proxy registry key requires administrator privileges. The app detects when elevation is needed and asks whether to restart itself as administrator. The `ElevationHelper` uses the `runas` verb to trigger the UAC prompt only when required.

---

## Tech Stack

- **WPF** (.NET) — Main UI windows
- **Windows Forms (NotifyIcon)** — System tray icon and context menu
- **Win32 Interop** — `InternetSetOption` (via `wininet.dll`) to broadcast proxy setting changes
- **JSON** — Lightweight profile persistence

---

## License

MIT
