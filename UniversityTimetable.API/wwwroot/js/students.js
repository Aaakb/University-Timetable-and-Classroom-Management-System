import { api } from './api.js';

const byId = id => document.getElementById(id);
const stage = byId('stage');
const group = byId('group');
const operation = byId('operation');
const destination = byId('destination');
const panels = [byId('students-panel'), byId('students-results')];
let requestedGroup = null;

export async function openStudents(params) {
  await pending;
  const requestedStage = params.get('stage');
  if (['1','2','3','4'].includes(requestedStage)) stage.value = requestedStage;
  requestedGroup = params.has('stage') ? params.get('section') ?? '' : null;
  await run(loadStudents);
}
export function clearStudents() {
  revision++;
  dismissImportResult();
  stageStudents = [];
  group.replaceChildren();
  destination.replaceChildren();
  byId('students').replaceChildren();
}
let groups = [];
let stageStudents = [];
let pending = null;
let revision = 0;

function populate(select, choices) {
  select.replaceChildren(new Option('بدون تحديد', ''));
  for (const choice of choices) select.add(new Option(choice.label, choice.id));
}

async function loadDestination() {
  const promote = operation.value === 'promote';
  const current = Number(stage.value);
  const choices = promote ? await api(`students/${current + 1}/groups`) : groups;
  populate(destination, choices);
  destination.required = !promote || current === 2;
}

async function loadStudents() {
  const currentRevision = revision;
  stageStudents = [];
  byId('students').replaceChildren();
  const { students, groups: currentGroups } = await api(`students/${stage.value}`);
  if (currentRevision !== revision) return;
  groups = currentGroups;
  const explicitGroup = requestedGroup;
  const selectedGroup = explicitGroup ?? group.value;
  requestedGroup = null;
  populate(group, [...groups, { id:-1, label:'غير موزّعون' }]);
  group.options[0].textContent='جميع الطلبة';
  if ([...group.options].some(choice => choice.value === selectedGroup)) group.value = selectedGroup;
  else if (explicitGroup) {
    renderStudents();
    throw new Error('الشعبة أو التخصص المحدد غير متاح. اختر نطاقًا آخر.');
  }

  operation.options[1].disabled = stage.value === '4';
  if (stage.value === '4') operation.value = 'assign';
  await loadDestination();
  if (currentRevision !== revision) return;
  stageStudents = students;
  renderStudents();
}

function renderStudents() {
  byId('students').replaceChildren();
  const labels = new Map(groups.map(item => [item.id, item.label]));
  const section = Number(group.value);
  const visible = stageStudents.filter(student => group.value==='' || student.sectionID === (section === -1 ? null : section))
    .sort((a,b)=>(a.practicalGroupNumber ?? 3)-(b.practicalGroupNumber ?? 3));
  for (const [index, student] of visible.entries()) {
    const row = byId('students').insertRow();
    row.insertCell().textContent = index + 1;
    const checkbox = document.createElement('input');
    checkbox.type = 'checkbox';
    checkbox.value = student.studentID;
    checkbox.setAttribute('aria-label', student.fullName);
    row.insertCell().append(checkbox);
    row.insertCell().append(studentButton(student.fullName, () => renameStudent(student)));
    const label = labels.get(student.sectionID) ?? 'غير موزّع';
    const detail = student.practicalGroupNumber ? `${label} · المجموعة ${student.practicalGroupNumber}` : label;
    const alternative = Number(stage.value) <= 2 && student.sectionID
      ? groups.find(choice=>choice.id!==student.sectionID) : null;
    const sectionCell=row.insertCell();
    if(alternative) sectionCell.append(studentButton(detail, () => changeSection(student, alternative)));
    else sectionCell.textContent=detail;
    row.insertCell().textContent=student.studentID;
  }
  updateSelection();
}

function studentButton(text, action) {
  const button=document.createElement('button');
  button.type='button';
  button.className='student-row-action';
  button.textContent=text;
  button.onclick=action;
  return button;
}

function renameStudent(student) {
  const fullName=prompt('تعديل اسم الطالب',student.fullName)?.trim();
  if(!fullName || fullName===student.fullName)return;
  run(async()=>{
    await api(`students/${student.studentID}/name`,{method:'PUT',body:{fullName}});
    await loadStudents();
    return 'تم تعديل اسم الطالب.';
  });
}

function changeSection(student, destination) {
  if(!confirm(`نقل «${student.fullName}» إلى ${destination.label}؟`))return;
  run(async()=>{
    await api(`students/${stage.value}/change-section`,{
      method:'POST',body:{studentId:student.studentID,sourceId:student.sectionID,destinationId:destination.id}
    });
    await loadStudents();
    return 'تم نقل الطالب إلى الشعبة الأخرى.';
  });
}

