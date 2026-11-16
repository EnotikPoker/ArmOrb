#!/usr/bin/env bash
set -euo pipefail
project_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
unity_editor="${UNITY_EDITOR:-$HOME/Unity/Hub/Editor/6000.4.0f1/Editor/Unity}"
if [[ ! -x "$unity_editor" ]]; then
    echo 'Set UNITY_EDITOR to the executable for Unity 6000.4.0f1.' >&2
    exit 1
fi
mkdir -p "$project_dir/TestResults" "$project_dir/Logs"
case "${1:-}" in
    test)
        "$unity_editor" -batchmode -nographics -projectPath "$project_dir" \
            -runTests -testPlatform EditMode -testResults "$project_dir/TestResults/editmode.xml" \
            -logFile "$project_dir/Logs/tests.log"
        ;;
    android)
        "$unity_editor" -batchmode -nographics -quit -projectPath "$project_dir" -buildTarget Android \
            -executeMethod ArmOrb.Editor.ProjectBuilder.BuildAndroid -logFile "$project_dir/Logs/android.log"
        ;;
    scene)
        "$unity_editor" -batchmode -nographics -quit -projectPath "$project_dir" \
            -executeMethod ArmOrb.Editor.ProjectBuilder.CreateProject -logFile "$project_dir/Logs/scene.log"
        ;;
    *) echo 'Usage: Tools/unity.sh {test|android|scene}' >&2; exit 2 ;;
esac
