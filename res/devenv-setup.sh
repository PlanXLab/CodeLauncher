#!/bin/bash
# devenv-setup.sh — macOS development environment setup (DevEnv)
# https://github.com/PlanXLab/DevEnv
#
# Usage:
#   sh -c "$(curl -fsSL https://raw.githubusercontent.com/PlanXLab/DevEnv/main/res/devenv-setup.sh)"
#
# Prompts:
#   Install folder  (default: ~/VSCode)   — e.g. ~/My/MinX installs to ~/My/MinX/
#   Python version  (default: 3.12)       — latest patch is resolved automatically
#
# Installed folder layout (mirrors Windows DevEnv structure):
#   <install>/
#   ├── Visual Studio Code.app
#   ├── pvs.info
#   └── data/
#       ├── extensions/        (VS Code portable extensions)
#       ├── user-data/         (VS Code portable user data)
#       └── lib/
#           ├── fonts/
#           └── python/ -> ~/.pyenv/versions/<X.Y.Z>   (symlink)

# Re-exec with bash if invoked via sh
[ -z "$BASH_VERSION" ] && exec bash "$0" "$@"

set -e

# ---------------------------------------------------------------------------
# Interactive setup prompts
# ---------------------------------------------------------------------------
echo ""
echo "=== DevEnv macOS Setup ==="
echo ""
read -r -p "  Install folder [~/VSCode]: " _VSCODE_HOME_INPUT
_VSCODE_HOME_INPUT="${_VSCODE_HOME_INPUT:-~/VSCode}"
VSCODE_HOME="${_VSCODE_HOME_INPUT/#\~/$HOME}"

read -r -p "  Python version [3.12]: " _PY_MINOR_INPUT
PY_MINOR="${_PY_MINOR_INPUT:-3.12}"
echo ""

RES="https://raw.githubusercontent.com/PlanXLab/DevEnv/main/res"

# Paths
# (VSCODE_HOME is set by the prompt above)
VSCODE_APP="$VSCODE_HOME/Visual Studio Code.app"
VSCODE_CLI="$VSCODE_APP/Contents/Resources/app/bin/code"
VSCODE_DATA="$VSCODE_HOME/data"
VSCODE_EXTENSIONS="$VSCODE_DATA/extensions"
VSCODE_USER_DATA="$VSCODE_DATA/user-data"
VSCODE_FONTS="$VSCODE_DATA/lib/fonts"
VSCODE_PYTHON="$VSCODE_DATA/lib/python"
ZSHRC="$HOME/.zshrc"
P10K_CONFIG="$HOME/.p10k.zsh"

step() { echo ""; echo "==> $*"; }
info() { echo "    $*"; }
warn() { echo "    WARNING: $*"; }

brew_install() {
  local pkg="$1"
  if brew list --formula "$pkg" &>/dev/null 2>&1; then
    info "Already installed: $pkg"
  else
    brew install "$pkg" || warn "Failed to install $pkg (skipping)"
  fi
}

# ---------------------------------------------------------------------------
# Homebrew
# ---------------------------------------------------------------------------
step "Checking Homebrew"
if ! command -v brew &>/dev/null; then
  echo "    Installing Homebrew..."
  /bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"
fi
if [[ -f /opt/homebrew/bin/brew ]]; then
  eval "$(/opt/homebrew/bin/brew shellenv)"
elif [[ -f /usr/local/bin/brew ]]; then
  eval "$(/usr/local/bin/brew shellenv)"
fi

# ---------------------------------------------------------------------------
# VS Code — portable mode (mirrors Windows layout)
# ---------------------------------------------------------------------------
step "Setting up VS Code portable at $VSCODE_HOME"
mkdir -p "$VSCODE_HOME"

if [[ ! -d "$VSCODE_APP" ]]; then
  info "Downloading VS Code (universal)..."
  VSCODE_ZIP="/tmp/devenv-vscode.zip"
  curl -fsSL -L "https://update.code.visualstudio.com/latest/darwin-universal/stable" \
    -o "$VSCODE_ZIP"
  info "Extracting..."
  unzip -q "$VSCODE_ZIP" -d "$VSCODE_HOME"
  rm -f "$VSCODE_ZIP"
  xattr -dr com.apple.quarantine "$VSCODE_APP" 2>/dev/null || true
  info "Installed: $VSCODE_APP"
