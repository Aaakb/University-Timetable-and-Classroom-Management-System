import {api} from './api.js';
import {days} from './schema.js';
const $=id=>document.getElementById(id),names=Object.fromEntries(days),esc=value=>String(value??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
let facultyId=0,semester=1,revision=0,busy=false,dirty=false,authenticated=false,ready=false,controller=null;
function table(host,headings,rows){$(host).innerHTML=`<table><thead><tr>${headings.map(h=>`<th scope="col">${esc(h)}</th>`).join('')}</tr></thead><tbody>${rows.map(row=>`<tr>${row.map(c=>`<td>${esc(c)}</td>`).join('')}</tr>`).join('')}</tbody></table>`;}
function showTable(view){
  for(const name of ['current','changes']){
    const active=name===view;
    $('day-'+name).hidden=!active;
    $('day-show-'+name).setAttribute('aria-pressed',String(active));
  }
}
for(const name of ['current','changes'])$('day-show-'+name).onclick=()=>showTable(name);
function actions(){$('day-controls').disabled=busy||!authenticated;const readOnly=document.body.dataset.readOnly==='true';$('day-enabled').disabled=busy||!authenticated||!ready||readOnly;$('day-cards').disabled=busy||!ready||readOnly||!$('day-enabled').checked;$('day-save').disabled=busy||!authenticated||!ready||!facultyId;$('day-reload').disabled=busy||!authenticated||!facultyId;$('day-save').textContent=$('day-enabled').checked?'حفظ التفضيلات وتحديث الجدول':'حفظ بدون تفضيلات';}
async function run(work){if(busy)return;busy=true;controller=new AbortController();$('day-cancel').hidden=false;$('day-error').hidden=true;actions();try{await work();}catch(ex){$('day-error').textContent=ex.status===401?'انتهت الجلسة. سجل الدخول من الصفحة الرئيسية أولاً.':controller.signal.aborted?'توقف انتظار العملية. حدّث البيانات للتحقق من نتيجة الحفظ قبل إعادة المحاولة.':ex.message;$('day-error').hidden=false;}finally{busy=false;controller=null;$('day-cancel').hidden=true;actions();}}
function paint(){for(const select of $('day-cards').querySelectorAll('select'))select.className=`preference-${select.value}`;}
function edited(){dirty=true;paint();actions();$('day-status').textContent='تعديلات غير محفوظة. الحفظ قد يغيّر جدول الفصل.';}
function discard(){return !dirty||confirm('توجد تعديلات غير محفوظة. تجاهلها والمتابعة؟');}
async function load(){
  showTable('current');
  ready=false;revision=0;dirty=false;$('day-enabled').checked=false;
  for(const id of ['day-current','day-changes','day-cards'])$(id).replaceChildren();
  $('day-profile-note').textContent='';$('day-status').textContent='جارٍ تحميل التفضيلات…';
  const lookups=await api(`faculty-preferences/lookups?semester=${semester}`,{signal:controller?.signal}),configured=new Set(lookups.configured);
  $('day-faculty').innerHTML=lookups.data.faculty.map(f=>`<option value="${f.id}">${esc(f.name)} — ${configured.has(f.id)?'لديه تفضيلات':'بدون تفضيلات'}</option>`).join('');
  if(!lookups.data.faculty.some(f=>f.id===facultyId))facultyId=lookups.data.faculty[0]?.id??0;
  $('day-faculty').value=String(facultyId);$('day-changes').replaceChildren();
  if(!facultyId){$('day-status').textContent='أضف التدريسيين أولاً.';return;}
  const data=await api(`faculty-preferences/${facultyId}/${semester}`,{signal:controller?.signal}),settings=data.profile?.settings;
  revision=data.profile?.revision??0;$('day-enabled').checked=!!data.profile;
  $('day-cards').innerHTML=days.map(([day,name])=>{
    const value=settings?.useDayPreferences?settings.dayPreferences.find(p=>p.day===day)?.level??1:!settings?1:
      settings.unavailableDays?.includes(day)||(settings.mode===0&&!settings.selectedDays.includes(day))?2:
      settings.usePreferenceMatrix&&settings.periodPreferences.some(p=>p.day===day&&p.level===0)?0:1;
    return `<label>${name}<select data-day="${day}" aria-label="تفضيل ${name}">${['مفضّل','مسموح','غير متاح'].map((text,i)=>`<option value="${i}" ${value===i?'selected':''}>${text}</option>`).join('')}</select></label>`;
  }).join('');paint();
  $('day-profile-note').textContent=settings&&!settings.useDayPreferences?'توجد إعدادات قديمة للفترات. حفظ تفضيلات الأيام سيستبدلها؛ راجع الأيام المعروضة أولاً.':'التفضيلات اختيارية. يحسّن الحفظ جدول الفصل ويحافظ على المحاضرات المثبتة.';
  table('day-current',['المادة','الشعبة','نوع المحاضرة','المجموعة','اليوم','الوقت','القاعة'],data.current.map(r=>[r.subject,r.section,r.lectureType==='Practical'?'عملي':'نظري',r.groupName||'—',names[r.day],r.period,r.room]));
  ready=true;$('day-status').textContent=settings?'تم تحميل التفضيلات المحفوظة.':'بدون تفضيلات محفوظة. فعّل تحديد التفضيلات لإدخال الأيام.';
}
$('day-faculty').onchange=()=>{if(!discard()){$('day-faculty').value=String(facultyId);return;}facultyId=Number($('day-faculty').value);run(load);};
$('day-semester').onchange=()=>{if(!discard()){$('day-semester').value=String(semester);return;}semester=Number($('day-semester').value);run(load);};
$('day-cancel').onclick=()=>controller?.abort();
$('day-enabled').onchange=edited;$('day-cards').onchange=edited;$('day-reload').onclick=()=>{if(discard())run(load);};
function confirmation(text){const dialog=$('day-confirm');$('day-confirm-text').textContent=text;dialog.returnValue='cancel';dialog.showModal();return new Promise(resolve=>dialog.addEventListener('close',()=>resolve(dialog.returnValue==='yes'),{once:true}));}
$('day-save').onclick=async()=>{
  if(busy||!facultyId||document.body.dataset.readOnly==='true')return;
  const enabled=$('day-enabled').checked;
  if(!await confirmation(enabled?'حفظ تفضيلات الأيام وتحديث جدول الفصل؟ قد تتحرك محاضرات تدريسيين آخرين، مع الحفاظ على المحاضرات المثبتة.':'حفظ هذا التدريسي بدون تفضيلات؟ ستتاح أيامه وفق قواعد الجدولة العامة، دون تغيير مواعيد محاضراته الحالية.'))return;
  await run(async()=>{
    if(!enabled){await api(`faculty-preferences/${facultyId}/${semester}`,{method:'DELETE',signal:controller?.signal,body:{expectedRevision:revision,confirmed:true}});revision=0;dirty=false;await load();$('day-status').textContent='حُفظ بدون تفضيلات. أيام التدريسي متاحة وفق قواعد الجدولة العامة.';return;}
    const result=await api('faculty-preferences/days',{method:'PUT',signal:controller?.signal,body:{expectedRevision:revision,confirmed:true,settings:{facultyId,semester,mode:0,selectedDays:[],timeLimitSeconds:Number($('day-seconds').value),useDayPreferences:true,dayPreferences:[...$('day-cards').querySelectorAll('[data-day]')].map(c=>({day:c.dataset.day,level:Number(c.value)}))}}});
    revision=result.profile.revision;dirty=false;await load();
    table('day-changes',['التدريسي','المادة','الشعبة','اليوم السابق','الوقت السابق','اليوم الجديد','الوقت الجديد','القاعة السابقة','القاعة الجديدة'],result.changes.map(c=>[c.faculty,c.subject,c.section,names[c.oldDay],c.oldPeriod,names[c.newDay],c.newPeriod,c.oldRoom,c.newRoom]));
    if(result.changedLectures>0)showTable('changes');
    $('day-status').textContent=result.status==='NoTimetable'?'حُفظت التفضيلات للتوليد القادم؛ لا توجد محاضرات لهذا الفصل حاليًا.':`حُفظت التفضيلات وحُدّث الجدول: ${result.changedLectures} محاضرة تغيّرت. ${result.status==='Optimal'?'اكتمل التحسين ضمن النموذج.':'وُجد حل صالح؛ لم تثبت الأمثلية.'}`;
  });
};
window.addEventListener('beforeunload',e=>{if(!$('preferences-workspace').hidden&&(dirty||busy)){e.preventDefault();e.returnValue='';}});
export function canLeavePreferences(){
  if(busy){
    $('day-status').textContent='انتظر انتهاء العملية أو ألغِ البحث قبل الانتقال.';
    return false;
  }
  if(!discard())return false;
  dirty=false;
  return true;
}
$('day-advanced').onclick=event=>{if(!canLeavePreferences())event.preventDefault();};
export function clearPreferences(){
  authenticated=false;ready=false;controller?.abort();dirty=false;facultyId=0;semester=1;
  if($('day-confirm').open)$('day-confirm').close('cancel');
  $('day-semester').value='1';$('day-seconds').value='20';$('day-enabled').checked=false;
  for(const id of ['day-current','day-changes','day-cards','day-faculty','day-profile-note','day-status','day-error'])$(id).replaceChildren();
  $('day-error').hidden=true;
  actions();
}
export async function openPreferences(){
  authenticated=true;
  await run(load);
}
