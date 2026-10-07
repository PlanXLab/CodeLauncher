# DevEnv

DevEnv is a portable Windows development environment installer and updater. The current repository is built around two WinForms executables:

- `devenv-setup.exe`: initial installation
- `launcher.exe`: launch, version checks, and upgrades

The final distributable is `devenv-setup.exe` only. `launcher.exe` is embedded into the installer and extracted into the target folder during installation.

## Download

- Latest release page: https://github.com/PlanXLab/DevEnv/releases/latest
- Latest `devenv-setup.exe`: https://github.com/PlanXLab/DevEnv/releases/latest/download/devenv-setup.exe

## Current Architecture

### Installer

`devenv-setup.exe` installs the latest available versions of the following components:

- VS Code
- Python Embedded
- PowerShell 7
- Oh My Posh
- PowerShell modules: `Terminal-Icons`, `PSFzf`, `modern-unix-win`
- Fonts: `0xProto Nerd Font`, `DalseoHealing`
- VS Code extensions:
  - `teabyii.ayu`
  - `zhuangtongfa.material-theme`
  - `ms-python.python`
  - `ms-python.vscode-pylance`
  - `ms-vscode-remote.remote-ssh`
  - `KevinRose.vsc-python-indent`
  - `usernamehw.errorlens`
  - `Gerrnperl.outline-map`

Installer UI behavior:

- Default installation path is `C:\VSCode`
- The Python version field can be left empty to install the latest supported version
- Pressing `Enter` in the Python version field starts installation immediately
- The UI provides overall progress, per-component progress, and a filtered Key Events log

### Launcher

`launcher.exe` is the runtime entry point for an installed environment.

- Launches `Code.exe` in portable mode
- Checks component versions in the background
- Creates `upgrade_*` flag files when updates are available
- Shows the upgrade UI on the next run and upgrades only the required individual components

Upgradeable components:

- PowerShell 7
- Oh My Posh
- Terminal-Icons
- PSFzf
- modern-unix-win
- VS Code

### Version and State Files

The installation root contains the following runtime files:

- `pvs.info`: installation path and installed component versions
- `pvs.log`: installation log
- `upgrade.log`: upgrade log
- `upgrade_*`: launcher-generated upgrade flag files

## Installed Folder Layout

A typical installed layout looks like this:

```text
<install-root>/
├─ Code.exe
├─ launcher.exe
├─ pvs.info
├─ pvs.log
├─ upgrade.log
├─ data/
│  ├─ extensions/
│  ├─ user-data/
│  └─ lib/
│     ├─ fonts/
│     ├─ pwsh/
│     ├─ python/
│     └─ origin/
└─ resources/
```

## Usage

### End Users

1. Download `devenv-setup.exe` from GitHub Releases.
2. Run it and choose the installation path and Python version.
3. After installation, launch VS Code through `launcher.exe` or the created shortcut.
4. From that point on, `launcher.exe` handles background version checks and upgrade entry.

### Maintenance Mode

`launcher.exe --init` runs the internal maintenance routine.

- Synchronizes the VS Code extension set
- Restores configuration and origin files
- Cleans the Python environment
- Reinstalls fonts and recreates shortcuts
- Cleans user-data and launches VS Code

## Terminal (PowerShell 7)

The integrated terminal runs **PowerShell 7** with a custom profile that provides a Unix-like experience on Windows. Type `?` at the prompt to list all available commands.

### Navigation

| Command | Description |
|---------|-------------|
| `..` | Go up one directory |
| `...` | Go up two directories |
| `....` | Go up three directories |
| `z <dir>` | Jump to a frequently visited directory (zoxide) |
| `zi` | Interactively select a directory to jump to (zoxide + fzf) |
| `<dirname>` | Change into a directory by typing its name directly |

### File and Directory Listing

| Command | Alias for | Description |
|---------|-----------|-------------|
| `ls` | `lsd` | Colorized directory listing with icons |
| `ll` | `lsd -l` | Long format listing |
| `la` | `lsd -lall` | Long format including hidden files |
| `cat <file>` | `bat` | Syntax-highlighted file viewer with Git integration |
| `find` | `fd` | Fast, user-friendly alternative to `find` |
| `du` | `dust` | Disk usage with visual tree |
| `df` | `duf` | Disk free space overview |

### Text Processing

| Command | Alias for | Description |
|---------|-----------|-------------|
| `grep <pattern>` | `rg` | Recursive regex search (ripgrep) |
| `sed` | `sd` | Intuitive find-and-replace |
| `jq` | — | Command-line JSON processor |
| `delta` | — | Syntax-highlighted diff viewer |

### System and Process Monitoring

| Command | Alias for | Description |
|---------|-----------|-------------|
| `ps` | `procs` | Modern process list |
| `top` | `btm` | Interactive graphical process/system monitor |
| `ping <host>` | `gping` | Ping with a real-time graph |

### Network

| Command | Alias for | Description |
|---------|-----------|-------------|
| `http` | `xh` | Send HTTP requests (HTTPie-style) |
| `dig <host>` | `dog` | DNS lookup |
| `curlie` | — | curl with httpie-style output |

### Miscellaneous Tools

| Command | Description |
|---------|-------------|
| `fzf` | Interactive fuzzy finder |
| `broot` | Interactive tree explorer and launcher |
| `cheat <cmd>` | View community cheatsheets for a command |
| `hyperfine <cmd>` | Command-line benchmarking |
| `which <cmd>` | Show the full path of a command |
| `clh` | Clear all terminal history (file, buffer, and session) |

### Keyboard Shortcuts (PSReadLine / Emacs mode)

| Key | Action |
|-----|--------|
| `Ctrl+A` | Move to beginning of line |
| `Ctrl+E` | Move to end of line |
| `Alt+B` | Move backward one word |
| `Alt+F` | Move forward one word |
| `Ctrl+U` | Delete to beginning of line |
| `Ctrl+K` | Delete to end of line |
| `Tab` | Menu-style autocomplete |
| `Ctrl+R` | Fuzzy search command history (fzf) |
| `Ctrl+T` | Fuzzy file picker (fzf) |
| `↑` / `↓` | Navigate history predictions (inline list view) |

### Running Python Scripts

Python scripts can be run directly from the terminal without typing `python`:

```powershell
foo.py          # runs: python foo.py
./foo.py        # runs: python ./foo.py
./ch1/foo.py    # runs: python ./ch1/foo.py
```

If the first line of the script is exactly `#!replx`, it is executed with `replx` instead:

```python
#!replx
print("Hello from replx")
```

```powershell
foo.py    # runs: replx foo.py
```

