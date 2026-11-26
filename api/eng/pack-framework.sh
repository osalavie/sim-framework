#!/usr/bin/env bash
# 打包框架 NuGet 包，供业务仓按版本引用。
# 用法：bash api/eng/pack-framework.sh [version]
set -euo pipefail

api_root="$(cd "$(dirname "$0")/.." && pwd)"
version="${1:-1.0.0}"
output="${api_root}/artifacts/nuget"

mkdir -p "${output}"
dotnet pack "${api_root}/Sim.slnx" -c Release -o "${output}" -p:Version="${version}"

echo "NuGet packages written to ${output}"