else
  info "Already installed: $VSCODE_APP"
fi

# Create portable data/ directory structure (activates VS Code portable mode)
# Note: $VSCODE_PYTHON is created/replaced by the Python install step below
mkdir -p "$VSCODE_EXTENSIONS" "$VSCODE_USER_DATA" "$VSCODE_FONTS" "$(dirname "$VSCODE_PYTHON")"

# Make code CLI available for this session
export PATH="$VSCODE_APP/Contents/Resources/app/bin:$PATH"

# ---------------------------------------------------------------------------
# Python — install python-build-standalone directly into portable layout
# Fully self-contained: no system python dependency, move folder freely
# ---------------------------------------------------------------------------
step "Installing Python $PY_MINOR (portable)"

# Resolve latest patch version via endoflife.date
info "Fetching latest $PY_MINOR.x release..."
PY_VERSION=$(python3 -c "
import urllib.request, json, sys
try:
    with urllib.request.urlopen(
        'https://endoflife.date/api/python/' + sys.argv[1] + '.json', timeout=10
    ) as r:
        print(json.loads(r.read())['latest'])
except Exception:
    print(sys.argv[2])
" "$PY_MINOR" "$PY_MINOR.0" 2>/dev/null || echo "$PY_MINOR.0")
info "Target: Python $PY_VERSION"

# Detect architecture
ARCH=$(uname -m)
[[ "$ARCH" == "arm64" ]] && PY_ARCH="aarch64-apple-darwin" || PY_ARCH="x86_64-apple-darwin"
PY_PATTERN="cpython-${PY_VERSION}+.*-${PY_ARCH}-install_only\\.tar\\.gz"

# Find download URL from python-build-standalone GitHub releases
info "Searching python-build-standalone release..."
PY_ASSET_URL=$(python3 -c "
import urllib.request, json, re, sys
try:
    req = urllib.request.Request(
        'https://api.github.com/repos/astral-sh/python-build-standalone/releases',
        headers={'Accept': 'application/vnd.github.v3+json', 'User-Agent': 'DevEnv/1.0'})
    with urllib.request.urlopen(req, timeout=15) as r:
        releases = json.loads(r.read())
    pat = re.compile(sys.argv[1])
    for release in releases[:5]:
        for asset in release.get('assets', []):
            if pat.match(asset['name']):
                print(asset['browser_download_url'])
                sys.exit(0)
    sys.exit(1)
except Exception as e:
    print(f'Error: {e}', file=sys.stderr)
    sys.exit(1)
" "$PY_PATTERN" 2>/dev/null) || { warn "Python $PY_VERSION asset not found; skipping"; PY_ASSET_URL=""; }

if [[ -n "${PY_ASSET_URL:-}" ]]; then
  info "Downloading Python $PY_VERSION ($PY_ARCH)..."
  PY_TMP_TAR="/tmp/devenv-python.tar.gz"
  PY_TMP_DIR="/tmp/devenv-python-extract"
  curl -fsSL -L "$PY_ASSET_URL" -o "$PY_TMP_TAR"
  info "Extracting to $VSCODE_PYTHON..."
  rm -rf "$PY_TMP_DIR" && mkdir -p "$PY_TMP_DIR"
  tar -xzf "$PY_TMP_TAR" -C "$PY_TMP_DIR"
  rm -f "$PY_TMP_TAR"
  # Atomic swap: extract → .new → active ← .old → remove
  # Mirrors launcher.sh upgrade pattern; protects concurrent reads
  rm -rf "${VSCODE_PYTHON}.new"
  mv "$PY_TMP_DIR/python" "${VSCODE_PYTHON}.new"
  rm -rf "${VSCODE_PYTHON}.old"
  [[ -d "$VSCODE_PYTHON" ]] && mv "$VSCODE_PYTHON" "${VSCODE_PYTHON}.old"
  mv "${VSCODE_PYTHON}.new" "$VSCODE_PYTHON"
  rm -rf "${VSCODE_PYTHON}.old" "$PY_TMP_DIR"
  info "Python $PY_VERSION → $VSCODE_PYTHON/bin/python3"
else
  PY_VERSION="(not installed)"
fi

# ---------------------------------------------------------------------------
# Modern Unix tools  (same set as Windows modern-unix-win)
# ---------------------------------------------------------------------------
step "Installing modern Unix tools"
for pkg in \
  bat lsd fd ripgrep dog procs bottom dust duf gping xh sd \
  zoxide delta broot jq hyperfine fzf curlie
do
  brew_install "$pkg"
done

# fzf shell key-bindings
FZF_INSTALL="$(brew --prefix)/opt/fzf/install"
[[ -f "$FZF_INSTALL" ]] && \
  "$FZF_INSTALL" --all --no-update-rc --no-bash --no-fish 2>/dev/null || true

# ---------------------------------------------------------------------------
# oh-my-zsh
# ---------------------------------------------------------------------------
step "Installing oh-my-zsh"
if [[ ! -d "$HOME/.oh-my-zsh" ]]; then
  RUNZSH=no CHSH=no KEEP_ZSHRC=yes \
    sh -c "$(curl -fsSL https://raw.githubusercontent.com/ohmyzsh/ohmyzsh/master/tools/install.sh)"
  info "oh-my-zsh installed"
else
  info "Already installed"
fi

# powerlevel10k theme
P10K_DIR="${ZSH_CUSTOM:-$HOME/.oh-my-zsh/custom}/themes/powerlevel10k"
if [[ ! -d "$P10K_DIR" ]]; then
  git clone --depth=1 https://github.com/romkatv/powerlevel10k.git "$P10K_DIR"
  info "powerlevel10k installed"
else
  info "powerlevel10k: already installed"
fi

# zsh-autosuggestions (fish-style inline history predictions)
AUTOSUGGEST_DIR="${ZSH_CUSTOM:-$HOME/.oh-my-zsh/custom}/plugins/zsh-autosuggestions"
if [[ ! -d "$AUTOSUGGEST_DIR" ]]; then
  git clone https://github.com/zsh-users/zsh-autosuggestions "$AUTOSUGGEST_DIR"
  info "zsh-autosuggestions installed"
else
  info "zsh-autosuggestions: already installed"
fi

# ---------------------------------------------------------------------------
# Fonts
# ---------------------------------------------------------------------------
step "Installing fonts"
SYS_FONT_DIR="$HOME/Library/Fonts"
mkdir -p "$SYS_FONT_DIR"

info "Downloading 0xProto Nerd Font..."
NERD_ZIP="/tmp/devenv-0xProto.zip"
curl -fsSL -L \
  "https://github.com/ryanoasis/nerd-fonts/releases/latest/download/0xProto.zip" \
  -o "$NERD_ZIP"
unzip -o "$NERD_ZIP" -d "$SYS_FONT_DIR" '*.ttf' '*.otf' 2>/dev/null || true
cp "$SYS_FONT_DIR"/0xProto*.ttf "$VSCODE_FONTS/" 2>/dev/null || true
rm -f "$NERD_ZIP"

if curl --head --silent --fail "$RES/DalseoHealingMedium.ttf" &>/dev/null; then
  curl -fsSL "$RES/DalseoHealingMedium.ttf" -o "$SYS_FONT_DIR/DalseoHealingMedium.ttf"
  cp "$SYS_FONT_DIR/DalseoHealingMedium.ttf" "$VSCODE_FONTS/" 2>/dev/null || true
fi

# ---------------------------------------------------------------------------
# VS Code extensions
# ---------------------------------------------------------------------------
step "Installing VS Code extensions"
if [[ -x "$VSCODE_CLI" ]]; then
  for ext in \
    teabyii.ayu \
    zhuangtongfa.material-theme \
    ms-python.python \
    ms-python.vscode-pylance \
    ms-python.vscode-python-envs \
    ms-vscode-remote.remote-ssh \
    KevinRose.vsc-python-indent \
    usernamehw.errorlens \
    Gerrnperl.outline-map
  do
    "$VSCODE_CLI" \
      --extensions-dir "$VSCODE_EXTENSIONS" \
      --user-data-dir  "$VSCODE_USER_DATA"  \
      --install-extension "$ext" --force \
      && info "OK: $ext" || warn "Failed: $ext"
  done
else
  warn "VS Code CLI not found at: $VSCODE_CLI"
fi

# ---------------------------------------------------------------------------
# VS Code settings  (portable: ~/VSCode/data/user-data/User/settings.json)
# python.defaultInterpreterPath matches Windows path convention via ${userHome}
# ---------------------------------------------------------------------------
step "Applying VS Code settings"
SETTINGS_DIR="$VSCODE_USER_DATA/User"
mkdir -p "$SETTINGS_DIR"
SETTINGS="$SETTINGS_DIR/settings.json"
[[ -f "$SETTINGS" ]] && cp "$SETTINGS" "${SETTINGS}.bak"

cat > "$SETTINGS" << 'SETTINGS_EOF'
{
  "files.autoSave": "onFocusChange",
  "files.autoSaveDelay": 500,
  "files.exclude": {
    "**/__pycache__": true,
    "**/.venv": true,
    "**/.vscode": true,
    "**/.temp": true,
    "**/.git": true
  },
  "files.watcherExclude": {
    "**/.git/objects/**": true,
    "**/.git/subtree-cache/**": true,
    "**/dist/**": true,
    "**/build/**": true,
    "**/.vscode/**": true,
    "**/.cache/**": true,
    "**/__pycache__/**": true,
    "**/.venv/**": true
  },
  "search.exclude": {
    "**/dist/**": true,
    "**/build/**": true,
    "**/__pycache__/**": true,
    "**/.venv/**": true,
    "**/.git/**": true
  },
  "editor.mouseWheelZoom": true,
  "editor.fontFamily": "'0xProto Nerd Font', DalseoHealing",
  "editor.minimap.enabled": false,
  "editor.renderWhitespace": "boundary",
  "editor.cursorSmoothCaretAnimation": "on",
  "editor.smoothScrolling": true,

  "workbench.colorTheme": "One Dark Pro Darker",
  "workbench.iconTheme": "ayu",
  "workbench.startupEditor": "none",
  "workbench.tree.indent": 22,

  "security.workspace.trust.untrustedFiles": "newWindow",
  "window.commandCenter": false,

  "terminal.integrated.mouseWheelZoom": true,
  "terminal.integrated.fontSize": 14,
  "terminal.explorerKind": "integrated",
  "terminal.integrated.suggest.enabled": false,
  "terminal.integrated.minimumContrastRatio": 1,

  "python.defaultInterpreterPath": "__PYTHON_INTERPRETER_PATH__",
  "python.terminal.activateEnvironment": false,
  "python.venvFolders": [],
  "python.useEnvironmentsExtension": true,
  "python.createEnvironment.trigger": "off",
  "python.analysis.diagnosticSeverityOverrides": {
    "reportMissingModuleSource": "none"
  },
  "python.analysis.exclude": [
    "**/__pycache__",
    "**/.venv",
    "**/.vscode",
    "**/build",
    "**/dist",
    "**/.git"
  ],

  "explorer.confirmDelete": false,
  "explorer.confirmPasteNative": false
}
SETTINGS_EOF
# Use VS Code's portable-mode env var for true folder-portability:
#   $VSCODE_PORTABLE is auto-set by Code.app to <install>/data when the data/
#   directory exists alongside the .app. Move/rename the folder freely.
sed -i '' \
  's|"__PYTHON_INTERPRETER_PATH__"|"${env:VSCODE_PORTABLE}/lib/python/bin/python3"|' \
  "$SETTINGS"

# ---------------------------------------------------------------------------
# pvs.info  (runtime state file, matches Windows convention)
# ---------------------------------------------------------------------------
VSCODE_VERSION_INSTALLED=$("$VSCODE_CLI" --version 2>/dev/null | head -1 || echo "unknown")
cat > "$VSCODE_HOME/pvs.info" << PVSINFO_EOF
INSTALL_DIR=$VSCODE_HOME
INSTALL_DATE=$(date -u +%Y-%m-%dT%H:%M:%SZ)
PLATFORM=macOS
VSCODE_VERSION=$VSCODE_VERSION_INSTALLED
PYTHON_VERSION=$PY_VERSION
PYTHON_PATH=$VSCODE_DATA/lib/python/bin/python3
PVSINFO_EOF

# ---------------------------------------------------------------------------
# launcher.sh  (portable launcher + auto-updater, matches Windows launcher.exe)
# ---------------------------------------------------------------------------
step "Installing launcher.sh"
if curl -fsSL "$RES/launcher.sh" -o "$VSCODE_HOME/launcher.sh" 2>/dev/null; then
  chmod +x "$VSCODE_HOME/launcher.sh"
  info "launcher.sh installed at $VSCODE_HOME/launcher.sh"
else
  warn "Could not download launcher.sh from $RES/launcher.sh"
fi

# ---------------------------------------------------------------------------
# ~/.p10k.zsh  — tos-term compatible theme for powerlevel10k
# Matches Windows tos-term.omp.json:
#   Left:  OS icon (orange) > username (gray) > path (blue) > git (green/yellow)
#   Right: execution time (gray) + time (orange)
#   Line2: ➜ (orange)
# ---------------------------------------------------------------------------
step "Writing powerlevel10k theme (~/.p10k.zsh)"
cat > "$P10K_CONFIG" << 'P10K_EOF'
# ~/.p10k.zsh — DevEnv tos-term compatible theme
# Auto-generated by devenv-setup.sh
'builtin' 'local' '-a' 'p10k_config_opts'
[[ ! -o 'aliases'         ]] || p10k_config_opts+=('aliases')
[[ ! -o 'sh_glob'         ]] || p10k_config_opts+=('sh_glob')
[[ ! -o 'no_brace_expand' ]] || p10k_config_opts+=('no_brace_expand')
'builtin' 'setopt' 'no_aliases' 'no_sh_glob' 'brace_expand'

() {
  emulate -L zsh -o extended_glob

  # Prompt elements (matches tos-term layout)
  typeset -g POWERLEVEL9K_LEFT_PROMPT_ELEMENTS=(
    os_icon       # OS icon  — orange diamond  (tos-term: leading segment)
    context       # username — gray box
    dir           # path     — blue box (max 3 levels)
    vcs           # git      — green/yellow/purple
    newline
    prompt_char   # ➜        — orange, second line
  )
  typeset -g POWERLEVEL9K_RIGHT_PROMPT_ELEMENTS=(
    command_execution_time  # elapsed — gray  (tos-term: right)
    time                    # clock   — orange (tos-term: right)
  )

  typeset -g POWERLEVEL9K_PROMPT_ADD_NEWLINE=false
  typeset -g POWERLEVEL9K_MODE=nerdfont-v3

  # Powerline separators (matches tos-term \ue0b4 style)
  typeset -g POWERLEVEL9K_LEFT_SEGMENT_SEPARATOR=$'\ue0b4'
  typeset -g POWERLEVEL9K_LEFT_SUBSEGMENT_SEPARATOR=$'\ue0b5'
  typeset -g POWERLEVEL9K_RIGHT_SEGMENT_SEPARATOR=$'\ue0b6'
  typeset -g POWERLEVEL9K_RIGHT_SUBSEGMENT_SEPARATOR=$'\ue0b7'

  # ── OS icon  (#d75f00 orange, matches tos-term) ──────────────────────────
  typeset -g POWERLEVEL9K_OS_ICON_FOREGROUND=255
  typeset -g POWERLEVEL9K_OS_ICON_BACKGROUND=208        # #d75f00

  # ── Context / username  (#e4e4e4 gray, matches tos-term) ─────────────────
  typeset -g POWERLEVEL9K_CONTEXT_DEFAULT_FOREGROUND=238   # #4e4e4e
  typeset -g POWERLEVEL9K_CONTEXT_DEFAULT_BACKGROUND=254   # #e4e4e4
  typeset -g POWERLEVEL9K_CONTEXT_ROOT_FOREGROUND=238
  typeset -g POWERLEVEL9K_CONTEXT_ROOT_BACKGROUND=254
  typeset -g POWERLEVEL9K_CONTEXT_SUDO_FOREGROUND=238
  typeset -g POWERLEVEL9K_CONTEXT_SUDO_BACKGROUND=254
  typeset -g POWERLEVEL9K_CONTEXT_TEMPLATE='%n'
  typeset -g POWERLEVEL9K_ALWAYS_SHOW_CONTEXT=true

  # ── Dir / path  (#0087af blue, max 3 levels, matches tos-term) ───────────
  typeset -g POWERLEVEL9K_DIR_FOREGROUND=255
  typeset -g POWERLEVEL9K_DIR_BACKGROUND=31              # #0087af
  typeset -g POWERLEVEL9K_DIR_SHORTENED_FOREGROUND=250
  typeset -g POWERLEVEL9K_DIR_ANCHOR_FOREGROUND=255
  typeset -g POWERLEVEL9K_SHORTEN_STRATEGY=truncate_to_unique
  typeset -g POWERLEVEL9K_SHORTEN_DIR_LENGTH=3
  typeset -g POWERLEVEL9K_DIR_MAX_LENGTH=3
  typeset -g POWERLEVEL9K_DIR_SEPARATOR=' '$'\ue0b1'' '

  # ── VCS / git  (green clean / yellow dirty / purple ahead-behind) ────────
  typeset -g POWERLEVEL9K_VCS_BRANCH_ICON=$'\uf418 '
  typeset -g POWERLEVEL9K_VCS_UNTRACKED_ICON='?'
  typeset -g POWERLEVEL9K_VCS_CLEAN_FOREGROUND=255
  typeset -g POWERLEVEL9K_VCS_CLEAN_BACKGROUND=28        # #378504 green
  typeset -g POWERLEVEL9K_VCS_MODIFIED_FOREGROUND=255
  typeset -g POWERLEVEL9K_VCS_MODIFIED_BACKGROUND=136    # #a97400 amber
  typeset -g POWERLEVEL9K_VCS_UNTRACKED_FOREGROUND=255
  typeset -g POWERLEVEL9K_VCS_UNTRACKED_BACKGROUND=136
  typeset -g POWERLEVEL9K_VCS_CONFLICTED_FOREGROUND=255
  typeset -g POWERLEVEL9K_VCS_CONFLICTED_BACKGROUND=196
  typeset -g POWERLEVEL9K_VCS_AHEAD_BEHIND_FOREGROUND=255
  typeset -g POWERLEVEL9K_VCS_AHEAD_BEHIND_BACKGROUND=97 # #744d89 purple

  # ── Prompt char ➜  (orange, matches tos-term second-line arrow) ───────────
  typeset -g POWERLEVEL9K_PROMPT_CHAR_OK_VIINS_CONTENT_EXPANSION='➜'
  typeset -g POWERLEVEL9K_PROMPT_CHAR_OK_VICMD_CONTENT_EXPANSION='➜'
  typeset -g POWERLEVEL9K_PROMPT_CHAR_OK_VIVIS_CONTENT_EXPANSION='➜'
  typeset -g POWERLEVEL9K_PROMPT_CHAR_ERROR_VIINS_CONTENT_EXPANSION='➜'
  typeset -g POWERLEVEL9K_PROMPT_CHAR_ERROR_VICMD_CONTENT_EXPANSION='➜'
  typeset -g POWERLEVEL9K_PROMPT_CHAR_OK_VIINS_FOREGROUND=208    # orange
  typeset -g POWERLEVEL9K_PROMPT_CHAR_OK_VICMD_FOREGROUND=208
  typeset -g POWERLEVEL9K_PROMPT_CHAR_ERROR_VIINS_FOREGROUND=196 # red on error

  # ── Execution time  (gray, right side, matches tos-term) ─────────────────
  typeset -g POWERLEVEL9K_COMMAND_EXECUTION_TIME_THRESHOLD=0
  typeset -g POWERLEVEL9K_COMMAND_EXECUTION_TIME_PRECISION=0
  typeset -g POWERLEVEL9K_COMMAND_EXECUTION_TIME_FOREGROUND=240  # #585858
  typeset -g POWERLEVEL9K_COMMAND_EXECUTION_TIME_BACKGROUND=254  # #e4e4e4
  typeset -g POWERLEVEL9K_COMMAND_EXECUTION_TIME_FORMAT='duration'

  # ── Time  (orange, rightmost, matches tos-term) ───────────────────────────
  typeset -g POWERLEVEL9K_TIME_FORMAT='%D{%H:%M:%S}'
  typeset -g POWERLEVEL9K_TIME_FOREGROUND=255
  typeset -g POWERLEVEL9K_TIME_BACKGROUND=208                    # #d75f00 orange
  typeset -g POWERLEVEL9K_TIME_UPDATE_ON_COMMAND=false

  typeset -g POWERLEVEL9K_INSTANT_PROMPT=quiet
  typeset -g POWERLEVEL9K_DISABLE_CONFIGURATION_WIZARD=true
}

(( ${#p10k_config_opts} )) && setopt ${p10k_config_opts[@]}
'builtin' 'unset' 'p10k_config_opts'
P10K_EOF

# ---------------------------------------------------------------------------
# ~/.zshrc  — oh-my-zsh header + DevEnv block
# ---------------------------------------------------------------------------
step "Configuring ~/.zshrc"

# Remove previous DevEnv block
if [[ -f "$ZSHRC" ]]; then
  python3 - "$ZSHRC" << 'PYEOF'
import sys, re
content = open(sys.argv[1]).read()
content = re.sub(r'# === DevEnv BEGIN ===.*?# === DevEnv END ===\n?', '', content, flags=re.DOTALL)
open(sys.argv[1], 'w').write(content)
PYEOF
fi

# Write oh-my-zsh header if not already present
if ! grep -q 'oh-my-zsh.sh' "$ZSHRC" 2>/dev/null; then
  cat > "$ZSHRC" << 'ZSHHEADER_EOF'
# p10k instant prompt — must be at the very top of .zshrc
if [[ -r "${XDG_CACHE_HOME:-$HOME/.cache}/p10k-instant-prompt-${(%):-%n}.zsh" ]]; then
  source "${XDG_CACHE_HOME:-$HOME/.cache}/p10k-instant-prompt-${(%):-%n}.zsh"
fi

export ZSH="$HOME/.oh-my-zsh"
ZSH_THEME="powerlevel10k/powerlevel10k"
plugins=(git zsh-autosuggestions)
source "$ZSH/oh-my-zsh.sh"
ZSHHEADER_EOF
fi

# Append DevEnv block
cat >> "$ZSHRC" << 'ZSHRC_EOF'
# === DevEnv BEGIN ===
# DevEnv — macOS development environment
# https://github.com/PlanXLab/DevEnv

# code CLI (VS Code portable at ~/VSCode)
export PATH="$HOME/VSCode/Visual Studio Code.app/Contents/Resources/app/bin:$PATH"

# --- lsd: ls / ll / la (matches Windows ps1) ---
if command -v lsd &>/dev/null; then
  alias ls='lsd'
  alias ll='lsd -l'
  alias la='lsd -lall'
fi

# --- bat: cat with syntax highlighting ---
if command -v bat &>/dev/null; then
  alias cat='bat'
  export BAT_THEME='ansi'
fi

# --- modern Unix aliases (matches Windows ps1 profile) ---
command -v rg    &>/dev/null && alias grep='rg'
command -v fd    &>/dev/null && alias find='fd'
command -v procs &>/dev/null && alias ps='procs'
command -v btm   &>/dev/null && alias top='btm'
command -v dust  &>/dev/null && alias du='dust'
command -v duf   &>/dev/null && alias df='duf'
command -v gping &>/dev/null && alias ping='gping'
command -v xh    &>/dev/null && alias http='xh'
command -v dog   &>/dev/null && alias dig='dog'
command -v sd    &>/dev/null && alias sed='sd'

# --- zoxide: z / zi (smart cd, matches Windows zoxide init pwsh) ---
command -v zoxide &>/dev/null && eval "$(zoxide init zsh)"

# --- fzf key bindings (Ctrl+T: file picker, Ctrl+R: history search) ---
_FZF_SHELL="$(brew --prefix 2>/dev/null)/opt/fzf/shell"
[[ -f "$_FZF_SHELL/key-bindings.zsh" ]] && source "$_FZF_SHELL/key-bindings.zsh"

# --- Navigation shortcuts (matches Windows ps1) ---
alias ..='cd ..'
alias ...='cd ../..'
alias ....='cd ../../..'
setopt AUTO_CD

# --- clh: clear all history (matches Windows clh function) ---
function clh() {
  local histfile="${HISTFILE:-$HOME/.zsh_history}"
  [[ -f "$histfile" ]] && rm -f "$histfile"
  HISTSIZE=0 && HISTSIZE=10000
  echo "History cleared."
}

# --- ?: list available commands (matches Windows ps1 '?' function) ---
function _devenv_help() {
  local -A descs=(
    bat       "A cat(1) clone with syntax highlighting and Git integration"
    broot     "A tree explorer and a customizable launcher"
    btm       "A customizable cross-platform graphical process/system monitor"
    cheat     "Create and view interactive cheatsheets on the command-line"
    curlie    "The power of curl with the ease of use of httpie (curl frontend)"
    delta     "A viewer for git and diff output"
    dog       "Command-line DNS client"
    duf       "Disk Usage/Free Utility - a better df alternative"
    dust      "A more intuitive version of du in rust"
    fd        "A simple, fast and user-friendly alternative to find"
    fzf       "A command-line fuzzy finder"
    gping     "Ping, but with a graph"
    hyperfine "A command-line benchmarking tool"
    jq        "Command-line JSON processor"
    lsd       "An ls command with a lot of pretty colors and some other stuff"
    procs     "A modern replacement for ps"
    rg        "Recursively searches the current directory for a regex pattern"
    sd        "Intuitive find & replace CLI (sed alternative)"
    xh        "A friendly and fast tool for sending HTTP requests"
    zoxide    "A smarter cd command for your terminal"
  )
  local -A als=(
    bat    "cat"  btm   "top"   dog  "dig"   duf   "df"
    dust   "du"   fd    "find"  gping "ping"  lsd   "ls, ll, la"
    procs  "ps"   rg    "grep"  sd   "sed"   xh    "http"
    zoxide "z"
  )
  echo ""
  echo "\033[36mModern Unix Commands (DevEnv macOS):\033[0m"
  printf '\033[90m%s\033[0m\n' "$(printf '=%.0s' {1..80})"
  for cmd in bat broot btm cheat curlie delta dog duf dust fd fzf gping \
             hyperfine jq lsd procs rg sd xh zoxide; do
    command -v "$cmd" &>/dev/null || continue
    printf "  \033[32m%-12s\033[0m" "$cmd"
    [[ -n "${als[$cmd]:-}" ]] && \
      printf "\033[90m[\033[36m%s\033[90m]\033[0m " "${als[$cmd]}"
    printf "\033[90m%s\033[0m\n" "${descs[$cmd]}"
  done
  echo ""
  echo "\033[36mCustom Commands:\033[0m"
  printf '\033[90m%s\033[0m\n' "$(printf '=%.0s' {1..80})"
  printf "  \033[32m%-20s\033[0m\033[90m%s\033[0m\n" \
    "clh"             "Clear all terminal history (file + session)" \
    ".. / ... / ...." "Go up 1 / 2 / 3 directories"
  echo ""
  echo "\033[33mTip: Use --help with any command (e.g., 'bat --help')\033[0m"
}
alias '?'='_devenv_help'

# --- Python script runner: foo.py / ./foo.py / path/to/foo.py ---
# ZLE hook equivalent to Windows ps1 PreCommandLookupAction
_devenv_py_runner() {
  local cmd="${BUFFER%% *}"
  local rest="${BUFFER#"$cmd"}"
  if [[ "$cmd" == *.py ]]; then
    local filepath="$cmd"
    [[ "$filepath" != /* && "$filepath" != ./* && "$filepath" != ../* ]] \
      && filepath="./$cmd"
    if [[ -f "$filepath" ]]; then
      local first_line
      first_line=$(head -1 "$filepath" 2>/dev/null)
      if [[ "$first_line" == "#!replx" ]]; then
        BUFFER="replx $filepath$rest"
      else
        BUFFER="python3 $filepath$rest"
      fi
    fi
  fi
  zle .accept-line
}
zle -N accept-line _devenv_py_runner
# === DevEnv END ===

# p10k config — must be at the end of .zshrc
[[ -f ~/.p10k.zsh ]] && source ~/.p10k.zsh
ZSHRC_EOF

# ---------------------------------------------------------------------------
echo ""
echo "=========================================="
echo " DevEnv setup complete!"
echo "=========================================="
echo ""
echo " Install path : $VSCODE_HOME"
echo " Python        : $PY_VERSION ($VSCODE_DATA/lib/python/bin/python3)"
echo ""
echo " Layout:"
echo "   $VSCODE_HOME/"
echo "   ├── Visual Studio Code.app"
echo "   ├── pvs.info"
echo "   └── data/"
echo "       ├── extensions/"
echo "       ├── user-data/"
echo "       └── lib/"
echo "           ├── fonts/"
echo "           └── python/ → python-build-standalone $PY_VERSION"
echo ""
echo " Launcher:"
echo "   $VSCODE_HOME/launcher.sh           — launch VS Code"
echo "   $VSCODE_HOME/launcher.sh --update  — update VS Code + Python, then launch"
echo ""
echo " Restart your terminal, or run:"
echo "   source ~/.zshrc"
echo ""
echo " To list all available commands:"
echo "   ?"
echo "==========================================="
