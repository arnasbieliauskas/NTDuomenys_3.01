from pathlib import Path
path = Path('RpaAruodas/MainWindow.axaml.cs')
lines = path.read_text().splitlines()
keep = lines[:759] + lines[1169:]
path.write_text('\n'.join(keep) + '\n')
