# Ruffle v0.7.1 web runtime

`upstream-lock.json` locks the official self-hosted release archive and the
seven selected files. Every selected file was compared byte-for-byte against
the official `ruffle-0.7.1-web-selfhosted.zip` release. Source maps are omitted;
`ruffle.js`, both hashed core JS chunks, both hashed WASM variants, and the
original MIT / Apache-2.0 licenses are retained unchanged.

This directory is a reusable source input outside Unity Assets. The Bomb It
builder packages these files once under `ruffle/`; the game SWF remains separate.
No application assets are fetched from a CDN at runtime.

Upstream: https://github.com/ruffle-rs/ruffle/releases/tag/v0.7.1

The runtime's licenses cover Ruffle, not the separately sourced Flash game.
