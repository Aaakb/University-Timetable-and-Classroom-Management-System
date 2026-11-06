import {api} from './api.js';
import {days as configuredDays} from './schema.js';
const $=id=>document.getElementById(id),esc=value=>String(value??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c])),days=Object.fromEntries(configuredDays);
let current,options=[],selected=0,busy=false,authenticated=false,controller=null,searching=false;
const status=s=>s==='Current'?'للمقارنة':s==='Optimal'?'أمثل ضمن هدف ونطاق البدائل':'صالح؛ الأمثلية غير مثبتة';
function table(host,headings,rows){$(host).innerHTML=`<table><thead><tr>${headings.map(h=>`<th scope="col">${esc(h)}</th>`).join('')}</tr></thead><tbody>${rows.map(row=>`<tr>${row.map(c=>`<td>${c}</td>`).join('')}</tr>`).join('')}</tbody></table>`;}
function showView(view){
  for(const name of ['original','detail','week']){
    const active=name===view;
    $('alt-'+name+'-view').hidden=!active;
    $('alt-show-'+name).setAttribute('aria-pressed',String(active));
  }
}
for(const name of ['original','detail','week'])$('alt-show-'+name).onclick=()=>showView(name);
function setControls(){
  const unavailable=busy||!authenticated;
  const readOnly=document.body.dataset.readOnly==='true';
  $('alt-controls').disabled=unavailable;
  $('alt-reload').disabled=unavailable;
  $('alt-generate').disabled=unavailable||!current||readOnly;
  $('alt-show-current').disabled=unavailable||!current;
  $('alt-cancel').disabled=!searching||controller.signal.aborted;
  for(const id of ['alt-unlock','alt-lock','alt-select-all'])$(id).disabled=unavailable||!current||readOnly;
  for(const box of $('alt-original').querySelectorAll('[data-lock]'))box.disabled=unavailable||readOnly;
  $('alt-save').disabled=unavailable||!current||selected===0||readOnly;
}
async function run(work,search=false){
  if(busy)return;
  busy=true;
  const operation=controller=new AbortController();
  searching=search;
  setControls();
  $('alt-error').hidden=true;
  try{await work(operation.signal);}
  catch(ex){
    if(controller===operation&&!operation.signal.aborted&&authenticated){
      $('alt-error').textContent=ex.message;
      $('alt-error').hidden=false;
      if(!current)$('alt-message').textContent='تعذر تحميل الجدول. حاول التحديث مجددًا.';
    }
  }finally{
    if(controller===operation){
      if(search&&operation.signal.aborted)$('alt-message').textContent='أُلغي البحث عن البدائل.';
      busy=false;
      searching=false;
      controller=null;
      setControls();
    }
  }
}
function render(){table('alt-comparison',['الاختيار','عقوبة التفضيل ↓','فراغات التدريسيين/دقيقة ↓','فراغات الطلبة/دقيقة ↓','أعلى عبء يومي/دقيقة ↓','محاضرات متغيرة','تدريسيون متأثرون','الحالة','المعاينة'],options.map((x,i)=>{const a=x.alternative,m=a.metrics;return [a.title,m.preferencePenalty,m.facultyGapMinutes,m.studentGapMinutes,m.peakStudentDailyMinutes,m.changedLectures,m.affectedFaculty,status(a.status)].map(esc).concat(`<button data-select="${i}" ${selected===i?'disabled':''}>${selected===i?'محدد':'اختيار'}</button>`);}));details();setControls();}
function details(){if(!options[selected])return;const rows=options[selected].alternative.lectures.filter(l=>!$('alt-faculty').value||l.faculty===$('alt-faculty').value);table('alt-detail',['التدريسي','المادة','الشعبة','المجموعة','النوع','اليوم','الفترة','القاعة','مثبتة','تغيّرت'],rows.map(l=>[l.faculty,l.subject,l.section,l.groupName,l.lectureType==='Practical'?'عملي':'نظري',days[l.day],l.period,l.room,l.locked?'نعم':'لا',l.changed?'نعم':'لا'].map(esc)));const periods=[...new Set(rows.map(l=>l.period))].sort();table('alt-week',['الفترة',...Object.values(days)],periods.map(p=>[esc(p),...Object.keys(days).map(d=>rows.filter(l=>l.day===d&&l.period===p).map(l=>`${esc(l.subject)} — ${esc(l.faculty)}<br>${esc(l.section)} ${esc(l.groupName)} / ${esc(l.room)}`).join('<hr>'))]));}
function setCurrent(value){current=value;options=[{id:null,alternative:value}];selected=0;$('alt-select-all').checked=false;$('alt-faculty').innerHTML='<option value="">كل التدريسيين</option>'+[...new Set(current.lectures.map(l=>l.faculty))].sort().map(f=>`<option value="${esc(f)}">${esc(f)}</option>`).join('');table('alt-original',['تحديد','التدريسي','المادة','الشعبة','اليوم','الفترة','مثبتة'],current.lectures.map(l=>[`<input type="checkbox" data-lock="${l.id}" aria-label="تحديد ${esc(l.subject)} ${esc(l.faculty)}" >`,...[l.faculty,l.subject,l.section,days[l.day],l.period,l.locked?'نعم':'لا'].map(esc)]));render();}
function clearCurrent(){current=null;options=[];selected=0;for(const id of ['alt-faculty','alt-comparison','alt-detail','alt-week','alt-original'])$(id).replaceChildren();$('alt-select-all').checked=false;setControls();}
async function load(signal){clearCurrent();showView('original');$('alt-message').textContent='جارٍ تحميل الجدول…';const value=await api(`timetable-alternatives?semester=${$('alt-semester').value}`,{signal});if(signal.aborted)return;setCurrent(value);$('alt-message').textContent=`${current.lectures.length} محاضرة، منها ${current.lectures.filter(l=>l.locked).length} مثبتة. قد تسمح القيود بأقل من ثلاثة بدائل.`;}
$('alt-semester').onchange=()=>run(load);$('alt-reload').onclick=()=>run(load);$('alt-faculty').onchange=details;
$('alt-generate').onclick=()=>run(async signal=>{
  $('alt-message').textContent='جارٍ البحث عن بدائل…';
  const result=await api('timetable-alternatives/preview',{method:'POST',signal,body:{semester:Number($('alt-semester').value),count:3,timeLimitSeconds:Number($('alt-seconds').value)}});
  if(signal.aborted)return;
  setCurrent(result.current);
  options.push(...result.alternatives);
  selected=options.length>1?1:0;
  render();
  showView(selected?'detail':'original');
  $('alt-message').textContent=result.message;
},true);
$('alt-cancel').onclick=()=>{controller?.abort();setControls();};
$('alt-show-current').onclick=()=>{
  selected=0;
  render();
  showView('original');
};
$('alt-comparison').onclick=e=>{const button=e.target.closest('[data-select]');if(!button||busy)return;selected=Number(button.dataset.select);render();};
$('alt-save').onclick=()=>run(async signal=>{const chosen=options[selected];if(!chosen?.id||!confirm(`اعتماد «${chosen.alternative.title}» وتغيير ${chosen.alternative.metrics.changedLectures} محاضرة؟`))return;const result=await api('timetable-alternatives/commit',{method:'POST',signal,body:{id:chosen.id,confirmed:true}});if(signal.aborted)return;await load(signal);if(!signal.aborted)$('alt-message').textContent=`حُفظ ${result.changed} تغييرًا بعد التحقق الكامل.`;});
$('alt-select-all').onchange=e=>{for(const box of $('alt-original').querySelectorAll('input:not(:disabled)'))box.checked=e.target.checked;};
function changeLocks(locked){return run(async signal=>{const ids=[...$('alt-original').querySelectorAll('[data-lock]:checked')].map(x=>Number(x.dataset.lock));if(!ids.length)throw new Error('حدد المحاضرات المطلوبة أولاً.');if(!confirm(`${locked?'تثبيت':'إلغاء تثبيت'} ${ids.length} محاضرة محددة؟ هذا يحفظ حالة التثبيت فقط دون تغيير المواعيد.`))return;await api('timetable-alternatives/locks',{method:'POST',signal,body:{semester:Number($('alt-semester').value),ids,locked,version:current.version,confirmed:true}});if(!signal.aborted)await load(signal);});}
$('alt-unlock').onclick=()=>changeLocks(false);$('alt-lock').onclick=()=>changeLocks(true);
export function clearAlternatives(){
  authenticated=false;
  controller?.abort();
  controller=null;busy=false;searching=false;
  clearCurrent();
  $('alt-semester').value='1';
  $('alt-seconds').value='30';
  $('alt-error').hidden=true;
  $('alt-error').textContent='';
  $('alt-message').textContent='';
}
export function canLeaveAlternatives(){
  if(!busy)return true;
  $('alt-message').textContent='انتظر انتهاء العملية أو ألغِ البحث قبل الانتقال.';
  return false;
}
export async function openAlternatives(){
  authenticated=true;
  await run(load);
}
