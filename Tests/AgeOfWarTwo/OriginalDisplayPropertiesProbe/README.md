# Original AVM2 display-property observation

Independent follow-up to OriginalDisplayObjectProbe, preserving the earlier x/y fixture. The 27 traces from the patched original SWF in Ruffle 0.7.1 verify alpha, rotation and scale getters. Both original ABC byte hashes were preserved; see metadata.json. Build and observe using the same injector procedure described in the sibling fixture.

Alpha retains values outside 0..1 but truncates each setter to signed 1/256 units. Rotation normalizes to -180..180 and preserves the sign of exact 180. Scale showed ordinary floating-point decomposition differences, not an established integer quantization rule; no invented scale rule is derived here.

This is a property probe, not full-game comparison or proof of other Flash Player implementations.
