import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import vm from 'node:vm';

const schemaSource = await readFile(new URL('../../UniversityTimetable.API/wwwroot/js/schema.js', import.meta.url), 'utf8');
const { lectureCounts, modules } = await import('data:text/javascript,' + encodeURIComponent(schemaSource));
for (const [theoreticalHours, practicalHours, expected] of [[0,0,[1,0]], [4,0,[1,0]], [0,2,[0,1]], [4,2,[1,1]]]) {
  const counts = lectureCounts({ theoreticalHours, practicalHours });
  assert.deepEqual(Object.values(counts), expected);
}
assert.deepEqual(Object.values(lectureCounts({ theoreticalHours: 4, practicalHours: 2, theoreticalLectureCount: 2, practicalLectureCount: 3 })), [2,3]);
assert.deepEqual(Object.values(lectureCounts({ theoreticalHours: 4, practicalHours: 2, theoreticalLectureCount: 0, practicalLectureCount: 0 })), [0,0]);
assert.equal(modules.subjects.fields.filter(f => f.lectureCount).length, 2);
assert.equal(modules.subjects.fields.some(f => f.key === 'creditUnits'), false);
assert.equal(modules.subjects.fields.some(f => ['theoreticalHours', 'practicalHours'].includes(f.key)), false);
// Parse the complete browser module, including its DOM event handlers.
const source = await readFile(new URL('../../UniversityTimetable.API/wwwroot/js/app.js', import.meta.url), 'utf8');
new vm.SourceTextModule(source);
console.log('PASS Web automatic counts, overrides, zero counts, replacement fields and module syntax');
