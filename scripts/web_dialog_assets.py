"""Prepare the same mobile dialog theme for each offline web game."""
import json
from pathlib import Path

INPUTS = ('ThirdParty/WebGameUI/dialogs.css', 'ThirdParty/WebGameUI/dialogs.js',
          'Assets/Resources/DialogSheetMotion.json', 'scripts/web_dialog_assets.py')


def shared_dialog_files(root):
    css, script, config, _ = (Path(root) / name for name in INPUTS)
    duration = json.loads(config.read_text())['durationMs']
    if type(duration) is not int or duration <= 0:
        raise ValueError(f'Expected a positive integer durationMs in {config}')
    timing = f':root {{ --xy-sheet-duration: {duration}ms; }}\n'.encode()
    return {'dialogs.css': timing + css.read_bytes(), 'dialogs.js': script.read_bytes()}
