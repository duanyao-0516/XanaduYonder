#!/usr/bin/env bash
set +x
set -euo pipefail

usage() {
    cat <<'HELP'
Usage: scripts/build.sh PLATFORM [OPTIONS]

Platforms:
  android [--configuration debug|release] [--install [DEVICE_SERIAL]]
      Build an ARM64 APK; debug is the default. Installation uses a physical device.
  ios
      Export a filtered Xcode project to artifacts/ios/xcode.
      This does not compile, sign, or install an iOS application with Xcode.

Use scripts/build.sh PLATFORM --help for platform details.
An explicit platform is required; the host and connected devices do not select it.
HELP
}

platform_help() {
    if [[ $platform == android ]]; then
        cat <<'HELP'
Usage: scripts/build.sh android [--configuration debug|release] [--install [DEVICE_SERIAL]]

  --configuration debug|release  Default: debug, without Unity Development mode.
  --install [DEVICE_SERIAL]       Install after a successful build and verification.
                                 With no serial, exactly one ready physical device is required.
  -h, --help                     Show this help without accessing build tools or signing data.

UNITY_EDITOR overrides the executable for the project's pinned Unity Hub version.
Release uses the existing private signing configuration or all four XANADU signing
variables. Replacement installation requires the same package and signing key.
HELP
    else
        cat <<'HELP'
Usage: scripts/build.sh ios [--help]
Export Unity-native games using an isolated, filtered project copy.
This command does not compile, sign, or install an iOS application with Xcode.

Environment:
  UNITY_EDITOR          Unity executable (defaults to the project's pinned Hub version).
  XANADU_IOS_TEAM_ID     Optional Apple development team ID; no credentials are accepted.

The Unity iOS Build Support module must be installed for that Editor version.
Existing Xcode output is preserved as artifacts/ios/previous-xcode-<timestamp>.
The source Assets and ProjectSettings are never used as the build workspace.
Android options --configuration and --install are not supported for iOS.
HELP
    fi
}

argument_error() {
    printf '%s\n' "$1" >&2
    usage >&2
    exit 2
}

if [[ $# -eq 0 ]]; then
    argument_error 'A platform is required: android or ios.'
fi
if [[ $1 == --help || $1 == -h ]]; then
    [[ $# -eq 1 ]] || argument_error 'Top-level help does not accept other arguments.'
    usage
    exit 0
fi
platform="$1"
shift
case "$platform" in
    android|ios) ;;
    *) argument_error "Unknown platform: $platform" ;;
esac

configuration=debug
configuration_set=false
install_set=false
show_help=false
install_args=()
while [[ $# -gt 0 ]]; do
    case "$1" in
        --configuration)
            [[ $platform == android ]] || argument_error '--configuration is only supported for Android.'
            [[ $configuration_set == false ]] || argument_error '--configuration may only be specified once.'
            [[ $# -ge 2 && $2 != -* && -n $2 ]] || argument_error '--configuration requires debug or release.'
            case "$2" in
                debug|release) configuration="$2" ;;
                *) argument_error "Unknown Android configuration: $2" ;;
            esac
            configuration_set=true
            shift 2
            ;;
        --install)
            [[ $platform == android ]] || argument_error '--install is only supported for Android.'
            [[ $install_set == false ]] || argument_error '--install may only be specified once.'
            install_set=true
            install_args=(--install)
            shift
            if [[ $# -gt 0 && $1 != -* ]]; then
                [[ -n $1 ]] || argument_error 'DEVICE_SERIAL must not be empty when supplied.'
                install_args+=("$1")
                shift
            fi
            ;;
        --help|-h)
            [[ $show_help == false ]] || argument_error '--help may only be specified once.'
            show_help=true
            shift
            ;;
        *) argument_error "Unknown argument: $1" ;;
    esac
done

if [[ $show_help == true ]]; then
    platform_help
    exit 0
fi
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
if [[ $platform == ios ]]; then
    exec "$SCRIPT_DIR/build/ios-export.sh"
fi
# The conditional expansion also works with empty arrays under macOS Bash 3.2's nounset.
exec "$SCRIPT_DIR/build/android-$configuration.sh" ${install_args[@]+"${install_args[@]}"}
