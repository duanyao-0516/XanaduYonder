#!/usr/bin/env bash
# Sourced by APK scripts after setting PROJECT_ROOT.
PROJECT_VERSION="$(sed -nE 's/^[[:space:]]*public const string Version = "([^"]+)";.*/\1/p' "$PROJECT_ROOT/Assets/Editor/ProjectBuild.cs")"
if [[ ! "$PROJECT_VERSION" =~ ^[0-9]+(\.[0-9]+)+$ ]]; then
    printf 'Cannot read ProjectBuild.Version from %s\n' "$PROJECT_ROOT/Assets/Editor/ProjectBuild.cs" >&2
    return 1
fi
