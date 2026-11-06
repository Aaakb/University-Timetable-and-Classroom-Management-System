import { openAlternatives, clearAlternatives, canLeaveAlternatives } from './alternatives.js';
import { openPreferences, clearPreferences, canLeavePreferences } from './day-preferences.js';
import { openStudents, clearStudents } from './students.js';
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
  resetBranchEditor();
  clearStudents();
  clearPreferences();
  clearAlternatives();
  $('application').hidden=true; $('auth').hidden=false; $('auth-form').reset(); $('dashboard').replaceChildren(); $('table-host').replaceChildren(); $('editor-fields').replaceChildren(); $('filters').replaceChildren();
  accountDialog?.close();
  listImportDialog?.close();
  importedFaculty.clear();
  showImportedFaculty=false;
  if ($('editor').open) $('editor').close();
  if ($('confirmation').open) $('confirmation').close('cancel');
  document.body.classList.remove('menu-open');
}
async function enter(user) {
  await refreshCsrf();
  state.authenticated=true; $('auth').hidden=true; $('application').hidden=false;
  document.body.dataset.readOnly = user.canManage === false ? 'true' : 'false';
  $('manage-accounts').hidden = !user.canManage;
  $('user-name').textContent = user.fullName; $('auth-form').reset();
  await navigate();
}
let register = false;
$('auth-switch').hidden=true;
const accountButton=document.createElement('button'); accountButton.id='manage-accounts'; accountButton.className='secondary'; accountButton.textContent='إنشاء حساب'; accountButton.hidden=true; $('logout').before(accountButton);
const accountDialog=document.createElement('dialog'); accountDialog.innerHTML='<form id=account-form><h2>إنشاء حساب للقراءة</h2><label>الاسم الكامل<input name=fullName required maxlength=150></label><label>اسم المستخدم<input name=userName required minlength=3 maxlength=50></label><label>كلمة المرور<input name=password type=password required minlength=12 maxlength=512 autocomplete=new-password></label><label>تأكيد كلمة المرور<input name=confirmPassword type=password required maxlength=512 autocomplete=new-password></label><p id=account-error role=alert></p><button type=submit class=primary>إنشاء الحساب</button><button type=button id=account-close>إغلاق</button></form>'; document.body.append(accountDialog);
accountButton.onclick=()=>{accountDialog.querySelector('form').reset();$('account-error').textContent='';accountDialog.showModal();};
$('account-close').onclick=()=>accountDialog.close();
$('account-form').onsubmit=async event=>{event.preventDefault();const button=event.submitter;button.disabled=true;try{await api('auth/register',{method:'POST',body:Object.fromEntries(new FormData(event.target))});accountDialog.close();notice('تم إنشاء حساب للقراءة. يمنح مسؤول الخادم صلاحيات الإدارة من الإعدادات.');}catch(ex){$('account-error').textContent=ex.message;}finally{button.disabled=false;}};
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
const desktopNavigation=[
  ['branches','الفروع'],
  ['studyyears','المراحل والشعب والطلبة'],
  ['subjects','المواد'],
  ['classrooms','القاعات'],
  ['timeslots','الفترات الدراسية'],
  ['assignments','التكليفات التدريسية'],
  ['facultymembers','التدريسيون'],
  ['schedules','الجدول الدراسي']
];
$('navigation').innerHTML='<a href="#dashboard" data-route="dashboard"><span class="nav-icon">◈</span>نظرة عامة</a>'+desktopNavigation.map(([route,label])=>
  `<a href="#${route}" data-route="${route}"><span class="nav-icon">${modules[route].icon}</span>${label}</a>`).join('')
  +'<a href="#preferences" data-route="preferences"><span class="nav-icon">★</span>تفضيلات التدريسيين</a>'
  +'<a href="#alternatives" data-route="alternatives"><span class="nav-icon">▦</span>جداول بديلة</a>';
