#!/usr/bin/env bash
set -euo pipefail
if [[ $# != 4 ]]; then printf 'Usage: %s original.swf ffdec_lib.jar ruffle-directory output-directory\n' "$0" >&2; exit 2; fi
AOW_SCRIPT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
AOW_SOURCE="$1"; AOW_FFDEC="$2"; AOW_RUFFLE="$3"; AOW_OUTPUT="$4"
mkdir -p "$AOW_OUTPUT/classes" "$AOW_OUTPUT/site"
javac -cp "$AOW_FFDEC" -d "$AOW_OUTPUT/classes" "$AOW_SCRIPT/InjectProbe.java"
java -Xmx1g -cp "$AOW_OUTPUT/classes:$AOW_FFDEC" InjectProbe "$AOW_SOURCE" "$AOW_OUTPUT/site/probe.swf" "$AOW_SCRIPT/probe.as"
cp "$AOW_SCRIPT/index.html" "$AOW_OUTPUT/site/index.html"
ln -snf "$AOW_RUFFLE" "$AOW_OUTPUT/site/ruffle"
printf 'Prepared %s/site. Serve with the adjacent ../one/serve.py helper.\n' "$AOW_OUTPUT"
