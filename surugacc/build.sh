#!/bin/sh
# IsuSDK (ISDK) のプロジェクトを SurugaCC でビルドする (surugacc/build.ps1 を PowerShell 7.4 以降で動かす)。
#   surugacc/build.sh [プロジェクトのフォルダ] [-g] [-O 0..3] [-j N] [-LibOnly]
here=$(cd "$(dirname "$0")" && pwd -P)
if ! command -v pwsh >/dev/null 2>&1; then
    echo "build.sh: PowerShell 7.4 以降 (pwsh) が要ります: https://aka.ms/powershell" >&2
    exit 1
fi
exec pwsh -NoProfile -File "$here/build.ps1" "$@"