let pageEntranceAnimations = [];
let currentPageWipe;
let navigationId = 0;
let selectedSemester = '';
let importedFaculty = new Set();
let showImportedFaculty = false;
try {
  const saved = sessionStorage.getItem('academic-semester');
  if (saved === '1' || saved === '2') selectedSemester = saved;
} catch { /* Continue without persistence. */ }
const semesterRoutes = ['subjects', 'assignments', 'schedules'];
let navigating = false;
function startPageWipe() {
  if (matchMedia('(prefers-reduced-motion: reduce)').matches)
    return { covered: Promise.resolve(), reveal() {}, cancel() {} };

  const overlay = document.createElement('div');
  overlay.className = 'page-wipe';
  overlay.setAttribute('aria-hidden', 'true');
  overlay.innerHTML = '<div class="page-wipe__accent"></div><div class="page-wipe__surface"></div>';
  document.body.append(overlay);
  const panels = [...overlay.children];
  let animations = [];
  let cancelled = false;
  // GSAP power4.inOut is a fifth-degree ease; sample it for native animations.
  const frames = (start, end) => Array.from({ length: 41 }, (_, index) => {
    const t = index / 40;
    const eased = t < .5 ? 16 * t ** 5 : 1 - (-2 * t + 2) ** 5 / 2;
    return { offset: t, transform: `translateY(${start + (end - start) * eased}%)` };
  });
  const play = (start, end) => {
    animations = panels.map((panel, index) => panel.animate(frames(start, end), {
      duration: 500, delay: index * 50, fill: 'forwards', easing: 'linear'
    }));
    return Promise.all(animations.map(animation => animation.finished.catch(() => {})));
  };
  return {
    covered: play(100, 0),
    reveal() { if (!cancelled) play(0, -100).then(() => overlay.remove()); },
    cancel() { cancelled = true; animations.forEach(animation => animation.cancel()); overlay.remove(); }
  };
}
function animatePageEntrance(section) {
  if (matchMedia('(prefers-reduced-motion: reduce)').matches) return;
  const selector = section.id === 'dashboard'
    ? ':scope > .hero, :scope > .stats > .stat, :scope > .dashboard-bottom > .panel'
    : ':scope > .panel > .toolbar, :scope > .panel > .filters, :scope > .panel > .table-meta, :scope > .panel > .table-scroll, :scope > .panel > .pagination';
  const elements = [...section.querySelectorAll(selector)].filter(element => element.getClientRects().length);
  pageEntranceAnimations = elements.map((element, index) => element.animate(
    [{ opacity: 0.35, transform: 'translateY(40px)' }, { opacity: 1, transform: 'translateY(0)' }],
    { duration: 500, delay: Math.min(index * 50, 150), easing: 'cubic-bezier(.16, 1, .3, 1)', fill: 'backwards' }
  ));
}
const academicRoute=()=>['studyyears','sections','students'].includes(state.route);
const inlineEditorRoute=()=>['studyyears','sections','subjects','classrooms','timeslots','assignments','facultymembers'].includes(state.route);
async function navigate() {
  if (!state.authenticated) return;
  const [route,query]=location.hash.slice(1).split('?');
  if(state.route==='preferences' && route!=='preferences' && !canLeavePreferences()){
    history.replaceState(null,'','#preferences');
    return;
  }
  if(state.route==='alternatives' && route!=='alternatives' && !canLeaveAlternatives()){
    history.replaceState(null,'','#alternatives');
    return;
  }
  const currentNavigation = ++navigationId;
  navigating = true;
  currentPageWipe?.cancel();
  const wipe = currentPageWipe = startPageWipe();
  pageEntranceAnimations.forEach(animation => animation.cancel());
  pageEntranceAnimations = [];
  if($('editor').classList.contains('academic-inline')){
    if(route!==state.route && (state.busy || state.dirty && !confirm('هل تريد الانتقال دون حفظ التعديلات؟'))){
      history.replaceState(null,'',`#${state.route}`);
      if(currentNavigation===navigationId){navigating=false;wipe.cancel();}
      return;
    }
    $('editor').close();
    state.dirty=false;
  }
  if(state.route==='preferences' && route!=='preferences')clearPreferences();
  if(state.route==='alternatives' && route!=='alternatives')clearAlternatives();
  if(['preferences','alternatives'].includes(route) && state.route!==route){
    const previousRoute=state.route==='dashboard' && $('dashboard').hidden ? 'schedules' : state.route;
    const previousQuery=previousRoute==='students' && state.studentParams.size ? '?'+state.studentParams : '';
    $(route==='preferences'?'day-close':'alt-close').href='#'+previousRoute+previousQuery;
  }
  if(route!=='branches')resetBranchEditor();
  const studentAccess=route==='students' && document.body.dataset.readOnly!=='true';
  state.route=modules[route]||['dashboard','preferences','alternatives'].includes(route)||studentAccess ? route : route==='students' ? 'studyyears' : 'dashboard';
  state.studentParams=new URLSearchParams(query);
  state.selectedLecture=null;
  state.page=1; state.filters=semesterRoutes.includes(state.route) ? {semesterNumber:selectedSemester} : {}; $('search').value=''; state.rows=[]; state.details=[];
  document.body.classList.remove('menu-open'); $('menu').setAttribute('aria-expanded','false');
  const module=modules[state.route];
  const preferences=state.route==='preferences';
  const alternatives=state.route==='alternatives';
  $('refresh').hidden=preferences||alternatives;
  $('page-title').textContent=alternatives?'قارن الجدول الحالي ببدائل مختلفة قبل الحفظ':preferences?'تفضيلات أيام التدريسيين':state.route==='branches'?'إدارة الفروع':academicRoute()?'المراحل والشعب والطلبة':state.route==='subjects'?'إدارة المواد':state.route==='classrooms'?'إدارة القاعات':state.route==='timeslots'?'إدارة الفترات الدراسية':state.route==='assignments'?'إدارة التكليفات التدريسية':state.route==='facultymembers'?'إدارة التدريسيين':state.route==='schedules'?'إدارة الجداول الدراسية':module?.title||'نظرة عامة'; $('breadcrumb').textContent=alternatives?'جداول بديلة':preferences?'تفضيلات التدريسيين':module?.title||'نظرة عامة';
  $('page-description').textContent=alternatives?'اعرض البدائل وقارنها قبل اعتماد الجدول.':preferences?'اختر الأيام، وسنُعيد توزيع الجدول الحالي وفق التفضيلات المتوفرة.':module?.description||'كل ما تحتاجه لتنظيم الفصل الدراسي، في مكان واحد.';
  const students=state.route==='students';
  $('students-panel').hidden=$('students-results').hidden=!students;
  $('resource-results').hidden=students;
  $('branch-workspace').hidden=state.route!=='branches'||document.body.dataset.readOnly==='true';
  $('branch-list-title').hidden=state.route!=='branches';
  $('subject-list-title').hidden=state.route!=='subjects';
  $('classroom-list-title').hidden=state.route!=='classrooms';
  $('timeslot-list-title').hidden=state.route!=='timeslots';
  $('assignment-list-title').hidden=state.route!=='assignments';
  $('faculty-list-title').hidden=state.route!=='facultymembers';
  $('add').hidden=!module||['branches','schedules'].includes(state.route)||inlineEditorRoute(); $('add').textContent=state.route==='schedules'?'إضافة محاضرة يدويًا':`+ إضافة ${module?.singular||'سجل'}`;
  // Hide the previous screen until the new route has finished loading.
  $('dashboard').hidden=true; $('management').hidden=true; $('preferences-workspace').hidden=true; $('alternatives-workspace').hidden=true;
  for (const link of $('navigation').children) { const active=link.dataset.route===state.route || academicRoute()&&link.dataset.route==='studyyears'; link.classList.toggle('active',active); if(active)link.setAttribute('aria-current','page');else link.removeAttribute('aria-current'); }
  $('add').disabled=true;
  await Promise.all([load(), wipe.covered]);
  if (state.authenticated && currentNavigation === navigationId && $('page-error').hidden) {
    const section = $(alternatives ? 'alternatives-workspace' : preferences ? 'preferences-workspace' : module||students ? 'management' : 'dashboard');
    section.hidden=false;
    if(inlineEditorRoute() && document.body.dataset.readOnly!=='true')openEditor(null);
    animatePageEntrance(section);
  }
  if (currentNavigation === navigationId) { navigating=false; $('add').disabled=false; }
  wipe.reveal();
}
window.addEventListener('hashchange',navigate);
$('refresh').onclick=()=>($('dashboard').hidden && $('management').hidden ? navigate() : load());
async function load() {
  const epoch=++state.epoch, route=state.route;
  $('loading').hidden=false; $('refresh').disabled=true; $('add').disabled=true; error('page-error','');
  try {
    if(route==='alternatives'){
      await openAlternatives();
    } else if(route==='preferences'){
      await openPreferences();
    } else if(route==='students'){
      renderSpecialActions();
      await openStudents(state.studentParams);
    } else if(route==='dashboard') {
      const data=await api('overview'); if(epoch!==state.epoch)return; renderDashboard(data);
    } else {
      // Reload only the dependencies this page uses, alongside its own records.
      const dependencies={sections:['studyyears','branches'],subjects:['studyyears','branches'],assignments:['studyyears','branches','subjects','facultymembers','sections'],schedules:Object.keys(modules).filter(x=>x!=='schedules')}[route]||[];
      const [entries,rows,rules,details]=await Promise.all([
        Promise.all(dependencies.map(async key=>[key,(await all(key)).map(x=>x.data)])),
        serverPaged()?api(`${route}?page=${state.page}&pageSize=25&q=${encodeURIComponent($('search').value.trim())}`):all(route),['sections','subjects','assignments','schedules'].includes(route)?api('overview/rules'):Promise.resolve({}),
        route==='schedules'?api('schedules/details'):Promise.resolve({items:[]})]);
      if(epoch!==state.epoch)return;
      const records=serverPaged()?rows.items:rows; state.total=serverPaged()?rows.total:records.length;
      if(serverPaged() && !records.length && state.total && state.page>1){state.page--;return await load();}
      state.lookups={...Object.fromEntries(entries),[route]:records.map(x=>x.data)}; state.rows=records; state.rules=rules; state.details=details.items;
      renderFilters(); renderSpecialActions(); renderTable();
    }
    $('last-refresh').textContent=`آخر تحديث: ${new Date().toLocaleTimeString('ar-IQ',{hour:'2-digit',minute:'2-digit'})}`;
  }catch(ex){ if(epoch===state.epoch)error('page-error',ex.message); }
  finally{ if(epoch===state.epoch){ $('loading').hidden=true; $('refresh').disabled=false; $('add').disabled=navigating; } }
}
function renderDashboard(data){
  $('dashboard').innerHTML=`<div class="hero"><div><span class="eyebrow">تنظيم أفضل. يوم دراسي أوضح.</span><h2>جدول الجامعة، أمامك بالكامل.</h2><p>تابع المحاضرات والموارد المتاحة، وانتقل مباشرة إلى الجدول لإدارة توزيع المواد والقاعات.</p></div><a class="primary" href="#schedules">عرض الجدول الدراسي ←</a></div><div class="stats">${[['classrooms','القاعات والمختبرات','مساحات تعليمية'],['subjects','المواد الدراسية','مواد الفصل الدراسي'],['facultyMembers','أعضاء الهيئة التدريسية','تدريسيون مسجلون'],['sections','الشعب الدراسية','شعب في المراحل المختلفة']].map(([key,label,foot])=>`<div class="stat"><span class="stat-label">${label}</span><strong>${fmt(data[key])}</strong><span class="stat-foot">${foot}</span></div>`).join('')}</div><div class="dashboard-bottom"><section class="panel"><div class="section-head"><h3>وصول سريع</h3><span>إدارة الفصل الدراسي</span></div><div class="quick-links">${[['subjects','المواد والمحاضرات'],['assignments','توزيع المواد على التدريسيين'],['classrooms','السعة وأنواع القاعات'],['timeslots','بداية المحاضرات ونهايتها']].map(([key,label])=>`<a class="quick-link" href="#${key}">${modules[key].icon} &nbsp;${modules[key].title}<small>${label} ←</small></a>`).join('')}</div></section><section class="panel"><div class="section-head"><h3>حالة الإعداد</h3><span>من البيانات الحالية</span></div><div class="readiness"><strong>${fmt(data.schedules)}</strong><h3>محاضرة في الجدول</h3><p>${fmt(data.assignments)} تكليفًا تدريسيًا مسجلًا.</p><p class="${data.unassignedSubjects?'warning':''}">${data.unassignedSubjects?`${fmt(data.unassignedSubjects)} مادة تحتاج إلى تكليف تدريسي قبل التوليد.`:'كل المواد مرتبطة بتكليفات تدريسية. تتحقق بقية شروط الجدولة عند التوليد.'}</p><p>حدّث البيانات لعرض آخر تغييرات تطبيق سطح المكتب.</p></div></section></div>`;
}
function lookupLabel(resource,row){
  if(!row)return '—';
  if(resource==='timeslots')return `${time(row.startTime)} – ${time(row.endTime)}`;
  if(resource==='sections')return `${row.sectionName} · ${lookupValue('studyyears',row.studyYearID)}${row.branchID?' · '+lookupValue('branches',row.branchID):''}`;
  if(resource==='subjects')return `${row.subjectName} · ${lookupValue('studyyears',row.studyYearID)} · ${row.semesterNumber===0?'فصل غير محدد':`ف${row.semesterNumber}`}${row.branchID?' · '+lookupValue('branches',row.branchID):''}`;
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
function matchesFilter(row,key,value){
  if(key==='semesterNumber' && value==='unclassified')return row.studyYearID===0;
  if(!value)return true;
  if(key==='groupName'){
    const group=String(row.groupName||'').trim().toUpperCase();
    const selected=String(value).trim().toUpperCase();
    if(row.lectureType==='Theory'||!group){
      const section=/^[AB][12]$/.test(selected)?selected[0]:selected;
      return String(row.sectionName||'').trim().toUpperCase()===section;
    }
    return group===selected;
  }
  return String(row[key]??'')===String(value);
}
function renderFilters(){
  const fields=[];
  const add=(key,label,options)=>{
    if(state.filters[key]&&!options.some(([value])=>String(value)===state.filters[key]))state.filters[key]='';
    fields.push([key,label,options]);
  };
  const academic=['subjects','sections','schedules','assignments'].includes(state.route);
  const unclassified=state.route==='subjects'&&state.filters.semesterNumber==='unclassified';
  if(academic){
    if(unclassified){state.filters.studyYearID='';state.filters.branchID='';}
    add('studyYearID','المرحلة',resourceOptions('studyyears'));
    if(state.route!=='sections')add('semesterNumber','الفصل',state.route==='subjects'?[[0,'المضافة حديثًا (الفصل غير محدد)'],['unclassified','مواد غير مصنّفة'],[1,'الأول'],[2,'الثاني']]:[[1,'الأول'],[2,'الثاني']]);
    const year=state.filters.studyYearID;
    const semester=state.filters.semesterNumber;
    const source=state.route==='schedules'?(semester?state.lookups.subjects:state.lookups.sections):state.rows.map(x=>x.data);
    const branchIds=new Set((source||[]).filter(row=>matchesFilter(row,'studyYearID',year)&&matchesFilter(row,'semesterNumber',semester)).map(row=>String(row.branchID)));
    add('branchID','الفرع',resourceOptions('branches').filter(([id])=>!year&&!semester||branchIds.has(String(id))));
  }
  if(state.route==='schedules'){
    const semesterSubjectIds=new Set((state.lookups.subjects||[]).filter(row=>matchesFilter(row,'semesterNumber',state.filters.semesterNumber)).map(row=>row.subjectID));
    const semesterSectionIds=new Set((state.rules.subjectSections||[]).filter(rule=>semesterSubjectIds.has(rule.id)).flatMap(rule=>rule.sectionIds));
    const sections=(state.lookups.sections||[]).filter(row=>matchesFilter(row,'studyYearID',state.filters.studyYearID)&&matchesFilter(row,'branchID',state.filters.branchID)&&(!state.filters.semesterNumber||semesterSectionIds.has(row.sectionID)));
    add('sectionID','الشعبة',sections.map(row=>[row.sectionID,lookupLabel('sections',row)]));
    const selectedSections=sections.filter(row=>matchesFilter(row,'sectionID',state.filters.sectionID));
    const sectionIds=new Set(selectedSections.map(row=>row.sectionID));
    const groups=[...new Set((state.rules.sections||[]).filter(rule=>sectionIds.has(rule.id)).flatMap(rule=>rule.groups))].sort();
    add('groupName','المجموعة',groups.map(group=>[group,group]));
    const academicKeys=['studyYearID','branchID','semesterNumber','sectionID','groupName'];
    const relevant=state.details.filter(row=>academicKeys.every(key=>matchesFilter(row,key,state.filters[key])));
    const facultyIds=new Set(relevant.map(row=>String(row.facultyMemberID)));
    add('facultyMemberID','التدريسي',resourceOptions('facultymembers').filter(([id])=>facultyIds.has(String(id))));
    const roomIds=new Set(relevant.filter(row=>matchesFilter(row,'facultyMemberID',state.filters.facultyMemberID)).map(row=>String(row.classroomID)));
    add('classroomID','القاعة',resourceOptions('classrooms').filter(([id])=>roomIds.has(String(id))));
    const roomRows=relevant.filter(row=>matchesFilter(row,'facultyMemberID',state.filters.facultyMemberID)&&matchesFilter(row,'classroomID',state.filters.classroomID));
    add('dayOfWeek','اليوم',days.filter(([day])=>roomRows.some(row=>row.dayOfWeek===day)));
    const dayRows=roomRows.filter(row=>matchesFilter(row,'dayOfWeek',state.filters.dayOfWeek));
    add('lectureType','نوع المحاضرة',types.filter(([type])=>dayRows.some(row=>row.lectureType===type)));
  }
  if(state.route==='schedules'){
    const order=['semesterNumber','studyYearID','sectionID','groupName','facultyMemberID','branchID','classroomID','dayOfWeek','lectureType'];
    fields.sort((a,b)=>order.indexOf(a[0])-order.indexOf(b[0]));
  }
  $('filters').innerHTML=fields.map(([key,label,options])=>`<label>${label}<select data-filter="${key}" ${unclassified&&['studyYearID','branchID'].includes(key)?'disabled':''}>${selectOptions(options,state.filters[key])}</select></label>`).join('');
  $('filters').onchange=event=>{const key=event.target.dataset.filter;state.filters[key]=event.target.value;
    if (key==='semesterNumber' && semesterRoutes.includes(state.route) && ['', '1', '2'].includes(state.filters[key])) {
      selectedSemester=state.filters[key];
      try { sessionStorage.setItem('academic-semester',selectedSemester); } catch { /* Storage is optional. */ }
    }
    state.page=1;renderFilters();
    if(state.route==='assignments'&&key==='semesterNumber'&&!state.dirty&&!state.editing&&$('editor').open)updateDependentFields(key);
    $('filters').querySelector(`[data-filter="${key}"]`)?.focus();renderTable();};
}
function filtered(){
  const query=$('search').value.trim().toLocaleLowerCase();
  const module=modules[state.route];
  return state.rows.filter(envelope=>{
    const row=envelope.data;
    if(state.route==='facultymembers' && showImportedFaculty && !importedFaculty.has(row.facultyMemberID))return false;
    const detail=state.route==='schedules'?state.details.find(x=>x.scheduleID===row.scheduleID):null;
    const joined={...row,...detail};
    for(const [key,value] of Object.entries(state.filters)){
      if(!matchesFilter(joined,key,value))return false;
    }
    return !query||[...module.fields.map(f=>display(f,f.lectureCount?lectureCounts(row)[f.key]:row[f.key])),...Object.values(detail||{})].join(' ').toLocaleLowerCase().includes(query);
  });
}
let searchTimer; $('search').oninput=()=>{state.page=1;if(serverPaged()){clearTimeout(searchTimer);searchTimer=setTimeout(()=>load(),200);}else renderTable();};
const serverPaged = () => ['branches','studyyears','classrooms','facultymembers'].includes(state.route) &&
  !(state.route==='facultymembers' && showImportedFaculty);
const lockControl = row => `<label class="lecture-lock"><input type="checkbox" data-lecture-lock="${row.scheduleID}" ${row.isLocked?'checked':''} aria-label="تثبيت المحاضرة ${row.scheduleID}"> تثبيت</label>`;
function updateTableScrollHint() {
  const host = $('table-host');
  $('table-scroll-hint').hidden = host.scrollWidth <= host.clientWidth + 1;
}
if (window.ResizeObserver) new ResizeObserver(updateTableScrollHint).observe($('table-host'));
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
  $('table-scroll-hint').hidden=true;
  const rows=serverPaged()?state.rows:filtered(), module=modules[state.route];$('result-count').textContent=`${fmt(serverPaged()?state.total:rows.length)} سجل`;
  if(state.route==='schedules'){
    const details=new Map(state.details.map(row=>[row.scheduleID,row]));
    const dayOrder=new Map(days.map(([day],index)=>[day,index]));
    const yearOrder={'first year':1,'second year':2,'third year':3,'fourth year':4};
    const compareText=(a,b)=>String(a??'').localeCompare(String(b??''));
    rows.sort((a,b)=>{
      const x=details.get(a.data.scheduleID)||a.data,y=details.get(b.data.scheduleID)||b.data;
      return (x.semesterNumber??0)-(y.semesterNumber??0)||(dayOrder.get(x.dayOfWeek)??99)-(dayOrder.get(y.dayOfWeek)??99)||compareText(x.startTime,y.startTime)||
        (yearOrder[String(x.yearName??'').trim().toLowerCase()]??99)-(yearOrder[String(y.yearName??'').trim().toLowerCase()]??99)||
        compareText(x.branchName,y.branchName)||compareText(x.sectionName,y.sectionName)||compareText(x.groupName,y.groupName)||x.scheduleID-y.scheduleID;
    });
  }
  if(state.route==='schedules'){
    state.selectedLecture=null;
    $('schedule-edit').disabled=$('schedule-delete').disabled=true;
  }
  if(state.route==='facultymembers')for(const button of document.querySelectorAll('[data-faculty-export]'))button.disabled=!rows.length;
  if(!rows.length){$('table-host').innerHTML='<div class="empty"><h3>لا توجد سجلات للعرض</h3><p>غيّر عوامل التصفية أو أضف سجلًا جديدًا.</p></div>';$('pagination').replaceChildren();return;}
  if(state.route==='schedules'&&state.view==='timetable'){renderTimetable(rows);$('pagination').replaceChildren();requestAnimationFrame(updateTableScrollHint);return;}
  const size=25,pages=Math.ceil((serverPaged()?state.total:rows.length)/size);state.page=Math.min(state.page,pages);
  const start=serverPaged()?0:(state.page-1)*size;
  if(state.route==='schedules'){
    const details=new Map(state.details.map(row=>[row.scheduleID,row]));
    $('table-host').innerHTML=`<table class="schedule-list"><thead><tr>${['ت','اليوم','الوقت','المادة','التدريسي','القاعة','النوع','الشعبة','المجموعة','المرحلة','الفرع','الفصل الدراسي','المعرف','التثبيت','الإجراءات'].map(label=>`<th scope="col"${label==='الإجراءات'?' class="actions-cell"':''}>${label}</th>`).join('')}</tr></thead><tbody>${rows.slice(start,start+size).map((envelope,index)=>{
      const row=envelope.data,d=details.get(row.scheduleID)||{};
      const cells=[fmt((serverPaged()?(state.page-1)*size:start)+index+1),textOf(days,row.dayOfWeek),`${time(d.startTime)} – ${time(d.endTime)}`,d.subjectName,d.facultyMemberName,d.classroomNumber,textOf(types,row.lectureType),d.sectionName,d.groupName||'—',d.yearName,d.branchName||'—',d.semesterNumber,row.scheduleID];
      return `<tr data-lecture-row="${row.scheduleID}">${cells.map((value,index)=>`<td>${index===0&&document.body.dataset.readOnly!=='true'?`<button type="button" class="academic-row-button" data-select-lecture="${row.scheduleID}" aria-pressed="false" aria-label="تحديد محاضرة ${esc(d.subjectName??row.scheduleID)}">${esc(value)}</button>`:esc(value??'—')}</td>`).join('')}<td>${lockControl(row)}</td><td class="actions-cell"><button data-edit="${esc(row.scheduleID)}">تعديل</button><button class="delete" data-delete="${esc(row.scheduleID)}">حذف</button></td></tr>`;
    }).join('')}</tbody></table>`;
  }else if(state.route==='facultymembers'){
    const readOnly=document.body.dataset.readOnly==='true';
    $('table-host').innerHTML=`<table><thead><tr><th scope="col">ت</th><th scope="col">الاسم الكامل</th><th scope="col">اللقب العلمي</th></tr></thead><tbody>${rows.slice(start,start+size).map(({data},index)=>{
      const number=fmt((serverPaged()?(state.page-1)*size:start)+index+1);
      return `<tr><td>${readOnly?number:`<button class="academic-row-button" type="button" data-edit="${data.facultyMemberID}" aria-label="تعديل ${esc(data.fullName)}">${number}</button>`}</td><td>${esc(data.fullName)}</td><td>${esc(data.academicTitle||'—')}</td></tr>`;
    }).join('')}</tbody></table>`;
  }else if(state.route==='assignments'){
    const readOnly=document.body.dataset.readOnly==='true';
    const columns=['ت','التدريسي','المادة','الفصل الدراسي','المرحلة','التخصص','الشعبة','نوع التكليف'];
    const subjectName=row=>row.subjectName??lookupValue('subjects',row.subjectID);
    const facultyName=row=>lookupValue('facultymembers',row.facultyMemberID);
    const type=module.fields.find(field=>field.key==='lectureType');
    rows.sort(({data:a},{data:b})=>(a.semesterNumber??0)-(b.semesterNumber??0)||
      subjectName(a).localeCompare(subjectName(b),'ar')||facultyName(a).localeCompare(facultyName(b),'ar'));
    $('table-host').innerHTML=`<table><thead><tr>${columns.map(label=>`<th scope="col">${label}</th>`).join('')}</tr></thead><tbody>${rows.slice(start,start+size).map(({data},index)=>{
      const number=fmt(start+index+1);
      const semester=data.semesterNumber===1?'الأول':data.semesterNumber===2?'الثاني':'غير محدد';
      const cells=[facultyName(data),subjectName(data),semester,lookupValue('studyyears',data.studyYearID),lookupValue('branches',data.branchID),data.sectionID?lookupValue('sections',data.sectionID):'جميع الشعب المطابقة',display(type,data.lectureType)];
      return `<tr><td>${readOnly?number:`<button class="academic-row-button" type="button" data-edit="${keyOf('assignments',data)}" aria-label="تعديل تكليف ${esc(subjectName(data))}">${number}</button>`}</td>${cells.map(value=>`<td>${esc(value)}</td>`).join('')}</tr>`;
    }).join('')}</tbody></table>`;
  }else if(state.route==='timeslots'){
    const readOnly=document.body.dataset.readOnly==='true';
    $('table-host').innerHTML=`<table><thead><tr><th scope="col">ت</th><th scope="col">معرّف الفترة</th><th scope="col">وقت البدء</th><th scope="col">وقت الانتهاء</th></tr></thead><tbody>${rows.slice(start,start+size).map(({data},index)=>{
      const number=fmt(start+index+1);
      const first=time(data.startTime), last=time(data.endTime);
      return `<tr><td>${readOnly?number:`<button class="academic-row-button" type="button" data-edit="${data.timeSlotID}" aria-label="تعديل الفترة ${esc(first)} – ${esc(last)}">${number}</button>`}</td><td>${esc(data.timeSlotID)}</td><td dir="ltr">${esc(first)}</td><td dir="ltr">${esc(last)}</td></tr>`;
    }).join('')}</tbody></table>`;
  }else if(state.route==='classrooms'){
    const readOnly=document.body.dataset.readOnly==='true';
    const columns=['ت','معرّف القاعة','رقم القاعة','السعة','نوع القاعة'];
    const types=module.fields.find(field=>field.key==='roomType');
    $('table-host').innerHTML=`<table><thead><tr>${columns.map(label=>`<th scope="col">${label}</th>`).join('')}</tr></thead><tbody>${rows.map(({data},index)=>{
      const number=fmt((state.page-1)*size+index+1);
      const cells=[data.classroomID,data.classroomNumber,data.capacity,display(types,data.roomType)];
      return `<tr><td>${readOnly?number:`<button class="academic-row-button" type="button" data-edit="${data.classroomID}" aria-label="تعديل ${esc(data.classroomNumber)}">${number}</button>`}</td>${cells.map(value=>`<td>${esc(value)}</td>`).join('')}</tr>`;
    }).join('')}</tbody></table>`;
  }else if(state.route==='subjects'){
    const readOnly=document.body.dataset.readOnly==='true';
    const columns=['ت','المعرّف','اسم المادة','المرحلة الدراسية','الفرع','الفصل الدراسي','عدد النظري','عدد العملي'];
    $('table-host').innerHTML=`<table><thead><tr>${columns.map(label=>`<th scope="col">${label}</th>`).join('')}</tr></thead><tbody>${rows.slice(start,start+size).map(({data},index)=>{
      const number=fmt(start+index+1);
      const counts=lectureCounts(data);
      const semester=data.semesterNumber===0?'غير محدد':data.semesterNumber===1?'الأول':'الثاني';
      const cells=[data.subjectID,data.subjectName,lookupValue('studyyears',data.studyYearID),lookupValue('branches',data.branchID),semester,counts.theoreticalLectureCount,counts.practicalLectureCount];
      return `<tr><td>${readOnly?number:`<button class="academic-row-button" type="button" data-edit="${data.subjectID}" aria-label="تعديل ${esc(data.subjectName)}">${number}</button>`}</td>${cells.map(value=>`<td>${esc(value)}</td>`).join('')}</tr>`;
    }).join('')}</tbody></table>`;
  }else if(academicRoute()){
    const readOnly=document.body.dataset.readOnly==='true';
    const sections=state.route==='sections';
    const title=sections?'اسم الشعبة':'اسم المرحلة';
    $('table-host').innerHTML=`<table><thead><tr><th scope="col">ت</th><th scope="col">${title}${readOnly?'':' · عرض الطلبة'}</th>${sections?'<th scope="col">المرحلة</th><th scope="col">الفرع</th><th scope="col">عدد الطلبة</th>':''}</tr></thead><tbody>${rows.slice(start,start+size).map(({data},index)=>{
      const stage=data.studyYearID;
      const number=fmt((serverPaged()?(state.page-1)*size:start)+index+1);
      const name=esc(sections?data.sectionName:data.yearName);
      const url=`#students?stage=${stage}${sections?`&section=${data.sectionID}&from=sections`:''}`;
      const groupAllowed=!sections || (stage<3
        ? !data.branchID && ['A','B'].includes(data.sectionName)
        : !!data.branchID && data.sectionName==='A');
      const nameCell=!readOnly && stage>=1 && stage<=4 && groupAllowed ? `<a class="academic-student-link" href="${esc(url)}">${name}</a>` : name;
      return `<tr><td>${readOnly?number:`<button class="academic-row-button" type="button" data-edit="${keyOf(state.route,data)}" aria-label="تعديل ${name}">${number}</button>`}</td><td>${nameCell}</td>${sections?`<td>${esc(lookupValue('studyyears',stage))}</td><td>${esc(lookupValue('branches',data.branchID))}</td><td>${fmt(data.studentCount)}</td>`:''}</tr>`;
    }).join('')}</tbody></table>`;
  }else if(state.route==='branches'){
    const readOnly=document.body.dataset.readOnly==='true';
    $('table-host').innerHTML=`<table><thead><tr><th scope="col">ت</th><th scope="col">اسم الفرع</th></tr></thead><tbody>${rows.map(({data},index)=>`<tr><td>${fmt((state.page-1)*size+index+1)}</td><td>${readOnly?esc(data.branchName):`<button type="button" class="branch-row-button" data-branch-id="${data.branchID}" aria-pressed="${selectedBranch?.data.branchID===data.branchID}">${esc(data.branchName)}</button>`}</td></tr>`).join('')}</tbody></table>`;
  }else $('table-host').innerHTML=`<table><thead><tr><th>#</th>${module.fields.map(f=>`<th scope="col">${f.label}</th>`).join('')}<th class="actions-cell" scope="col">الإجراءات</th></tr></thead><tbody>${rows.slice(start,start+size).map((envelope,index)=>`<tr><td>${fmt((serverPaged()?(state.page-1)*size:start)+index+1)}</td>${module.fields.map(f=>`<td>${esc(display(f,f.lectureCount?lectureCounts(envelope.data)[f.key]:envelope.data[f.key]))}</td>`).join('')}<td class="actions-cell"><button data-edit="${esc(keyOf(state.route,envelope.data))}">تعديل</button><button class="delete" data-delete="${esc(keyOf(state.route,envelope.data))}">حذف</button></td></tr>`).join('')}</tbody></table>`;
  requestAnimationFrame(updateTableScrollHint);
  $('pagination').innerHTML=`<button class="secondary" id="previous" ${state.page===1?'disabled':''}>السابق</button><span>صفحة ${fmt(state.page)} من ${fmt(pages)}</span><button class="secondary" id="next" ${state.page===pages?'disabled':''}>التالي</button>`;
  $('previous').onclick=()=>{state.page--;serverPaged()?load():renderTable();};$('next').onclick=()=>{state.page++;serverPaged()?load():renderTable();};
}
function renderTimetable(envelopes){
  const records=new Map(envelopes.map(x=>[x.data.scheduleID,x.data]));
  const rows=state.details.filter(x=>records.has(x.scheduleID));
  const visibleDays=days;
  const semesters=new Set(rows.map(row=>row.semesterNumber));
  const slots=[...new Map(rows.map(row=>[`${row.semesterNumber}|${row.timeSlotID}`,row])).values()]
    .sort((a,b)=>a.semesterNumber-b.semesterNumber||String(a.startTime).localeCompare(String(b.startTime))||String(a.endTime).localeCompare(String(b.endTime))||a.timeSlotID-b.timeSlotID);
  const compareText=(a,b)=>String(a??'').localeCompare(String(b??''));
  $('table-host').innerHTML=`<table class="timetable"><thead><tr><th scope="col">الفترة</th>${visibleDays.map(([,label])=>`<th scope="col">${esc(label)}</th>`).join('')}</tr></thead><tbody>${slots.map(slot=>`<tr data-semester="${esc(slot.semesterNumber)}"><th scope="row">${semesters.size>1?`<span class="semester-label">الفصل ${esc(slot.semesterNumber)}</span>`:''}<span dir="ltr">${time(slot.startTime)} – ${time(slot.endTime)}</span></th>${visibleDays.map(([day])=>{
    const entries=rows.filter(row=>row.dayOfWeek===day&&row.semesterNumber===slot.semesterNumber&&row.timeSlotID===slot.timeSlotID)
      .sort((a,b)=>compareText(a.sectionName,b.sectionName)||compareText(a.groupName,b.groupName)||compareText(a.subjectName,b.subjectName)||a.scheduleID-b.scheduleID);
    return `<td>${entries.length?`<div class="timetable-cell" ${entries.length>1?'tabindex="0" aria-label="محاضرات الفترة، مرر لعرض المزيد"':''}>${entries.map(x=>`<div class="lesson ${x.lectureType==='Practical'?'practical':''}" data-semester="${esc(x.semesterNumber)}"><strong>${esc(x.subjectName)}</strong><span>${esc(x.facultyMemberName)}</span><span>${esc(x.classroomNumber)} · ${esc(x.sectionName)} · ${x.lectureType==='Practical'?'عملي':'نظري'} ${esc(x.groupName||'')}</span><span>${esc(x.yearName)}</span>${lockControl(records.get(x.scheduleID))}<button data-edit="${x.scheduleID}">تعديل</button></div>`).join('')}</div>`:'<span class="timetable-empty">—</span>'}</td>`;
  }).join('')}</tr>`).join('')}</tbody></table>`;
}
let selectedBranch=null;
const branchForm=$('branch-form');
function resetBranchEditor(){
  selectedBranch=null;
  branchForm.reset();
  $('branch-id').value='تلقائي';
  $('branch-edit').disabled=$('branch-delete').disabled=true;
  $('table-host').querySelector('[data-branch-id][aria-pressed=true]')?.setAttribute('aria-pressed','false');
  error('branch-error','');
}
$('branch-clear').onclick=()=>{resetBranchEditor();branchForm.elements.branchName.focus();};
branchForm.addEventListener('submit',async event=>{
  event.preventDefault();
  if(state.busy)return;
  const edit=event.submitter?.value==='edit';
  const branch=selectedBranch;
  if(edit&&!branch)return;
  const branchName=branchForm.elements.branchName.value.trim();
  if(!branchName){error('branch-error','أدخل اسم الفرع.');return;}
  state.busy=true;
  const submitter=event.submitter;
  if(submitter)submitter.disabled=true;
  error('branch-error','');
  try{
    await api(edit?`branches/${branch.data.branchID}`:'branches',{
      method:edit?'PUT':'POST',body:{branchName},
      headers:edit?{'If-Match':`"${branch.version}"`}:{}
    });
    resetBranchEditor();
    notice('تم حفظ بيانات الفرع.');
    await load();
  }catch(ex){error('branch-error',ex.message);}
  finally{
    state.busy=false;
    if(submitter)submitter.disabled=edit&&!selectedBranch;
  }
});
$('branch-delete').onclick=async ()=>{
  if(state.busy||!selectedBranch)return;
  if(await deleteRow(selectedBranch))resetBranchEditor();
};
$('table-host').onclick=event=>{
  const button=event.target.closest('button');if(!button||state.busy)return;
  if(button.dataset.branchId){
    selectedBranch=state.rows.find(({data})=>String(data.branchID)===button.dataset.branchId)??null;
    if(!selectedBranch)return;
    $('table-host').querySelector('[data-branch-id][aria-pressed=true]')?.setAttribute('aria-pressed','false');
    button.setAttribute('aria-pressed','true');
    $('branch-id').value=selectedBranch.data.branchID;
    branchForm.elements.branchName.value=selectedBranch.data.branchName;
    $('branch-edit').disabled=$('branch-delete').disabled=false;
    error('branch-error','');
    return;
  }
  if(button.dataset.selectLecture){
    state.selectedLecture=state.rows.find(({data})=>String(data.scheduleID)===button.dataset.selectLecture)??null;
    $('table-host').querySelector('[data-select-lecture][aria-pressed=true]')?.setAttribute('aria-pressed','false');
    button.setAttribute('aria-pressed','true');
    $('schedule-edit').disabled=$('schedule-delete').disabled=!state.selectedLecture;
    return;
  }
  const id=button.dataset.edit??button.dataset.delete;
  const row=state.rows.find(x=>String(keyOf(state.route,x.data))===id);if(!row)return;
  if(button.dataset.edit!==undefined)openEditor(row);else deleteRow(row);
};
const listImportDialog=document.createElement('dialog');
listImportDialog.setAttribute('aria-labelledby','list-import-title');
listImportDialog.innerHTML='<form id="inline-list-import"><h2 id="list-import-title"></h2><p id="list-import-description"></p><label>اختر الملف<input name="file" type="file" required></label><p id="list-import-error" class="error" role="alert" hidden></p><div class="dialog-footer"><button type="button" class="secondary" id="close-list-import">إلغاء</button><button class="primary" type="submit">استيراد القائمة</button></div></form>';
document.body.append(listImportDialog);
const listImportForm=$('inline-list-import');
$('close-list-import').onclick=()=>listImportDialog.close();
listImportDialog.addEventListener('cancel',event=>{if(state.busy)event.preventDefault();});
function openListImport(kind, label) {
  listImportForm.reset();
  listImportForm.dataset.kind=kind;
  $('list-import-title').textContent=label;
  $('list-import-description').textContent=kind==='subjects'
    ? 'استخدم عمودَي «المادة» و«المرحلة». اختر الفصل بعد الاستيراد.'
    : 'استخدم عمود «اسم التدريسي» ويمكنك إضافة عمود «اللقب العلمي».';
  listImportForm.elements.file.accept=kind==='subjects'?'.xlsx':'.xlsx,.csv';
  error('list-import-error','');
  listImportDialog.showModal();
}
listImportForm.onsubmit=async event=>{
  event.preventDefault();
  if(state.busy)return;
  const kind=listImportForm.dataset.kind;
  if(!confirm('حفظ بيانات القائمة المستوردة؟'))return;
  state.busy=true;
  const button=event.submitter;
  button.disabled=$('close-list-import').disabled=true;
  error('list-import-error','');
  try {
    const result=await api(`list-imports/${kind}`,{method:'POST',body:new FormData(listImportForm)});
    listImportDialog.close();
    if(kind==='subjects')state.filters={semesterNumber:'0'};
    if(kind==='faculty' && result.ids?.length){
      importedFaculty=new Set(result.ids);
      showImportedFaculty=true;
    }
    state.page=1;
    $('search').value='';
    await load();
    notice(`أضيف ${result.added}، وتُخطّي ${result.skipped}.`);
  } catch(ex) { error('list-import-error',ex.message); }
  finally { state.busy=false;button.disabled=$('close-list-import').disabled=false; }
};
function renderSpecialActions(){
  const actions=$('special-actions');
  const schedule=state.route==='schedules';
  for(const id of ['schedule-commands','schedule-filters','schedule-results-heading'])$(id).hidden=!schedule;
  if(schedule)$('schedule-filters').append($('filters'));
  else actions.closest('.toolbar').after($('filters'));
  const tabs=$('academic-navigation');
  tabs.hidden=!academicRoute();
  if (academicRoute()) {
    const links=[['#studyyears','المراحل'],['#sections','الشعب'],['#students','الطلبة']];
    actions.replaceChildren();
    tabs.innerHTML=links.filter(([url])=>!url.includes('students') || document.body.dataset.readOnly!=='true')
      .map(([url,label])=>`<a class="secondary" href="${url}" ${url==='#'+state.route?'aria-current="page"':''}>${label}</a>`).join('');
    return;
  }
  if (state.route==='subjects' || state.route==='facultymembers') {
    const kind=state.route==='subjects'?'subjects':'faculty';
    const label=kind==='subjects'?'استيراد مواد من Excel':'استيراد Excel أو CSV';
    actions.replaceChildren();
    if(document.body.dataset.readOnly!=='true'){
      const button=document.createElement('button');
      button.className='secondary';
      button.textContent=label;
      button.onclick=()=>openListImport(kind,label);
      actions.append(button);
      const reviewImport=document.createElement('a');
      reviewImport.className='secondary';
      reviewImport.href='/data-transfer.html';
      reviewImport.textContent='استيراد مع معاينة';
      actions.append(reviewImport);
      if(kind==='faculty')for(const [format,label] of [['pdf','تصدير PDF'],['xlsx','تصدير Excel']]){
        const exportButton=document.createElement('button');
        exportButton.type='button';
        exportButton.className='secondary';
        exportButton.textContent=label;
        exportButton.dataset.facultyExport=format;
        exportButton.onclick=()=>exportFaculty(format,exportButton);
        actions.append(exportButton);
      }
      if(kind==='faculty' && importedFaculty.size){
        const checkbox=document.createElement('input');
        checkbox.type='checkbox';
        checkbox.checked=showImportedFaculty;
        checkbox.onchange=()=>{showImportedFaculty=checkbox.checked;state.page=1;load();};
        const label=document.createElement('label');
        label.append(checkbox,' عرض آخر دفعة مستوردة');
        actions.append(label);
      }
    }
    return;
  }
  if(state.route!=='schedules'){actions.replaceChildren();return;}
  actions.replaceChildren();
  $('schedule-commands').innerHTML=`<div class="actions">
    <button class="primary" id="generate">توليد جدول جديد</button>
    <button class="secondary" id="schedule-add">إضافة محاضرة يدويًا</button>
    <button class="secondary" id="schedule-edit" disabled>تعديل</button>
    <button class="danger" id="schedule-delete" disabled>حذف</button>
    <button class="secondary" id="export">تصدير PDF</button>
    <button class="secondary" id="print">طباعة</button>
    <button class="secondary" id="schedule-backup">حفظ نسخة الجدول</button>
    <a class="secondary transfer-link" href="/data-transfer.html#backups">استعادة نسخة</a>
  </div>`;
  $('schedule-view-controls').innerHTML=`<button class="secondary ${state.view==='list'?'view-active':''}" id="list-view" aria-pressed="${state.view==='list'}">السجلات</button><button class="secondary ${state.view==='timetable'?'view-active':''}" id="timetable-view" aria-pressed="${state.view==='timetable'}">الجدول الأسبوعي</button>`;
  for(const [id,view] of [['list-view','list'],['timetable-view','timetable']]){
    $(id).onclick=()=>{state.view=view;renderSpecialActions();renderTable();};
  }
  $('schedule-add').onclick=()=>openEditor(null);
  $('schedule-edit').onclick=()=>{if(state.selectedLecture)openEditor(state.selectedLecture);};
  $('schedule-delete').onclick=()=>{if(state.selectedLecture)deleteRow(state.selectedLecture);};
  $('schedule-backup').onclick=downloadScheduleBackup;
  $('print').onclick=()=>{const old=state.view;state.view='timetable';renderTable();window.print();state.view=old;renderTable();};
  $('export').onclick=exportPdf;
  $('generate').onclick=generate;
}
async function exportFaculty(format,button){
  button.disabled=true;
  try{
    const ids=showImportedFaculty?filtered().map(row=>row.data.facultyMemberID):null;
    const query=showImportedFaculty?null:$('search').value.trim();
    const blob=await api('faculty-roster/export',{method:'POST',body:{format,query,ids},blob:true});
    const url=URL.createObjectURL(blob);
    const link=document.createElement('a');
    link.href=url;
    link.download=`faculty.${format}`;
    link.click();
    setTimeout(()=>URL.revokeObjectURL(url),10000);
  }catch(ex){notice(ex.message);}
  finally{button.disabled=false;}
}
async function downloadScheduleBackup(){
  const button=$('schedule-backup');
  button.disabled=true;
  try{
    const blob=await api('timetable-backups/download',{blob:true});
    const url=URL.createObjectURL(blob);
    const link=document.createElement('a');
    link.href=url;
    link.download='Timetable.json';
    link.click();
    setTimeout(()=>URL.revokeObjectURL(url),10000);
  }catch(ex){notice(ex.message);}
  finally{button.disabled=false;}
}
async function exportPdf(){
  const button=$('export');button.disabled=true;
  try{const filters={...state.filters,q:$('search').value};const query=new URLSearchParams(Object.entries(filters).filter(([,value])=>value));
    const blob=await api(`schedules/export?${query}`,{blob:true});const url=URL.createObjectURL(blob);const link=document.createElement('a');link.href=url;link.download='University-Timetable.pdf';link.click();setTimeout(()=>URL.revokeObjectURL(url),10000);
  }catch(ex){notice(ex.message);}finally{button.disabled=false;}
}
async function generate(){
  if(state.busy)return;const button=$('generate');button.disabled=true;
  const semesterNumber=Number(state.filters.semesterNumber)||null;
  try{const snapshot=await api('schedules/generation-state'+(semesterNumber?`?semesterNumber=${semesterNumber}`:''));if(!await confirmAction(snapshot.warning,'توليد جدول جديد'))return;
    state.busy=true;$('loading').textContent='جارٍ توليد الجدول والتحقق من المحاضرات… لا تغلق التطبيق.';$('loading').hidden=false;
    const result=await api('schedules/generate',{method:'POST',body:{confirmReplacement:true,version:snapshot.version,semesterNumber}});
    notice(`اكتمل توليد جدول جديد: ${fmt(result.createdCount)} محاضرة؛ واحتُفظ بعدد ${fmt(result.preservedCount)} من المحاضرات المثبتة.${result.safetyBackupPath?' نسخة الجدول السابق: '+result.safetyBackupPath:''}`);await load();
  }catch(ex){error('page-error',ex.message);}finally{state.busy=false;button.disabled=false;$('loading').textContent='جارٍ تحميل البيانات…';$('loading').hidden=true;}
}
$('add').onclick=()=>openEditor(null);
function openEditor(envelope){
  if(state.busy)return;
  state.editing=envelope;state.dirty=false;const module=modules[state.route];const row=envelope?.data||{};
  $('editor-title').textContent=`${envelope?'تعديل':'إضافة'} ${module.singular}`;error('editor-error','');
  $('editor-fields').innerHTML=module.fields.map(field=>{
    const value=field.lectureCount?lectureCounts(row)[field.key]:field.boolean?(row[field.key]??false):row[field.key]??(state.route==='subjects'&&field.key==='semesterNumber'?selectedSemester:field.initial??'');let control;
    const choices = field.resource ? resourceOptions(field.resource) :
      state.route==='subjects' && field.key==='semesterNumber' ? field.options.filter(([id])=>id!==0) : field.options;
    if(state.route==='subjects'&&field.key==='mergePracticalGroups')
      return `<label class="subject-merge"><input name="${field.key}" type="checkbox" value="true"${value?' checked':''}>${field.label}</label>`;
    if(field.type==='select')control=`<select name="${field.key}" ${field.optional?'':'required'}>${selectOptions(choices,value,field.optional?'بدون / عام':'اختر…')}</select>`;
    else control=`<input name="${field.key}" type="${field.type}" value="${esc(field.type==='time'?time(value):value)}" ${field.optional?'':'required'} ${field.max?`maxlength="${field.max}"`:''} ${field.type==='number'?`min="${field.min}" step="${field.step}" ${field.maxValue?`max="${field.maxValue}"`:''}`:''} ${field.type==='time'?'step="60"':''}>`;
    return `<label>${field.label}${control}</label>`;
  }).join('');
  if(state.route==='facultymembers'){
    const input=$('editor-form').elements.academicTitle;
    const titles=document.createElement('select');
    titles.setAttribute('aria-label','اختيار لقب');
    titles.hidden=true;
    titles.add(new Option('اختيار لقب',''));
    input.after(titles);
    titles.onchange=()=>{
      if(!titles.value)return;
      input.value=titles.value;
      input.dispatchEvent(new Event('input',{bubbles:true}));
      titles.value='';
    };
    api('overview/academic-titles').then(values=>{
      for(const value of values)titles.add(new Option(value,value));
      titles.hidden=values.length===0;
    }).catch(()=>{}); // Manual title entry remains available.
  }
  if(['subjects','classrooms','timeslots','facultymembers'].includes(state.route)){
    const id=document.createElement('label');
    id.innerHTML=`معرّف السجل<input value="${esc(row[module.key]??'تلقائي')}" readonly>`;
    $('editor-fields').prepend(id);
  }
  if(state.route==='subjects'){
    state.manualLectureCounts = new Set(['theoreticalLectureCount','practicalLectureCount'].filter(key=>row[key]!=null));
    $('editor-fields').insertAdjacentHTML('beforeend','<div><p>العدد أسبوعي، والعملي لكل مجموعة. يمكن تعديل العدد أو إعادة حسابه تلقائياً.</p><button type="button" id="reset-lecture-counts" class="secondary">إعادة الحساب تلقائياً</button></div>');
    $('reset-lecture-counts').onclick=()=>{state.manualLectureCounts.clear();updateSubjectLectureCounts();state.dirty=true;};
  }
  if(state.route==='schedules')arrangeScheduleEditor(row);
  updateDependentFields(null,true);
  const editor=$('editor');
  if(inlineEditorRoute()){
    $('academic-editor').append(editor);
    editor.classList.add('academic-inline');
    $('editor-title').textContent={subjects:'بيانات المادة',classrooms:'بيانات القاعة',timeslots:'بيانات الفترة الدراسية',assignments:'بيانات التكليف التدريسي',facultymembers:'بيانات التدريسي',sections:'بيانات الشعبة',studyyears:'بيانات المرحلة'}[state.route];
    $('save').textContent=envelope?'تعديل':'إضافة';
    $('cancel-editor').textContent='مسح الحقول';
    $('academic-delete').hidden=!envelope;
    $('close-editor').hidden=true;
    if(!editor.open)editor.show();
  }else{
    document.body.append(editor);
    editor.classList.remove('academic-inline');
    $('save').textContent='حفظ التغييرات';
    $('cancel-editor').textContent='إلغاء';
    $('academic-delete').hidden=true;
    $('close-editor').hidden=false;
    editor.showModal();
  }
}
function arrangeScheduleEditor(row){
  const fields=$('editor-fields');
  const form=$('editor-form');
  const subject=state.lookups.subjects.find(item=>item.subjectID===row.subjectID);
  const academicFields=[
    ['schedule-year','المرحلة',resourceOptions('studyyears').filter(([id])=>id>0),subject?.studyYearID],
    ['schedule-branch','الفرع',resourceOptions('branches'),subject?.branchID]
  ];
  const labels=new Map();
  for(const [id,title,options,value] of academicFields){
    const label=document.createElement('label');
    label.textContent=title;
    const select=document.createElement('select');
    select.id=id;
    select.innerHTML=selectOptions(options,value);
    label.append(select);
    labels.set(id,label);
  }
  const categories=[
    ['المادة والتدريسي',['subjectID','facultyMemberID','lectureType','groupName']],
    ['الشعبة والقاعة',['schedule-year','schedule-branch','sectionID','dayOfWeek','timeSlotID','classroomID']]
  ];
  for(const [title,names] of categories){
    const block=document.createElement('fieldset');
    const legend=document.createElement('legend');
    legend.textContent=title;
    block.append(legend);
    for(const name of names)block.append(labels.get(name)??form.elements.namedItem(name).closest('label'));
    fields.append(block);
  }
  const id=document.createElement('label');
  id.innerHTML=`معرّف المحاضرة<input value="${esc(row.scheduleID??'تلقائي')}" readonly>`;
  fields.prepend(id);
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
    const semester=Number(state.filters.semesterNumber);
    const subjects=(state.lookups.subjects||[]).filter(s=>year>0&&s.studyYearID===year&&(s.branchID??null)===branch&&
      s.semesterNumber>0&&(!semester||s.semesterNumber===semester));
    subjects.sort((a,b)=>a.subjectName.localeCompare(b.subjectName,'ar')||a.semesterNumber-b.semesterNumber);
    replace('subjectID',subjects.map(subject=>[subject.subjectID,`${subject.subjectName} — الفصل ${subject.semesterNumber}`]));
    const sectionIds=state.rules.subjectSections?.find(s=>String(s.id)===control('subjectID').value)?.sectionIds||[];
    replace('sectionID',resourceOptions('sections').filter(([id])=>sectionIds.includes(id)),true);
    control('sectionID').options[0].textContent='جميع الشعب المطابقة';
    if(!initial&&changed==='subjectID'&&sectionIds.length===1)control('sectionID').value=sectionIds[0];
    return;
  }
  if(state.route!=='schedules')return;
  const yearControl=$('schedule-year'), branchControl=$('schedule-branch');
  const currentSubject=state.lookups.subjects?.find(x=>String(x.subjectID)===control('subjectID').value);
  if((initial||changed==='subjectID')&&currentSubject){
    yearControl.value=currentSubject.studyYearID;
    branchControl.value=currentSubject.branchID??'';
  }
  if(changed==='schedule-year')branchControl.value='';
  const yearRule=state.rules.years?.find(item=>String(item.id)===yearControl.value);
  branchControl.disabled=!!yearRule?.usesGeneralSections;
  if(branchControl.disabled)branchControl.value='';
  const candidates=(state.lookups.subjects||[]).filter(item=>item.studyYearID>0&&item.semesterNumber>0&&
    (!yearControl.value||String(item.studyYearID)===yearControl.value)&&
    (!branchControl.value||String(item.branchID)===branchControl.value));
  replace('subjectID',candidates.map(item=>[item.subjectID,lookupLabel('subjects',item)]));
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
  const wholeSection=!!sectionRule?.allowsWholeSectionPractical||!!subject?.mergePracticalGroups;
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
$('editor-form').addEventListener('change',event=>{state.dirty=true;updateDependentFields(event.target.name||event.target.id);});
async function closeEditor(){if(state.busy)return;if(state.dirty&&!await confirmAction('سيتم تجاهل التغييرات غير المحفوظة. هل تريد الإغلاق؟'))return;$('editor').close();state.dirty=false;}
$('close-editor').onclick=closeEditor;$('cancel-editor').onclick=()=>{
  if(inlineEditorRoute()){if(!state.busy)openEditor(null);}
  else closeEditor();
};
$('academic-delete').onclick=async()=>{
  if(state.busy||!state.editing)return;
  if(await deleteRow(state.editing) && inlineEditorRoute())openEditor(null);
};
$('editor').addEventListener('cancel',event=>{event.preventDefault();closeEditor();});
$('editor-form').onsubmit=async event=>{
  event.preventDefault();if(state.busy)return;
  const route=state.route,module=modules[route],values=Object.fromEntries(new FormData(event.target));const body={};
  for(const field of module.fields){let value=values[field.key]??'';if(field.boolean)value=value==='true';else if(value==='')value=field.optional?null:value;else if(field.type==='number'||field.resource||field.key==='semesterNumber')value=Number(value);else if(field.type==='time')value+=':00';body[field.key]=value;}
  if(route==='subjects'){
    body.creditUnits=state.editing?.data.creditUnits??0;
    body.theoreticalHours=state.editing?.data.theoreticalHours??0;
    body.practicalHours=state.editing?.data.practicalHours??0;
    for(const key of ['theoreticalLectureCount','practicalLectureCount'])
      if(!state.manualLectureCounts.has(key))body[key]=null;
  }
  state.busy=true;$('save').disabled=true;error('editor-error','');
  try{const current=state.editing;await api(route+(current?`/${keyOf(route,current.data)}`:''),{method:current?'PUT':'POST',body,headers:current?{'If-Match':`"${current.version}"`}:{}});
    state.dirty=false;$('editor').close();notice('تم حفظ البيانات بنجاح.');await load();if(route===state.route&&inlineEditorRoute()){state.busy=false;openEditor(null);}
  }catch(ex){error('editor-error',ex.message);}finally{state.busy=false;$('save').disabled=false;}
};
async function deleteRow(envelope){
  const route=state.route;
  const message=route==='subjects'?'حذف المادة وجميع تكليفاتها ومحاضراتها من الجدول، بما فيها المحاضرات المثبتة؟':
    route==='assignments'?'حذف التكليف ومحاضرات هذا التدريسي لهذه المادة، بما فيها المحاضرات المثبتة؟ تبقى المادة نفسها دون حذف.':
    'سيُحذف هذا السجل من قاعدة البيانات المشتركة. يمنع النظام حذف السجلات المرتبطة عندما تتطلب قواعده ذلك.';
  if(!await confirmAction(message,'حذف السجل'))return;
  state.busy=true;
  try{await api(`${route}/${keyOf(route,envelope.data)}`,{method:'DELETE',headers:{'If-Match':`"${envelope.version}"`}});notice('تم حذف السجل.');await load();return true;}
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
