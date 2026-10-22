import { api, all, refreshCsrf } from './api.js';
import { modules, days, types, keyOf, lectureCounts } from './schema.js';

const $ = id => document.getElementById(id);
const state = { route:'dashboard', rows:[], lookups:{}, rules:{}, details:[], filters:{}, page:1, view:'list', editing:null, dirty:false, busy:false, epoch:0, authenticated:false };
const esc = value => String(value ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const fmt = value => Number(value).toLocaleString('en-US');
const time = value => String(value || '').slice(0,5);
const textOf = (options, value) => options.find(x => String(x[0]) === String(value))?.[1] ?? value ?? '—';
const notice = message => { $('toast').textContent = message; $('toast').hidden = false; clearTimeout(notice.timer); notice.timer = setTimeout(() => $('toast').hidden = true, 6000); };
function error(id, message) { $(id).textContent = message || ''; $(id).hidden = !message; }
function confirmAction(message, title = 'تأكيد العملية') {
  $('confirm-title').textContent = title; $('confirm-message').textContent = message;
  return new Promise(resolve => { const modal = $('confirmation'); modal.returnValue = 'cancel'; modal.addEventListener('close', () => resolve(modal.returnValue === 'yes'), {once:true}); modal.showModal(); });
}
function showLogin() {
  state.authenticated = false; state.epoch++; state.rows=[]; state.lookups={}; state.details=[]; state.rules={}; state.editing=null; state.dirty=false;
  $('application').hidden=true; $('auth').hidden=false; $('auth-form').reset(); $('dashboard').replaceChildren(); $('table-host').replaceChildren(); $('editor-fields').replaceChildren(); $('filters').replaceChildren();
  if ($('editor').open) $('editor').close();
  if ($('confirmation').open) $('confirmation').close('cancel');
  document.body.classList.remove('menu-open');
}
async function enter(user) {
  state.authenticated=true; $('auth').hidden=true; $('application').hidden=false;
  $('user-name').textContent = user.fullName; $('auth-form').reset();
  await refreshCsrf(); await navigate();
}
let register = false;
$('auth-switch').onclick = () => {
  register=!register; $('full-name-label').hidden=!register; $('confirm-label').hidden=!register;
  $('auth-form').elements.fullName.required=register; $('auth-form').elements.confirmPassword.required=register;
  $('auth-form').elements.password.autocomplete=register?'new-password':'current-password';
  $('auth-title').textContent=register?'إنشاء حساب':'تسجيل الدخول'; $('auth-submit').textContent=register?'إنشاء الحساب':'تسجيل الدخول';
  $('auth-description').textContent=register?'أنشئ حسابًا لاستخدام تطبيق الجامعة.':'استخدم حسابك الحالي في تطبيق الجامعة.';
  $('auth-switch').textContent=register?'لديك حساب؟ تسجيل الدخول':'ليس لديك حساب؟ إنشاء حساب'; error('auth-error','');
};
$('auth-form').onsubmit=async event => {
  event.preventDefault(); error('auth-error',''); $('auth-submit').disabled=true; $('auth-switch').disabled=true;
  try {
    const values=Object.fromEntries(new FormData(event.target));
    if (register) { await api('auth/register',{method:'POST',body:values}); $('auth-switch').onclick(); notice('تم إنشاء الحساب. سجل الدخول للمتابعة.'); }
    else await enter(await api('auth/login',{method:'POST',body:{userName:values.userName,password:values.password}}));
  } catch(ex) { error('auth-error',ex.message); }
  finally { $('auth-submit').disabled=false; $('auth-switch').disabled=false; }
};
$('logout').onclick=async()=>{
  if (state.busy) return notice('انتظر اكتمال العملية الحالية.');
  try { await api('auth/logout',{method:'POST'}); showLogin(); await refreshCsrf(); }
  catch(ex) { notice(ex.message); }
};
document.addEventListener('session-expired',()=>{ showLogin(); error('auth-error','انتهت الجلسة. سجل الدخول مجددًا.'); });
$('menu').onclick=()=>{ const open=document.body.classList.toggle('menu-open'); $('menu').setAttribute('aria-expanded',open); };
$('navigation').innerHTML='<a href="#dashboard" data-route="dashboard"><span class="nav-icon">◈</span>نظرة عامة</a>'+Object.entries(modules).map(([route,m])=>`<a href="#${route}" data-route="${route}"><span class="nav-icon">${m.icon}</span>${m.title}</a>`).join('');
$('navigation').insertAdjacentHTML('beforeend', '<a href="/preferences.html"><span class="nav-icon">★</span>تفضيلات التدريسيين</a>');
$('navigation').insertAdjacentHTML('beforeend', '<a href="/alternatives.html"><span class="nav-icon">▦</span>جداول بديلة</a>');
$('navigation').insertAdjacentHTML('beforeend', '<a href="/data-transfer.html"><span class="nav-icon">⇧</span>استيراد Excel / PDF</a><a href="/data-transfer.html#backups"><span class="nav-icon">▤</span>حفظ واستعادة الجدول</a>');
async function navigate() {
  if (!state.authenticated) return;
  const route=location.hash.slice(1)||'dashboard'; state.route=modules[route]?route:'dashboard';
  state.page=1; state.filters={}; $('search').value=''; state.rows=[]; state.details=[];
  document.body.classList.remove('menu-open'); $('menu').setAttribute('aria-expanded','false');
  const module=modules[state.route];
  $('page-title').textContent=module?.title||'نظرة عامة'; $('breadcrumb').textContent=module?.title||'نظرة عامة';
  $('page-description').textContent=module?.description||'كل ما تحتاجه لتنظيم الفصل الدراسي، في مكان واحد.';
  $('add').hidden=!module; $('add').textContent=`+ إضافة ${module?.singular||'سجل'}`;
  $('dashboard').hidden=!!module; $('management').hidden=!module;
  for (const link of $('navigation').children) { const active=link.dataset.route===state.route; link.classList.toggle('active',active); if(active)link.setAttribute('aria-current','page');else link.removeAttribute('aria-current'); }
  await load();
}
window.addEventListener('hashchange',navigate);
$('refresh').onclick=()=>load();
async function load() {
  const epoch=++state.epoch, route=state.route;
  $('loading').hidden=false; $('refresh').disabled=true; $('add').disabled=true; error('page-error','');
  try {
    if(route==='dashboard') {
      const data=await api('overview'); if(epoch!==state.epoch)return; renderDashboard(data);
    } else {
      // Reload only the dependencies this page uses, alongside its own records.
      const dependencies={sections:['studyyears','branches'],subjects:['studyyears','branches'],assignments:['studyyears','branches','subjects','facultymembers','sections'],schedules:Object.keys(modules).filter(x=>x!=='schedules')}[route]||[];
      const [entries,rows,rules,details]=await Promise.all([
        Promise.all(dependencies.map(async key=>[key,(await all(key)).map(x=>x.data)])),
        all(route),['sections','subjects','assignments','schedules'].includes(route)?api('overview/rules'):Promise.resolve({}),
        route==='schedules'?api('schedules/details'):Promise.resolve({items:[]})]);
      if(epoch!==state.epoch)return;
      state.lookups={...Object.fromEntries(entries),[route]:rows.map(x=>x.data)}; state.rows=rows; state.rules=rules; state.details=details.items;
      renderFilters(); renderSpecialActions(); renderTable();
    }
    $('last-refresh').textContent=`آخر تحديث: ${new Date().toLocaleTimeString('ar-IQ',{hour:'2-digit',minute:'2-digit'})}`;
  }catch(ex){ if(epoch===state.epoch)error('page-error',ex.message); }
  finally{ if(epoch===state.epoch){ $('loading').hidden=true; $('refresh').disabled=false; $('add').disabled=false; } }
}
function renderDashboard(data){
  $('dashboard').innerHTML=`<div class="hero"><div><span class="eyebrow">تنظيم أفضل. يوم دراسي أوضح.</span><h2>جدول الجامعة، أمامك بالكامل.</h2><p>تابع المحاضرات والموارد المتاحة، وانتقل مباشرة إلى الجدول لإدارة توزيع المواد والقاعات.</p></div><a class="primary" href="#schedules"><button class="primary">عرض الجدول الدراسي ←</button></a></div><div class="stats">${[['classrooms','القاعات والمختبرات','مساحات تعليمية'],['subjects','المواد الدراسية','مواد الفصل الدراسي'],['facultyMembers','أعضاء الهيئة التدريسية','تدريسيون مسجلون'],['sections','الشعب الدراسية','شعب في المراحل المختلفة']].map(([key,label,foot])=>`<div class="stat"><span class="stat-label">${label}</span><strong>${fmt(data[key])}</strong><span class="stat-foot">${foot}</span></div>`).join('')}</div><div class="dashboard-bottom"><section class="panel"><div class="section-head"><h3>وصول سريع</h3><span>إدارة الفصل الدراسي</span></div><div class="quick-links">${[['subjects','المواد والمحاضرات'],['assignments','توزيع المواد على التدريسيين'],['classrooms','السعة وأنواع القاعات'],['timeslots','بداية المحاضرات ونهايتها']].map(([key,label])=>`<a class="quick-link" href="#${key}">${modules[key].icon} &nbsp;${modules[key].title}<small>${label} ←</small></a>`).join('')}</div></section><section class="panel"><div class="section-head"><h3>حالة الإعداد</h3><span>من البيانات الحالية</span></div><div class="readiness"><strong>${fmt(data.schedules)}</strong><h3>محاضرة في الجدول</h3><p>${fmt(data.assignments)} تكليفًا تدريسيًا مسجلًا.</p><p class="${data.unassignedSubjects?'warning':''}">${data.unassignedSubjects?`${fmt(data.unassignedSubjects)} مادة تحتاج إلى تكليف تدريسي قبل التوليد.`:'كل المواد مرتبطة بتكليفات تدريسية. تتحقق بقية شروط الجدولة عند التوليد.'}</p><p>حدّث البيانات لعرض آخر تغييرات تطبيق سطح المكتب.</p></div></section></div>`;
}
function lookupLabel(resource,row){
  if(!row)return '—';
  if(resource==='timeslots')return `${time(row.startTime)} – ${time(row.endTime)}`;
  if(resource==='sections')return `${row.sectionName} · ${lookupValue('studyyears',row.studyYearID)}${row.branchID?' · '+lookupValue('branches',row.branchID):''}`;
  if(resource==='subjects')return `${row.subjectName} · ${lookupValue('studyyears',row.studyYearID)} · ف${row.semesterNumber}${row.branchID?' · '+lookupValue('branches',row.branchID):''}`;
  return row[modules[resource].label]??'—';
}
function lookupValue(resource,id){
  const rows=state.lookups[resource]||[];
  const row=rows.find(x=>String(x[modules[resource].key])===String(id));
  if(!row&&resource==='subjects'){
    const assignment=(state.lookups.assignments||[]).find(x=>String(x.subjectID)===String(id));
    if(assignment?.subjectName)return assignment.subjectName+(assignment.isSubjectDeleted?' (محذوفة من قائمة المواد)':'');
  }
  return lookupLabel(resource,row);
}
function display(field,value){ if(field.resource)return lookupValue(field.resource,value);if(field.options)return textOf(field.options,value);if(field.type==='time')return time(value);return value??'—'; }
function selectOptions(options,current,placeholder='الكل') {return `<option value="">${placeholder}</option>`+options.map(([value,label])=>`<option value="${esc(value)}" ${String(value)===String(current)?'selected':''}>${esc(label)}</option>`).join('');}
function resourceOptions(resource){return (state.lookups[resource]||[]).map(x=>[x[modules[resource].key],lookupLabel(resource,x)]);}
function renderFilters(){
  let fields=[];
  if(['subjects','sections','schedules','assignments'].includes(state.route))fields.push(['studyYearID','المرحلة',resourceOptions('studyyears')],['branchID','الفرع',resourceOptions('branches')]);
  if(['subjects','schedules','assignments'].includes(state.route))fields.push(['semesterNumber','الفصل',[[1,'الأول'],[2,'الثاني']]]);
  if(state.route==='schedules')fields.push(['sectionID','الشعبة',resourceOptions('sections')],['facultyMemberID','التدريسي',resourceOptions('facultymembers')],['classroomID','القاعة',resourceOptions('classrooms')],['dayOfWeek','اليوم',days],['lectureType','نوع المحاضرة',types],['groupName','المجموعة',[...new Set((state.rules.sections||[]).flatMap(x=>x.groups))].map(x=>[x,x])]);
  $('filters').innerHTML=fields.map(([key,label,options])=>`<label>${label}<select data-filter="${key}">${selectOptions(options,state.filters[key])}</select></label>`).join('');
  $('filters').onchange=event=>{ state.filters[event.target.dataset.filter]=event.target.value;state.page=1;renderTable(); };
}
function filtered(){
  const query=$('search').value.trim().toLocaleLowerCase();
  const module=modules[state.route];
  return state.rows.filter(envelope=>{
    const row=envelope.data, detail=state.route==='schedules'?state.details.find(x=>x.scheduleID===row.scheduleID):null;
    const joined={...row,...detail};
    for(const [key,value] of Object.entries(state.filters)){
      if(!value)continue;
      if(key==='groupName'&&(joined.lectureType==='Theory'||!joined.groupName)){const base=value.replace(/\d+$/,'');if(joined.sectionName!==base)return false;}
      else if(String(joined[key]??'')!==value)return false;
    }
    return !query||[...module.fields.map(f=>display(f,f.lectureCount?lectureCounts(row)[f.key]:row[f.key])),...Object.values(detail||{})].join(' ').toLocaleLowerCase().includes(query);
  });
}
$('search').oninput=()=>{state.page=1;renderTable();};
const lockControl = row => `<label class="lecture-lock"><input type="checkbox" data-lecture-lock="${row.scheduleID}" ${row.isLocked?'checked':''} aria-label="تثبيت المحاضرة ${row.scheduleID}"> تثبيت</label>`;
$('table-host').addEventListener('change',async event=>{
  const box=event.target.closest('[data-lecture-lock]');if(!box)return;
  const envelope=state.rows.find(x=>String(x.data.scheduleID)===box.dataset.lectureLock);
  if(!envelope)return;
  if(state.busy){box.checked=envelope.data.isLocked;return;}
  state.busy=true;box.disabled=true;
  try{const updated=await api(`schedules/${box.dataset.lectureLock}/lock`,{method:'PUT',headers:{'If-Match':`"${envelope.version}"`},body:{locked:box.checked}});Object.assign(envelope,updated);notice(box.checked?'تم تثبيت المحاضرة.':'تم إلغاء تثبيت المحاضرة.');}
  catch(ex){box.checked=envelope.data.isLocked;notice(ex.message);}
  finally{state.busy=false;box.disabled=false;}
});
function renderTable(){
  if(!modules[state.route])return;
  const rows=filtered(), module=modules[state.route];$('result-count').textContent=`${fmt(rows.length)} سجل`;
  if(!rows.length){$('table-host').innerHTML='<div class="empty"><h3>لا توجد سجلات للعرض</h3><p>غيّر عوامل التصفية أو أضف سجلًا جديدًا.</p></div>';$('pagination').replaceChildren();return;}
  if(state.route==='schedules'&&state.view==='timetable'){renderTimetable(rows);$('pagination').replaceChildren();return;}
  const size=25,pages=Math.ceil(rows.length/size);state.page=Math.min(state.page,pages);
  const start=(state.page-1)*size;
  $('table-host').innerHTML=`<table><thead><tr><th>#</th>${module.fields.map(f=>`<th scope="col">${f.label}</th>`).join('')}<th class="actions-cell" scope="col">الإجراءات</th></tr></thead><tbody>${rows.slice(start,start+size).map((envelope,index)=>`<tr><td>${fmt(start+index+1)}</td>${module.fields.map(f=>`<td>${esc(display(f,f.lectureCount?lectureCounts(envelope.data)[f.key]:envelope.data[f.key]))}</td>`).join('')}<td class="actions-cell">${state.route==='schedules'?lockControl(envelope.data):''}<button data-edit="${esc(keyOf(state.route,envelope.data))}">تعديل</button><button class="delete" data-delete="${esc(keyOf(state.route,envelope.data))}">حذف</button></td></tr>`).join('')}</tbody></table>`;
  $('pagination').innerHTML=`<button class="secondary" id="previous" ${state.page===1?'disabled':''}>السابق</button><span>صفحة ${fmt(state.page)} من ${fmt(pages)}</span><button class="secondary" id="next" ${state.page===pages?'disabled':''}>التالي</button>`;
  $('previous').onclick=()=>{state.page--;renderTable();};$('next').onclick=()=>{state.page++;renderTable();};
}
function renderTimetable(envelopes){
  const ids=new Set(envelopes.map(x=>x.data.scheduleID));const rows=state.details.filter(x=>ids.has(x.scheduleID));
  const periods=[...new Set(rows.map(x=>`${x.startTime}|${x.endTime}`))].sort();
  $('table-host').innerHTML=`<table class="timetable"><thead><tr><th>الفترة</th>${days.map(x=>`<th>${x[1]}</th>`).join('')}</tr></thead><tbody>${periods.map(period=>`<tr><th>${period.split('|').map(time).join(' – ')}</th>${days.map(([day])=>`<td>${rows.filter(x=>x.dayOfWeek===day&&`${x.startTime}|${x.endTime}`===period).map(x=>`<div class="lesson ${x.lectureType==='Practical'?'practical':''}"><strong>${esc(x.subjectName)}</strong><span>${esc(x.facultyMemberName)}</span><span>${esc(x.classroomNumber)} · ${esc(x.sectionName)} ${esc(x.groupName)}</span><span>${esc(x.yearName)} · ف${x.semesterNumber}</span>${lockControl(state.rows.find(e=>e.data.scheduleID===x.scheduleID).data)}<button data-edit="${x.scheduleID}">تعديل</button></div>`).join('')}</td>`).join('')}</tr>`).join('')}</tbody></table>`;
}
$('table-host').onclick=event=>{
  const button=event.target.closest('button');if(!button||state.busy)return;
  const id=button.dataset.edit??button.dataset.delete;
  const row=state.rows.find(x=>String(keyOf(state.route,x.data))===id);if(!row)return;
  if(button.dataset.edit!==undefined)openEditor(row);else deleteRow(row);
};
function renderSpecialActions(){
  if(state.route!=='schedules'){$('special-actions').replaceChildren();return;}
  $('special-actions').innerHTML=`<button class="secondary ${state.view==='list'?'view-active':''}" id="list-view">قائمة</button><button class="secondary ${state.view==='timetable'?'view-active':''}" id="timetable-view">جدول أسبوعي</button><button class="secondary" id="export">تصدير PDF</button><button class="secondary" id="print">طباعة</button><button class="primary" id="generate">توليد جدول جديد</button><a class="secondary transfer-link" href="/data-transfer.html#backups">حفظ / استعادة نسخة</a>`;
  $('list-view').onclick=()=>{state.view='list';renderSpecialActions();renderTable();};$('timetable-view').onclick=()=>{state.view='timetable';renderSpecialActions();renderTable();};
  $('print').onclick=()=>{const old=state.view;state.view='timetable';renderTable();window.print();state.view=old;renderTable();};
  $('export').onclick=exportPdf;$('generate').onclick=generate;
}
async function exportPdf(){
  const button=$('export');button.disabled=true;
  try{const filters={...state.filters,q:$('search').value};const query=new URLSearchParams(Object.entries(filters).filter(([,value])=>value));
    const blob=await api(`schedules/export?${query}`,{blob:true});const url=URL.createObjectURL(blob);const link=document.createElement('a');link.href=url;link.download='University-Timetable.pdf';link.click();setTimeout(()=>URL.revokeObjectURL(url),10000);
  }catch(ex){notice(ex.message);}finally{button.disabled=false;}
}
async function generate(){
  if(state.busy)return;const button=$('generate');button.disabled=true;
  try{const snapshot=await api('schedules/generation-state');if(!await confirmAction(snapshot.warning,'توليد جدول جديد'))return;
    state.busy=true;$('loading').textContent='جارٍ توليد الجدول والتحقق من المحاضرات… لا تغلق التطبيق.';$('loading').hidden=false;
    const result=await api('schedules/generate',{method:'POST',body:{confirmReplacement:true,version:snapshot.version}});
    notice(`اكتمل توليد جدول جديد: ${fmt(result.createdCount)} محاضرة؛ واحتُفظ بعدد ${fmt(result.preservedCount)} من المحاضرات المثبتة.${result.safetyBackupPath?' نسخة الجدول السابق: '+result.safetyBackupPath:''}`);await load();
  }catch(ex){error('page-error',ex.message);}finally{state.busy=false;button.disabled=false;$('loading').textContent='جارٍ تحميل البيانات…';$('loading').hidden=true;}
}
$('add').onclick=()=>openEditor(null);
function openEditor(envelope){
  if(state.busy)return;
  state.editing=envelope;state.dirty=false;const module=modules[state.route];const row=envelope?.data||{};
  $('editor-title').textContent=`${envelope?'تعديل':'إضافة'} ${module.singular}`;error('editor-error','');
  $('editor-fields').innerHTML=module.fields.map(field=>{
    const value=field.lectureCount?lectureCounts(row)[field.key]:row[field.key]??'';let control;
    if(field.type==='select')control=`<select name="${field.key}" ${field.optional?'':'required'}>${selectOptions(field.resource?resourceOptions(field.resource):field.options,value,field.optional?'بدون / عام':'اختر…')}</select>`;
    else control=`<input name="${field.key}" type="${field.type}" value="${esc(field.type==='time'?time(value):value)}" ${field.optional?'':'required'} ${field.max?`maxlength="${field.max}"`:''} ${field.type==='number'?`min="${field.min}" step="${field.step}" ${field.maxValue?`max="${field.maxValue}"`:''}`:''} ${field.type==='time'?'step="60"':''}>`;
    return `<label>${field.label}${control}</label>`;
  }).join('');
  if(state.route==='subjects'){
    state.manualLectureCounts = new Set(['theoreticalLectureCount','practicalLectureCount'].filter(key=>row[key]!=null));
    $('editor-fields').insertAdjacentHTML('beforeend','<div><p>العدد أسبوعي، والعملي لكل مجموعة. يمكن تعديل العدد أو إعادة حسابه تلقائياً.</p><button type="button" id="reset-lecture-counts" class="secondary">إعادة الحساب تلقائياً</button></div>');
    $('reset-lecture-counts').onclick=()=>{state.manualLectureCounts.clear();updateSubjectLectureCounts();state.dirty=true;};
  }
  updateDependentFields(null,true);$('editor').showModal();
}
function updateDependentFields(changed,initial=false){
  const form=$('editor-form');const control=name=>form.elements.namedItem(name);
  const replace=(name,options,optional=false)=>{const select=control(name);if(!select)return;const current=select.value||(initial?state.editing?.data[name]:'');select.innerHTML=selectOptions(options,current,optional?'بدون / عام':'اختر…');};
  if(['subjects','sections','assignments'].includes(state.route)){
    const year=state.rules.years?.find(x=>String(x.id)===control('studyYearID').value);
    control('branchID').disabled=!!year?.usesGeneralSections;control('branchID').required=!!year?.usesBranches;
    if(year?.usesGeneralSections)control('branchID').value='';
  }
  if(state.route==='assignments'){
    const year=Number(control('studyYearID').value),branch=Number(control('branchID').value)||null;
    const semester=Number(control('semesterNumber').value);
    const subjects=(state.lookups.subjects||[]).filter(s=>s.studyYearID===year&&(s.branchID??null)===branch&&s.semesterNumber===semester);
    replace('subjectID',resourceOptions('subjects').filter(([id])=>subjects.some(s=>s.subjectID===id)));
    const sectionIds=state.rules.subjectSections?.find(s=>String(s.id)===control('subjectID').value)?.sectionIds||[];
    replace('sectionID',resourceOptions('sections').filter(([id])=>sectionIds.includes(id)),true);
    control('sectionID').options[0].textContent='جميع الشعب المطابقة';
    if(!initial&&changed==='subjectID'&&sectionIds.length===1)control('sectionID').value=sectionIds[0];
    return;
  }
  if(state.route!=='schedules')return;
  const subject=state.lookups.subjects?.find(x=>String(x.subjectID)===control('subjectID').value);
  const sectionIds=state.rules.subjectSections?.find(x=>x.id===subject?.subjectID)?.sectionIds||[];
  replace('sectionID',resourceOptions('sections').filter(([id])=>sectionIds.includes(id)));
  const counts=subject?lectureCounts(subject):null;
  const allowedTypes=counts?types.filter(([type])=>type==='Practical'?counts.practicalLectureCount>0:counts.theoreticalLectureCount>0):types;
  replace('lectureType',allowedTypes);
  if(allowedTypes.length===1)control('lectureType').value=allowedTypes[0][0];
  const facultyIds=state.lookups.assignments?.filter(x=>x.subjectID===subject?.subjectID&&(!x.sectionID||String(x.sectionID)===control('sectionID').value)&&(!x.lectureType||x.lectureType==='Both'||x.lectureType===control('lectureType').value)).map(x=>x.facultyMemberID)||[];
  replace('facultyMemberID',resourceOptions('facultymembers').filter(([id])=>facultyIds.includes(id)));
  const practical=control('lectureType').value==='Practical';
  const sectionRule=state.rules.sections?.find(x=>String(x.id)===control('sectionID').value);
  const groups=sectionRule?.groups||[];
  const wholeSection=!!sectionRule?.allowsWholeSectionPractical;
  replace('groupName',groups.map(x=>[x,x]),!practical||wholeSection);control('groupName').disabled=!practical||wholeSection;control('groupName').required=practical&&!wholeSection;
  if(practical&&wholeSection)control('groupName').options[0].textContent=`${state.lookups.sections?.find(x=>String(x.sectionID)===control('sectionID').value)?.sectionName||'A'} — كامل الشعبة`;
  if(!practical)control('groupName').value='';
}
function updateSubjectLectureCounts(){
  const form=$('editor-form');
  const row=state.editing?.data||{};
  const defaults=lectureCounts({theoreticalHours:row.theoreticalHours??0,practicalHours:row.practicalHours??0});
  for(const key of ['theoreticalLectureCount','practicalLectureCount'])
    if(!state.manualLectureCounts.has(key)) form.elements.namedItem(key).value=defaults[key];
}
$('editor-form').addEventListener('input',event=>{
  state.dirty=true;
  if(state.route!=='subjects')return;
  if(['theoreticalLectureCount','practicalLectureCount'].includes(event.target.name))state.manualLectureCounts.add(event.target.name);
});
$('editor-form').addEventListener('change',event=>{state.dirty=true;updateDependentFields(event.target.name);});
async function closeEditor(){if(state.busy)return;if(state.dirty&&!await confirmAction('سيتم تجاهل التغييرات غير المحفوظة. هل تريد الإغلاق؟'))return;$('editor').close();state.dirty=false;}
$('close-editor').onclick=closeEditor;$('cancel-editor').onclick=closeEditor;
$('editor').addEventListener('cancel',event=>{event.preventDefault();closeEditor();});
$('editor-form').onsubmit=async event=>{
  event.preventDefault();if(state.busy)return;
  const route=state.route,module=modules[route],values=Object.fromEntries(new FormData(event.target));const body={};
  for(const field of module.fields){let value=values[field.key]??'';if(value==='')value=field.optional?null:value;else if(field.type==='number'||field.resource||field.key==='semesterNumber')value=Number(value);else if(field.type==='time')value+=':00';body[field.key]=value;}
  if(route==='subjects'){
    body.creditUnits=state.editing?.data.creditUnits??0;
    body.theoreticalHours=state.editing?.data.theoreticalHours??0;
    body.practicalHours=state.editing?.data.practicalHours??0;
    for(const key of ['theoreticalLectureCount','practicalLectureCount'])
      if(!state.manualLectureCounts.has(key))body[key]=null;
  }
  state.busy=true;$('save').disabled=true;error('editor-error','');
  try{const current=state.editing;await api(route+(current?`/${keyOf(route,current.data)}`:''),{method:current?'PUT':'POST',body,headers:current?{'If-Match':`"${current.version}"`}:{}});
    state.dirty=false;$('editor').close();notice('تم حفظ البيانات بنجاح.');await load();
  }catch(ex){error('editor-error',ex.message);}finally{state.busy=false;$('save').disabled=false;}
};
async function deleteRow(envelope){
  const route=state.route;
  const message=route==='subjects'?'حذف المادة من قائمة المواد ومن التوليد الجديد؟ ستبقى التكليفات والمحاضرات المحفوظة دون حذف.':
    route==='assignments'&&envelope.data.isSubjectDeleted?'هل تريد حذف تكليف هذه المادة المحذوفة؟ ستبقى محاضراتها المحفوظة بأسماء التدريسيين ومواعيدها الحالية.':
    'سيُحذف هذا السجل من قاعدة البيانات المشتركة. يمنع النظام حذف السجلات المرتبطة عندما تتطلب قواعده ذلك.';
  if(!await confirmAction(message,'حذف السجل'))return;
  state.busy=true;
  try{await api(`${route}/${keyOf(route,envelope.data)}`,{method:'DELETE',headers:{'If-Match':`"${envelope.version}"`}});notice('تم حذف السجل.');await load();}
  catch(ex){error('page-error',ex.message);}finally{state.busy=false;}
}
window.addEventListener('beforeunload',event=>{if(state.dirty||state.busy){event.preventDefault();event.returnValue='';}});
function connection(){ $('connection').hidden=navigator.onLine; }
window.addEventListener('online',connection);window.addEventListener('offline',connection);connection();
let installEvent;
window.addEventListener('beforeinstallprompt',event=>{event.preventDefault();installEvent=event;$('install').hidden=false;});
$('install').onclick=async()=>{if(!installEvent)return;await installEvent.prompt();await installEvent.userChoice;installEvent=null;$('install').hidden=true;};
window.addEventListener('appinstalled',()=>{$('install').hidden=true;notice('تم تثبيت التطبيق.');});
let updateRequested=false;
if('serviceWorker'in navigator){
  navigator.serviceWorker.register('/service-worker.js').then(registration=>{
    const offer=()=>{$('update-banner').hidden=!(registration.waiting&&navigator.serviceWorker.controller);};offer();
    registration.addEventListener('updatefound',()=>{const isUpdate=!!navigator.serviceWorker.controller;registration.installing?.addEventListener('statechange',()=>{if(isUpdate)offer();});});
    $('update-app').onclick=async()=>{if(state.busy)return notice('انتظر اكتمال العملية الحالية قبل التحديث.');if(state.dirty&&!await confirmAction('يوجد عمل غير محفوظ. هل تريد إعادة تحميل التطبيق؟'))return;updateRequested=true;registration.waiting?.postMessage({type:'ACTIVATE'});};
  }).catch(()=>notice('تعذر تفعيل وضع التطبيق. يمكنك متابعة الاستخدام عبر المتصفح.'));
  let reloading=false;navigator.serviceWorker.addEventListener('controllerchange',()=>{$('update-banner').hidden=true;if(updateRequested&&!reloading){reloading=true;state.dirty=false;location.reload();}});
}
try{await refreshCsrf();await enter(await api('auth/me'));}catch(ex){showLogin();if(ex.status!==401)error('auth-error',ex.message);}
