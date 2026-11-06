import assert from 'node:assert/strict';
import {createMockData} from '../../demo/netlify/mock-data.js';
const data=createMockData();
for(const row of data.schedules){
  assert.ok(data.subjects.some(s=>s.subjectID===row.subjectID));
  assert.ok(data.sections.some(s=>s.sectionID===row.sectionID));
  assert.ok(data.facultymembers.some(s=>s.facultyMemberID===row.facultyMemberID));
  assert.ok(data.classrooms.some(s=>s.classroomID===row.classroomID));
  assert.ok(data.timeslots.some(s=>s.timeSlotID===row.timeSlotID));
}
for(let i=0;i<data.schedules.length;i++)for(let j=i+1;j<data.schedules.length;j++){
  const a=data.schedules[i],b=data.schedules[j];
  if(a.semesterNumber===b.semesterNumber&&a.dayOfWeek===b.dayOfWeek&&a.timeSlotID===b.timeSlotID)assert.ok(a.facultyMemberID!==b.facultyMemberID&&a.classroomID!==b.classroomID&&a.sectionID!==b.sectionID,'Fixture has a schedule conflict');
}
const storage=new Map();globalThis.localStorage={getItem:key=>storage.get(key)??null,setItem:(key,value)=>storage.set(key,value)};
const {api,all,resetDemo}=await import('../../demo/netlify/api.js');
const overview=await api('overview');assert.equal(overview.subjects,24);assert.equal(overview.schedules,80);
assert.equal((await api('auth/me')).fullName,'زائر النسخة التجريبية');
for(const resource of ['branches','studyyears','sections','subjects','classrooms','facultymembers','assignments','timeslots','schedules']){
  const rows=await all(resource);assert.ok(rows.length>0);assert.ok(rows.every(r=>r.data&&r.version));
}
assert.equal((await api('schedules/details?semester=1')).items.length,40);
assert.equal((await api('faculty-preferences/lookups')).data.faculty.length,8);
assert.equal((await api('timetable-alternatives?semester=1')).lectures.length,40);
const rules=await api('overview/rules');assert.equal(rules.years.length,4);assert.equal(rules.subjectSections.length,24);
let branch=await api('branches',{method:'POST',body:{branchName:'اختبار محلي'}});assert.equal(branch.data.branchID,4);
const originalVersion=branch.version;
branch=await api('branches/4',{method:'PUT',headers:{'If-Match':`"${branch.version}"`},body:{branchName:'اختبار معدل'}});
await assert.rejects(()=>api('branches/4',{method:'DELETE',headers:{'If-Match':`"${originalVersion}"`}}),e=>e.status===412);
await api('branches/4',{method:'DELETE',headers:{'If-Match':`"${branch.version}"`}});
const schedule=(await all('schedules'))[0];
const unlocked=await api(`schedules/${schedule.data.scheduleID}/lock`,{method:'PUT',headers:{'If-Match':`"${schedule.version}"`},body:{locked:false}});assert.equal(unlocked.data.isLocked,false);
const before=JSON.stringify(storage);
await assert.rejects(()=>api('schedules/generation-state'),e=>e.status===501);
await assert.rejects(()=>api('imports/upload',{method:'POST',body:new FormData()}),e=>e.status===501);
assert.equal(JSON.stringify(storage),before);
resetDemo();assert.equal((await api('overview')).schedules,80);assert.equal((await all('branches')).length,3);
console.log('PASS: mock references, conflict-free timetable, 9 resources, dashboard, paging envelopes, CRUD, stale versions, locks, read-only previews, unsupported operations and reset.');
