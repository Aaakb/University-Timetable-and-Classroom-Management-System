"""Check exported PDFs after running the C# regression checks; requires pypdf."""
from pathlib import Path
import json
import unicodedata
from pypdf import PdfReader

output = Path('.tmp/FourFixes/results')
samples = []
for name in ['arabic', 'mixed', 'english', 'empty', 'multi-page']:
    reader = PdfReader(output / (name + '.pdf'))
    assert len(reader.pages) > 0
    assert all(abs(float(page.mediabox.width) - 842) < 1 and abs(float(page.mediabox.height) - 595) < 1 for page in reader.pages)
    assert all('?' not in page.extract_text() for page in reader.pages)
    samples.append({'name': name, 'pages': len(reader.pages), 'questionMarks': False})

arabic = PdfReader(output / 'arabic.pdf')
# PDF font maps can return the visually equivalent Persian yeh for Arabic yeh.
text = unicodedata.normalize('NFKC', arabic.pages[0].extract_text()).replace('\u06cc', '\u064a')
assert 'الرياضيات' in text and 'أحمد علي' in text
fonts = [reference.get_object() for reference in arabic.pages[0]['/Resources']['/Font'].values()]
assert all(font.get('/ToUnicode') for font in fonts)
assert all(font['/DescendantFonts'][0].get_object()['/FontDescriptor'].get_object().get('/FontFile2') for font in fonts)
results = {'samples': samples, 'arabicTextVerified': True, 'embeddedFontVerified': True}
(output / 'pdf-checks.json').write_text(json.dumps(results, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(results, ensure_ascii=False))
