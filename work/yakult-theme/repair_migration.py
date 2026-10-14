from pathlib import Path
import re
import subprocess
ROOT = Path(__file__).resolve().parents[2]
namespace = {}
source = (ROOT / 'work/yakult-theme/migrate_palette.py').read_text(encoding='utf-8')
exec(source.split('items = sorted(')[0], dict(__file__=str(ROOT / 'work/yakult-theme/migrate_palette.py')), namespace)
# Share globals used by the migration helpers without running its entry point.
namespace['xaml'].__globals__.update(namespace)
for path in (ROOT / 'Yakult.Inventory.App').rglob('*.xaml'):
    if any(part in {'obj', 'bin', 'artifacts'} for part in path.parts):
        continue
    raw = path.read_bytes()
    text = raw.decode('utf-8-sig')
    if 'YAKULT_PROTECTED_' in text:
        relative = path.relative_to(ROOT).as_posix()
        baseline = subprocess.check_output(['git', 'show', 'HEAD:' + relative], cwd=ROOT).decode('utf-8-sig')
        result = namespace['xaml'](baseline.replace('\r\n', '\n'))
    elif re.search(r'</ResourceDictionary.MergedDictionaries>\s*<ResourceDictionary Source=', text):
        result = re.sub(r'(</ResourceDictionary.MergedDictionaries>)(\s*)(<ResourceDictionary Source="[^"]+"\s*/>)', r'\3\2\1', text.replace('\r\n', '\n'), count=1)
    else:
        continue
    path.write_bytes((b'\xef\xbb\xbf' if raw.startswith(b'\xef\xbb\xbf') else b'') + result.replace('\n', '\r\n').encode('utf-8'))
    print('Repaired ' + str(path.relative_to(ROOT)))
