"""Verify observable PDF page grouping, printed headers and right-to-left day placement."""
import unicodedata
from pathlib import Path
import pymupdf

root=Path('tmp/pdfs/instructor-export')
def normalize(value):return unicodedata.normalize('NFKC',value).replace('\u06cc','ي').replace('\u06a9','ك')
def text(page):return normalize(page.get_text())
def require(condition,message):
    if not condition:raise AssertionError(message)
    print('PASS:',message)

with pymupdf.open(root/'instructor-fixture.pdf') as d:
    require(len(d)==2,'An instructor gets one page per semester across branches, years and sections.')
    for p in d:
        require('جدول التدريسي' in text(p),'The page title identifies the instructor report.')
        require('Section ' not in text(p) and 'Third Year' not in text(p),'An instructor title is not labeled with a branch or section.')
    require('Database Security' in text(d[0]) and 'Cloud Computing' in text(d[0]),'Lessons from both branches are on the same semester page.')
    require('10:20' in text(d[0]) and 'No records' not in text(d[0]),'24-hour API time values produce real timeline rows.')
    require('Second Semester' not in text(d[0]) and 'Second Semester' in text(d[1]),'Semesters remain separate.')

with pymupdf.open(root/'standard-fixture.pdf') as d:
    require(len(d)==3,'A normal academic export retains its year, branch and section pages.')
    require(all('Section ' in text(p) for p in d),'Normal academic exports retain academic titles.')

with pymupdf.open(root/'empty-fixture.pdf') as d:
    require(len(d)==1 and 'No records' in text(d[0]),'An empty export is still a valid, readable PDF.')

with pymupdf.open('output/pdf/instructor-warood-hassan.pdf') as d:
    require(len(d)==1,'The supplied six-lesson instructor report now fits on one combined page.')
    page=d[0]
    require('Data Analytics' in text(page) and 'Database Security' in text(page),'All instructor subjects are preserved.')
    headers=[]
    for block in page.get_text('dict')['blocks']:
        for line in block.get('lines',[]):
            label=normalize(''.join(span['text'] for span in line['spans'])).strip()
            if 105 < line['bbox'][1] < 135:headers.append((line['bbox'][0],label))
    labels=[label for _,label in sorted(headers,reverse=True)]
    require(labels==['الوقت','الأحد','الاثنين','الثلاثاء','الأربعاء','الخميس'],f'Days run Sunday through Thursday from right to left, with Tuesday present: {labels}')
    for subject,left,right in [('Data Analytics',432,566),('Database Security',30,164)]:
        cells=page.search_for(subject)
        require(len(cells)==3 and all(left<=r.x0<r.x1<=right for r in cells),f'{subject} stays in its original weekday column.')
        require(sorted(int((r.y0-139)//84) for r in cells)==[0,1,2],f'{subject} retains all three original time periods.')
    require(all(0<=w[0]<w[2]<=page.rect.width and 0<=w[1]<w[3]<=page.rect.height for w in page.get_text('words')),'All report text stays within the page.')
    page.get_pixmap(matrix=pymupdf.Matrix(1.6,1.6)).save(root/'after.png')
print('All PDF export checks passed.')
