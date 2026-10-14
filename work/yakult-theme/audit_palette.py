from pathlib import Path
from collections import Counter
import colorsys
import json
import re
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[2]
app = root / 'Yakult.Inventory.App'
paths = sorted(set(json.loads((root / 'work/yakult-theme/changed-files.json').read_text(encoding='utf-8')))
             | set(json.loads((root / 'work/yakult-theme/first-pass-files.json').read_text(encoding='utf-8')))
             | set(json.loads((root / 'work/yakult-theme/second-pass-files.json').read_text(encoding='utf-8'))))
namespaces = {'p': 'http://schemas.microsoft.com/winfx/2006/xaml/presentation'}
counts = Counter()
for name in paths:
    path = app / name
    raw = path.read_text(encoding='utf-8-sig')
    if path.suffix != '.xaml':
        continue
    parsed = ET.fromstring(raw)
    assert 'YAKULT_PROTECTED_' not in raw, name
    assert 'YakultPalette.xaml' in raw, name
    if parsed.tag.endswith('ResourceDictionary'):
        dictionary = parsed
    else:
        dictionary = parsed.find('p:' + parsed.tag.rsplit('}', 1)[-1] + '.Resources/p:ResourceDictionary', namespaces)
    assert dictionary is not None, name
    merged = dictionary.find('p:ResourceDictionary.MergedDictionaries', namespaces)
    assert merged is not None, name
    assert any('YakultPalette.xaml' in child.get('Source', '') for child in merged), name
    for old in re.findall(r'#[0-9A-Fa-f]{6}\b', raw):
        red, green, blue = [int(old[i:i+2], 16) / 255 for i in (1, 3, 5)]
        hue, sat, val = colorsys.rgb_to_hsv(red, green, blue)
        if .52 <= hue <= .74 and sat > .18 and val > .25:
            counts[old.upper()] += 1

print('Verified palette resources in ' + str(sum(Path(name).suffix == '.xaml' for name in paths)) + ' changed XAML files.')
print('Remaining blue-hue literals, including semantic data colors:')
for value, count in counts.most_common(25):
    print(value, count)