byId('split-students').onclick=()=>{
  if(!confirm('تقسيم الشعبة إلى مجموعتين عمليتين؟ سيُستبدل التقسيم الحالي.'))return;
  run(async()=>{
    await api(`students/${stage.value}/split`,{method:'POST',body:{sectionId:Number(group.value)}});
    await loadStudents();
    return 'تم تقسيم الشعبة إلى مجموعتين عمليتين.';
  });
};
for(const format of ['xlsx','pdf']){
  byId('export-students-'+format).onclick=()=>run(async()=>{
    const params=new URLSearchParams({format});
    if(group.value)params.set('sectionId',group.value);
    const blob=await api(`students/${stage.value}/export?${params}`,{blob:true});
    const link=document.createElement('a');
    const url=URL.createObjectURL(blob);
    link.href=url;
    link.download=`students-${stage.value}.${format}`;
    link.click();
    setTimeout(()=>URL.revokeObjectURL(url),10000);
  });
}

function updateSelection() {
  const list = byId('students');
  const selected = list.querySelectorAll('input:checked').length;
  byId('student-count').textContent = `الطلبة: ${list.rows.length} · المحددون: ${selected}`;
  const split=byId('split-students');
  split.hidden=Number(stage.value)>2 || Number(group.value)<=0;
  split.disabled=list.rows.length<2;
  for(const format of ['xlsx','pdf'])byId('export-students-'+format).disabled=list.rows.length===0;
  byId('students-empty').hidden = list.rows.length !== 0;
  byId('select-all-students').disabled = list.rows.length === 0;
  byId('clear-selected-students').disabled = selected === 0;
}

for (const [id, checked] of [['select-all-students', true], ['clear-selected-students', false]]) {
  byId(id).onclick = () => {
    for (const box of byId('students').querySelectorAll('input[type=checkbox]')) box.checked = checked;
    updateSelection();
  };
}
byId('students').addEventListener('change', updateSelection);

function run(action) {
  if (pending) return pending;
  pending = runAction(action).finally(() => { pending = null; });
  return pending;
}

async function runAction(action) {
  panels.forEach(panel=>panel.inert=true);
  byId('student-error').hidden = true;
  byId('student-status').textContent = '';
  try {
    const message = await action();
    if (message) byId('student-status').textContent = message;
  } catch (error) {
    byId('student-error').textContent = error.message;
    byId('student-error').hidden = false;
  } finally {
    panels.forEach(panel=>panel.inert=false);
  }
}

function bind(id, handler) {
  const form = byId(id);
  form.addEventListener('submit', event => {
    event.preventDefault();
    run(() => handler(form));
  });
}

function dismissImportResult() {
  byId('student-import-result').hidden = true;
  byId('student-imported-names').replaceChildren();
  byId('student-skipped-names').replaceChildren();
  byId('student-import-summary').textContent = '';
}
byId('dismiss-student-import').onclick = dismissImportResult;

function showImportResult(result) {
  const lists = [
    ['student-imported-details', 'student-imported-names', result.imported, false],
    ['student-skipped-details', 'student-skipped-names', result.skippedRows, true]
  ];
  for (const [detailsId, listId, rows, showReason] of lists) {
    const details = byId(detailsId);
    const list = byId(listId);
    list.replaceChildren();
    details.hidden = rows.length === 0;
    details.open = showReason && rows.length > 0;
    for (const row of rows) {
      const item = document.createElement('li');
      item.textContent = `${row.name} (${row.location})${showReason ? ` · ${row.reason}` : ''}`;
      list.append(item);
    }
  }
  byId('student-import-result').hidden = false;
  byId('student-import-summary').textContent = `نتيجة الاستيراد: أُضيف ${result.added}، وتُخطّي ${result.skipped}.`;
}

bind('add-student', async form => {
  const fullName = new FormData(form).get('fullName');
  await api(`students/${stage.value}`, { method: 'POST', body: { fullName, sectionId: Number(group.value)>0 ? Number(group.value) : null } });
  form.reset();
  await loadStudents();
  return 'تمت إضافة الطالب.';
});
bind('import-students', async form => {
  if (!confirm('استيراد الطلبة إلى المرحلة والشعبة المحددتين؟')) return;
  dismissImportResult();
  const currentRevision = revision;
  const body = new FormData(form);
  if (Number(group.value)>0) body.set('sectionId', group.value);
  const result = await api(`students/${stage.value}/import`, { method: 'POST', body });
  if (revision !== currentRevision) return;
  form.reset();
  showImportResult(result);
  await loadStudents();
});
bind('move-students', async () => {
  const studentIds = [...byId('students').querySelectorAll('input:checked')].map(input => Number(input.value));
  if (!studentIds.length) throw new Error('حدد طالبًا واحدًا على الأقل.');
  const promote = operation.value === 'promote';
  if (!confirm(`تطبيق الإجراء على ${studentIds.length} من الطلبة؟`)) return;
  const result = await api(`students/${stage.value}/move`, {
    method: 'POST', body: { studentIds, sectionId: Number(destination.value) || null, promote }
  });
  await loadStudents();
  return `تم تحديث ${result.count} من الطلبة.`;
});

group.addEventListener('change', renderStudents);
stage.addEventListener('change', () => run(loadStudents));
operation.addEventListener('change', () => run(loadDestination));
