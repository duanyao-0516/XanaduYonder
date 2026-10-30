# Original AVM2 display-coordinate observation

`reference.csv` records 16 traces from the original Age of War 2 SWF running in Ruffle 0.7.1 Web/WASM, Chrome on macOS. Columns are `kind,Number expression,Sprite.x,Sprite.y`; no native-port output is used as the reference. `metadata.json` records the observation, source hashes and retained original ABC hashes.

`InjectProbe.java` adds one standalone eager ABC containing `probe.as` before the first ShowFrame. It reopens the result and verifies that both original ABC serialized byte hashes remain present. It does not edit/recompile original methods. Compile the injector against FFDec 26.4.0's `ffdec_lib.jar`, then run with arguments `<original.swf> <probe.swf> <probe.as>`. Serve the patched file through the same Ruffle 0.7.1 web runtime with `logLevel: "info"`, and capture console lines containing `AS3TWIP`. The eager probe runs before a late `traceObserver` attaches; the observed data came from the browser console.

The observed x/y setter behavior is truncation toward zero of `value * 20`, then division by 20. In particular, `0.025` stores `0`, and `150.1 + 0.7` evaluates to `150.79999999999998` and stores `150.75`. Do not insert an epsilon or round to nearest. Only display-coordinate writes use this rule; ordinary Number velocities and elapsed time do not.

Scope: this is an original-runtime property probe. It does not establish full Two gameplay parity, nor rotation, scale, width or alpha storage semantics.
