#!/bin/bash
set -euo pipefail
repo="$(cd "$(dirname "$0")/../../.." && pwd)"
source_swf="${AOW_ONE_SWF:-/Users/bytedance/Downloads/age-of-war-research-20261030/age-of-war-1-7k7k.swf}"
ffdec="${FFDEC_HOME:-/tmp/aow-tools/ffdec-26.4.0}"
java_home="${AOW_JAVA_HOME:-/opt/homebrew/opt/openjdk@17}"
parity_dir="${AOW_PARITY_DIR:-/tmp/aow-parity-one}"
ruffle="${AOW_RUFFLE_DIR:-/Users/bytedance/Downloads/age-of-war-research-20261030/analysis/runtime-preview/ruffle}"
mkdir -p "$parity_dir/site"
"$java_home/bin/javac" -cp "$ffdec/ffdec.jar:$ffdec/lib/*" -d "$parity_dir" "$repo/scripts/age-of-war-parity/one/InjectOne.java"
"$java_home/bin/java" -cp "$parity_dir:$ffdec/ffdec.jar:$ffdec/lib/*" InjectOne "$source_swf" "$parity_dir/site/one-instrumented.swf" "$repo/scripts/age-of-war-parity/one"
cp "$repo/scripts/age-of-war-parity/one/index.html" "$parity_dir/site/index.html"
ln -sfn "$ruffle" "$parity_dir/site/ruffle"
python3 "$repo/scripts/age-of-war-parity/one/serve.py" --directory "$parity_dir/site" --trace "$parity_dir/reference.csv" --port "${AOW_PARITY_PORT:-8769}"
