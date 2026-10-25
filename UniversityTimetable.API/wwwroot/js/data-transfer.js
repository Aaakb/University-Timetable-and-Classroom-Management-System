import { api, refreshCsrf } from './api.js';
import { days } from './schema.js';
const dayNames=Object.fromEntries(days);
const $ = id => document.getElementById(id);
const esc = value => String(value ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
let schemas=[], workbook=null, preview=null, revision='', restore=null, sourcePage=1, busy=false, controller=null, authenticated=false, dirty=false;
const mode = () => 0;
const selectedSheet = () => workbook?.sheets.find(s => s.index === Number($('import-sheet').value));
const truth = value => /^(true|1|yes|نعم)$/i.test(value);
function message(text) { $('transfer-status').textContent=text; }
function table(host, headings, rows) {
  $(host).innerHTML=`<table><thead><tr>${headings.map(h=>`<th>${esc(h)}</th>`).join('')}</tr></thead><tbody>${rows.map(row=>`<tr>${row.map(c=>`<td>${esc(c)}</td>`).join('')}</tr>`).join('')}</tbody></table>`;
}
async function run(work, saving=false) {
  if(busy || !authenticated)return;
  busy=true;controller=new AbortController();$('transfer-error').hidden=true;updateActions();
  $('cancel-transfer').hidden=false;
  message(saving?'جارٍ الحفظ والتحقق…':'جارٍ تنفيذ العملية…');
  try { await work(controller.signal); }
  catch(ex) {
    if(saving){preview=null;revision='';restore=null;}
    $('transfer-error').textContent=controller.signal.aborted
      ? saving?'توقف انتظار العملية. حدّث البيانات للتحقق من نتيجة الحفظ قبل إعادة المحاولة.':'أُوقف انتظار العملية. أعد التحقق قبل الحفظ.'
      : ex.message;
    $('transfer-error').hidden=false;
    if(controller.signal.aborted){preview=null;revision='';}
    message('');
  }
  finally{busy=false;controller=null;$('cancel-transfer').hidden=true;updateActions();}
}
function confirmSave(text) {
  $('transfer-confirm-text').textContent=text;
  const dialog=$('transfer-confirm');dialog.returnValue='cancel';dialog.showModal();
  return new Promise(resolve=>dialog.addEventListener('close',()=>resolve(dialog.returnValue==='yes'),{once:true}));
}
function invalidate() { preview=null;revision='';dirty=!!workbook;$('import-summary').textContent='تغيّرت الخيارات أو البيانات. أعد التحقق قبل الحفظ.';$('import-report').replaceChildren();$('import-result').replaceChildren();updateActions(); }
function updateActions() {
  for(const id of ['import-controls','backup-controls','restore-controls'])$(id).disabled=busy||!authenticated;
  $('sheet-controls').disabled=$('preview-controls').disabled=busy||!authenticated||!workbook;
  $('save-import').disabled=busy||!preview?.isValid||!preview.changes.length||([2,3].includes(mode())&&!preview.generationCompleted);
  $('restore-backup').disabled=busy||!restore?.preview.canRestore;
  $('skip-import').hidden=mode()!==0;$('import-updates').disabled=busy||mode()!==0;
  $('import-semester').disabled=busy||mode()===0;$('import-seconds').disabled=busy||mode()!==3;
  $('sheet-schema').disabled=busy||mode()!==0;
}
function visibleSheets() { const session=$('import-session').value;return workbook?.sheets.filter(s=>!session||!s.studySession||s.studySession===session)||[]; }
function configureSheet(sheet) {
  const schema=mode()===0?(sheet.schema&&sheet.schema!=='Timetable'?sheet.schema:schemas[0].name):'Timetable';
  sheet.schema=schema;sheet.mapping=[...sheet.mappings[schema]];
}
function renderSheets() {
  const sheets=visibleSheets(),current=$('import-sheet').value;
  $('sheet-selection').innerHTML=sheets.map(s=>`<label><input type="checkbox" data-sheet-select="${s.index}" ${s.included?'checked':''}>${esc(s.name)} (${s.rows.length})</label>`).join('');
  $('import-sheet').innerHTML=sheets.map(s=>`<option value="${s.index}">${esc(s.name)}</option>`).join('');
  if(sheets.some(s=>String(s.index)===current))$('import-sheet').value=current;
  sourcePage=1;renderSource();
}
function renderSource() {
  const sheet=selectedSheet();if(!sheet){$('mapping-table').replaceChildren();$('source-table').replaceChildren();return;}
  const allowed=schemas.filter(s=>mode()===0?s.name!=='Timetable':s.name==='Timetable');
  $('sheet-schema').innerHTML=allowed.map(s=>`<option value="${esc(s.name)}">${esc(s.label)}</option>`).join('');$('sheet-schema').value=sheet.schema;
  const schema=schemas.find(s=>s.name===sheet.schema);
  $('mapping-table').innerHTML=`<table><thead><tr><th>عمود الملف</th><th>الحقل المطلوب</th></tr></thead><tbody>${sheet.columns.map((column,i)=>`<tr><td>${esc(column)}</td><td><select data-map="${i}" aria-label="ربط ${esc(column)}"><option value="">تجاهل العمود</option>${schema.columns.map(c=>`<option value="${esc(c.name)}" ${sheet.mapping[i]===c.name?'selected':''}>${esc(c.label)}${schema.required.includes(c.name)?' *':''}</option>`).join('')}</select></td></tr>`).join('')}</tbody></table>`;
  const pages=Math.max(1,Math.ceil(sheet.rows.length/50));sourcePage=Math.min(sourcePage,pages);const offset=(sourcePage-1)*50;
  $('source-table').innerHTML=`<table><thead><tr><th>صف المصدر</th>${sheet.columns.map(c=>`<th>${esc(c)}</th>`).join('')}</tr></thead><tbody>${sheet.rows.slice(offset,offset+50).map((row,r)=>`<tr><th>${sheet.sourceRows[r+offset]}</th>${row.map((value,c)=>`<td><input data-row="${r+offset}" data-col="${c}" aria-label="${esc(sheet.columns[c])} صف ${sheet.sourceRows[r+offset]}" ${sheet.mapping[c]==='Locked'?`type="checkbox" ${truth(value)?'checked':''}`:`type="text" maxlength="32000" value="${esc(value)}"`}></td>`).join('')}</tr>`).join('')}</tbody></table>`;
  $('source-page').textContent=`صفحة ${sourcePage} من ${pages} — ${sheet.rows.length} صف`;
  $('source-prev').disabled=sourcePage===1;$('source-next').disabled=sourcePage===pages;
}
function renderPreview(value) {
  revision=value.revision;preview=value.preview;
  const p=preview;
  $('import-summary').textContent=`${p.isValid?'نجح التحقق':'توجد أخطاء تمنع الحفظ'} — ${p.changes.length} تغيير، ${p.lectures.length} محاضرة، ${p.issues.length} خطأ، ${p.skippedRows.length} ملاحظة لصفوف متخطاة.\nالمحاضرات المتبقية: ${p.remainingLectureCount}. ${p.generationCompleted?'اكتملت المعالجة.':''} ${p.repairSearchStatus}`;
  const report=[...p.issues.map(i=>[i.sheet,i.row,'خطأ',i.column,i.message]),...p.repairableConflicts.map(i=>[i.sheet,i.row,'يحتاج إصلاحًا',i.column,i.message]),...p.readingNotes.map(i=>[i.sheet,i.row,'مراجعة المصدر',i.column,i.message]),...p.skippedRows.map(i=>[i.sheet,i.row,'تم التخطي',i.column,i.message]),...p.changes.map(i=>[i.sheet,i.row,({Add:'إضافة',Update:'تحديث',Unchanged:'دون تغيير','Lock existing':'تثبيت'}[i.action]||i.action),'',i.key]),...p.unscheduledLectures.map(i=>['',0,'غير مجدولة','',i])];
  table('import-report',['الورقة','الصف','الحالة','الحقل','التفاصيل'],report);
  if(p.placementChanges.length)table('import-result',['المادة','التدريسي','الشعبة','المجموعة','مثبتة','اليوم قبل','الفترة قبل','القاعة قبل','اليوم بعد','الفترة بعد','القاعة بعد','تغيّرت'],p.placementChanges.map(c=>[c.subjectName,c.facultyName,c.sectionName,c.groupName,c.locked?'نعم':'لا',dayNames[c.beforeDay]||c.beforeDay,c.beforePeriod,c.beforeClassroom,dayNames[c.afterDay]||c.afterDay,c.afterPeriod,c.afterClassroom,c.changed?'نعم':'لا']));
  else table('import-result',['المادة','التدريسي','الشعبة','القاعة','اليوم','الفترة','مثبتة'],p.lectures.map(l=>[l.subjectName,l.facultyName,l.sectionName,l.classroomName,dayNames[l.schedule.dayOfWeek]||l.schedule.dayOfWeek,l.period,l.locked?'نعم':'لا']));
  message('اكتملت المعاينة. راجع التقرير والنتيجة قبل الحفظ.');updateActions();
}
function download(blob,name){const url=URL.createObjectURL(blob),link=document.createElement('a');link.href=url;link.download=name;document.body.append(link);link.click();link.remove();setTimeout(()=>URL.revokeObjectURL(url),30000);}
function formFile(id){const file=$(id).files[0];if(!file)throw new Error('اختر ملفًا أولاً.');const form=new FormData();form.append('file',file);return form;}
$('cancel-transfer').onclick=()=>controller?.abort();
$('open-import').onclick=()=>run(async signal=>{
  const data=await api('imports/upload',{method:'POST',body:formFile('import-file'),signal});
  workbook=data;preview=null;revision='';dirty=true;
  for(const sheet of workbook.sheets){sheet.included=!!sheet.schema;configureSheet(sheet);}
  if(!workbook.sheets.some(s=>s.included))workbook.sheets[0].included=true;
  $('import-session').innerHTML='<option value="">الكل</option>'+[...new Set(data.sheets.map(s=>s.studySession).filter(Boolean))].map(s=>`<option value="${esc(s)}">${esc(s)}</option>`).join('');
  invalidate();renderSheets();message(`تمت قراءة ${data.sheets.length} ورقة. راجع الاختيارات والأعمدة والخلايا قبل التحقق.`);
});
$('download-template').onclick=()=>run(async signal=>{download(await api(`imports/template?mode=${mode()}`,{blob:true,signal}),'Import-template.xlsx');message('تم تنزيل قالب الاستيراد.');});
$('import-mode').onchange=()=>{if(workbook)for(const sheet of workbook.sheets)configureSheet(sheet);invalidate();renderSheets();};
for(const id of ['import-semester','import-updates','import-seconds'])$(id).onchange=invalidate;
$('import-session').onchange=()=>{invalidate();renderSheets();};
$('import-sheet').onchange=()=>{sourcePage=1;renderSource();};
$('sheet-selection').onchange=e=>{const sheet=workbook.sheets.find(s=>String(s.index)===e.target.dataset.sheetSelect);if(sheet){sheet.included=e.target.checked;invalidate();}};
$('sheet-schema').onchange=()=>{const sheet=selectedSheet();sheet.schema=$('sheet-schema').value;sheet.mapping=[...sheet.mappings[sheet.schema]];invalidate();renderSource();};
$('mapping-table').onchange=e=>{if(e.target.dataset.map===undefined)return;selectedSheet().mapping[Number(e.target.dataset.map)]=e.target.value;invalidate();renderSource();};
$('source-table').oninput=e=>{const input=e.target;if(input.dataset.row===undefined)return;selectedSheet().rows[Number(input.dataset.row)][Number(input.dataset.col)]=input.type==='checkbox'?(input.checked?'TRUE':'FALSE'):input.value;invalidate();};
$('source-prev').onclick=()=>{sourcePage--;renderSource();};$('source-next').onclick=()=>{sourcePage++;renderSource();};
async function validate(skipInvalid=false){await run(async signal=>{preview=null;revision='';const sheets=visibleSheets().filter(s=>s.included).map(s=>({index:s.index,schema:s.schema,mapping:s.mapping,rows:s.rows}));const data=await api('imports/validate',{method:'POST',body:{id:workbook.id,mode:mode(),semester:Number($('import-semester').value),updateExisting:$('import-updates').checked,skipInvalid,sheets},signal});renderPreview(data);});}
$('validate-import').onclick=()=>validate();$('skip-import').onclick=()=>validate(true);
$('save-import').onclick=async()=>{
  if(busy||!preview)return;
  const p=preview;
  if(!await confirmSave(`حفظ الاستيراد المعروض؟\n${p.changes.filter(c=>c.action==='Add').length} إضافة، ${p.changes.filter(c=>['Update','Lock existing'].includes(c.action)).length} تحديث.\nصفوف متخطاة: ${new Set(p.skippedRows.map(i=>i.sheet+'|'+i.row)).size}.\nتُحفظ جميع التغييرات معًا.`))return;
  await run(async signal=>{const result=await api('imports/save',{method:'POST',body:{id:workbook.id,revision,confirmed:true},signal});preview=null;workbook=null;revision='';dirty=false;message(`اكتمل الاستيراد: ${result.added} إضافة، ${result.updated} تحديث، ${result.unchanged} دون تغيير.`);},true);
};
$('download-backup').onclick=()=>run(async signal=>{download(await api('timetable-backups/download',{blob:true,signal}),'Timetable-'+new Date().toISOString().slice(0,10)+'.json');message('تم تنزيل نسخة الجدول.');});
$('restore-file').onchange=()=>{restore=null;$('restore-summary').textContent='';$('restore-issues').replaceChildren();updateActions();};
$('preview-restore').onclick=()=>run(async signal=>{restore=null;const value=await api('timetable-backups/preview',{method:'POST',body:formFile('restore-file'),signal});restore=value;const p=value.preview;$('restore-summary').textContent=`${p.canRestore?'النسخة صالحة للاستعادة':'لا يمكن استعادة النسخة'}\nتاريخ الحفظ: ${new Date(p.createdAtUtc).toLocaleString('ar-IQ')}\n${p.lectureCount} محاضرة: ${p.semesterOneCount} للفصل الأول و${p.semesterTwoCount} للفصل الثاني.\nسيُستبدل الجدول الحالي (${p.currentLectureCount} محاضرة).`;$('restore-issues').innerHTML=p.issues.map(i=>`<li>${esc(i)}</li>`).join('');message('اكتمل فحص النسخة.');});
$('restore-backup').onclick=async()=>{
  if(busy||!restore?.preview.canRestore)return;
  if(!await confirmSave(`استبدال الجدول الحالي (${restore.preview.currentLectureCount} محاضرة) بالنسخة (${restore.preview.lectureCount} محاضرة)؟\nتشمل العملية الفصلين وحالة التثبيت. ستُحفظ نسخة أمان من الجدول الحالي أولاً.`))return;
  await run(async signal=>{const result=await api('timetable-backups/restore',{method:'POST',body:{id:restore.id,confirmed:true},signal});restore=null;const link=$('safety-download');link.href=result.safetyDownload;link.hidden=false;message(`تمت استعادة ${result.restoredCount} محاضرة. يمكنك تنزيل نسخة الأمان للجدول السابق.`);},true);
};
function showTab(){const backups=location.hash==='#backups';$('import-section').hidden=backups;$('backup-section').hidden=!backups;for(const link of document.querySelectorAll('.transfer-tabs a')){if((link.hash==='#backups')===backups)link.setAttribute('aria-current','page');else link.removeAttribute('aria-current');}}
window.addEventListener('hashchange',showTab);window.addEventListener('beforeunload',e=>{if(busy||dirty){e.preventDefault();e.returnValue='';}});
document.addEventListener('session-expired',()=>{authenticated=false;controller?.abort();workbook=null;preview=null;restore=null;dirty=false;for(const id of ['source-table','mapping-table','sheet-selection','import-report','import-result','restore-issues'])$(id).replaceChildren();updateActions();});
showTab();updateActions();
try{await api('auth/me');await refreshCsrf();schemas=await api('imports/schemas');authenticated=true;updateActions();}catch(ex){$('transfer-error').textContent=ex.status===401?'سجل الدخول من الصفحة الرئيسية أولاً.':ex.message;$('transfer-error').hidden=false;}
