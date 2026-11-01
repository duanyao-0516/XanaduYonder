#!/usr/bin/env python3
"""Run the real platform-independent Java runtime policies without Android or network."""
from pathlib import Path
import os
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[2]
JAVA = Path(os.environ.get("UNITY_ANDROID_PLAYER", "/Applications/Unity/Hub/Editor/6000.3.27f1/PlaybackEngines/AndroidPlayer")) / "OpenJDK/bin"
SOURCE = ROOT / "ThirdParty/CutTheRopeRuntime/src/com/xanaduyonder/cuttherope"
required = [SOURCE / "CompletedBackPolicy.java", SOURCE / "OfflineAssetPolicy.java"]
missing = [str(path.relative_to(ROOT)) for path in required if not path.is_file()]
assert not missing, "Missing runtime implementation: " + ", ".join(missing)
with tempfile.TemporaryDirectory(prefix="cut-the-rope-tests-") as temp:
    subprocess.run([str(JAVA / "javac"), "-encoding", "UTF-8", "-d", temp] + [str(p) for p in required] + [str(Path(__file__).with_name("RuntimePolicyTest.java"))], check=True)
    subprocess.run([str(JAVA / "java"), "-cp", temp, "com.xanaduyonder.cuttherope.RuntimePolicyTest"], check=True)
