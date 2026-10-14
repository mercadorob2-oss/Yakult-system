from pathlib import Path
import json
import re

root = Path(__file__).resolve().parents[2]
app = root / 'Yakult.Inventory.App'
work = root / 'work/yakult-theme'
names = set(json.loads((work / 'first-pass-files.json').read_text(encoding='utf-8')))
names.update(json.loads((work / 'changed-files.json').read_text(encoding='utf-8')))
names.update(json.loads((work / 'second-pass-files.json').read_text(encoding='utf-8')))
names.update(('Wpf/Shared/Styles/ListPageStyles.xaml',
              'Wpf/RequestPortal/RequestPortalStyles.xaml',
              'Wpf/Shared/Styles/YakultPalette.xaml'))
cleaned = 0
for name in names:
    path = app / name
    if not path.exists() or path.suffix != '.xaml':
        continue
    raw = path.read_bytes()
    updated = re.sub(rb'(?m)^[ \t]+(?=\r?$)', b'', raw)
    if updated != raw:
        path.write_bytes(updated)
        cleaned += 1
print(f'Cleaned blank-line whitespace in {cleaned} XAML files.')
