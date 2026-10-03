#!/usr/bin/env bash
set -u

echo "NRS Workbench · Linux host check"
echo "================================"

echo
echo "[OS]"
if [[ -r /etc/os-release ]]; then
  # Only print the conventional identification fields, not arbitrary environment data.
  grep -E '^(NAME|VERSION|VERSION_ID|ID|ID_LIKE)=' /etc/os-release || true
else
  echo "/etc/os-release not found"
fi

echo
echo "[Kernel / architecture]"
uname -srmo 2>/dev/null || uname -a
printf 'machine: '
uname -m

echo
echo "[Init / systemd]"
if command -v systemctl >/dev/null 2>&1; then
  echo "systemctl: $(command -v systemctl)"
  systemctl --version 2>/dev/null | head -n 1 || true
  if [[ -d /run/systemd/system ]]; then
    echo "systemd-runtime: present"
  else
    echo "systemd-runtime: not detected"
  fi
else
  echo "systemctl: not found"
fi

echo
echo "[Git]"
if command -v git >/dev/null 2>&1; then
  git --version
else
  echo "git: not found"
fi

echo
echo "[.NET]"
if command -v dotnet >/dev/null 2>&1; then
  dotnet --version
  dotnet --list-runtimes 2>/dev/null || true
else
  echo "dotnet: not found"
fi

echo
echo "[Desktop session]"
if [[ -n "${XDG_CURRENT_DESKTOP:-}" ]]; then
  echo "desktop: ${XDG_CURRENT_DESKTOP}"
elif [[ -n "${DESKTOP_SESSION:-}" ]]; then
  echo "desktop: ${DESKTOP_SESSION}"
else
  echo "desktop: not detected (headless or non-graphical session)"
fi

echo
echo "[NRS Workbench platform prerequisites]"
missing=0
if ! command -v git >/dev/null 2>&1; then
  echo "MISSING: git"
  missing=1
fi
if ! command -v systemctl >/dev/null 2>&1; then
  echo "MISSING: systemctl (systemd-first spike)"
  missing=1
fi
if ! command -v dotnet >/dev/null 2>&1; then
  echo "MISSING: dotnet"
  missing=1
fi
if [[ "$missing" -eq 0 ]]; then
  echo "Foundation prerequisites detected."
else
  echo "One or more foundation prerequisites are not installed yet."
fi

echo
echo "No changes were made to this host."
