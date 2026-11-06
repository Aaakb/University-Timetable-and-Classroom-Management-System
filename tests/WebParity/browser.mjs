import assert from 'node:assert/strict';
import {pathToFileURL} from 'node:url';

const modulePath=process.env.PLAYWRIGHT_MODULE||'C:/Users/aloli/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright/index.mjs';
const {chromium}=await import(pathToFileURL(modulePath));
const browser=await chromium.launch({channel:'msedge',headless:true});
const page=await browser.newPage();
const errors=[];page.on('pageerror',error=>errors.push(error.message));
const check=(label,condition)=>{assert.ok(condition,label);console.log('PASS '+label);};
let holdAlternative=false,expire=false, failAlternative=false, failPreference=false, loginMode=false, failCsrf=false;
await page.route('**/api/**',async route=>{
  const url=new URL(route.request().url()),path=url.pathname;
  let body;
  if(path==='/api/auth/me'){
    if(loginMode)return route.fulfill({status:401,contentType:'application/json',body:JSON.stringify({title:'يرجى تسجيل الدخول'})});
    body={fullName:'اختبار'};
  }
  else if(path==='/api/auth/csrf'){
    if(failCsrf)return route.fulfill({status:500,contentType:'application/json',body:'{}'});
    body={token:'test'};
  }
  else if(path==='/api/auth/login')body={fullName:'اختبار'};
  else if(path==='/api/imports/schemas')body=[{name:'NamedAssignments',label:'أسماء',required:[],columns:[{name:'FacultyName',label:'التدريسي'},{name:'SubjectName',label:'المادة'}]}];
  else if(path==='/api/imports/upload')body={id:'test',sheets:[{index:0,name:'Subjects',schema:'Subjects',columns:['المادة'],rows:[['رياضيات']],sourceRows:[2],mappings:{NamedAssignments:['SubjectName']}}]};
  else if(path==='/api/faculty-preferences/lookups')body={data:{faculty:[{id:1,name:'تدريسي'}],days:['Sunday','Monday','Tuesday','Wednesday','Thursday'],periods:[{id:1,name:'08:30 – 10:00'}]},configured:[]};
  else if(path==='/api/faculty-preferences/days'){
    const setting=route.request().postDataJSON().settings;
    check('Day preferences submit all five day settings',setting.dayPreferences.length===5);
    body={profile:{revision:1},changedLectures:1,status:'Optimal',changes:[{faculty:'تدريسي',subject:'رياضيات',section:'A',oldDay:'Sunday',oldPeriod:'08:30',oldRoom:'1',newDay:'Monday',newPeriod:'10:00',newRoom:'2'}]};
  }
  else if(/^\/api\/faculty-preferences\/1\/[12]$/.test(path)){
    if(expire)return route.fulfill({status:401,contentType:'application/json',body:JSON.stringify({title:'انتهت الجلسة'})});
    if(failPreference)return route.fulfill({status:500,contentType:'application/json',body:JSON.stringify({title:'تعذر تحميل التفضيلات'})});
    body={profile:null,current:[]};
  }
  else if(path==='/api/timetable-alternatives'){
    if(expire)return route.fulfill({status:401,contentType:'application/json',body:JSON.stringify({title:'انتهت الجلسة'})});
    if(failAlternative)return route.fulfill({status:500,contentType:'application/json',body:JSON.stringify({title:'تعذر تحميل الجدول'})});
    body={title:'الجدول',status:'Current',version:'test',metrics:{preferencePenalty:0,facultyGapMinutes:0,studentGapMinutes:0,peakStudentDailyMinutes:0,changedLectures:0,affectedFaculty:0},lectures:[{id:1,subject:'بيانات اختبار',faculty:'تدريسي',section:'A',day:'Sunday',period:'08:30 – 10:00',room:'1',locked:false}]};
  }else if(path==='/api/timetable-alternatives/preview'){
    if(holdAlternative)await new Promise(resolve=>setTimeout(resolve,1000));
    const metrics={preferencePenalty:0,facultyGapMinutes:0,studentGapMinutes:0,peakStudentDailyMinutes:0,changedLectures:0,affectedFaculty:0};
    const current={title:'الجدول',status:'Current',version:'test',metrics,lectures:[]};
    body={current,alternatives:[{id:'variant',alternative:{...current,title:'البديل الأول',status:'Optimal'}}],message:'تم البحث.'};
    return route.fulfill({status:200,contentType:'application/json',body:JSON.stringify(body)}).catch(()=>{});
  }else return route.fulfill({status:404,contentType:'application/json',body:'{}'});
  return route.fulfill({status:200,contentType:'application/json',body:JSON.stringify(body)});
});
try{
  await page.goto('http://localhost:5193/data-transfer.html');
  await page.waitForFunction(()=>!document.getElementById('import-controls').disabled);
  await page.locator('#import-file').setInputFiles({name:'subjects.xlsx',mimeType:'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',buffer:Buffer.from('test')});
  await page.locator('#open-import').click();
  await page.locator('#source-table input').first().waitFor({timeout:5000});
  check('Import opens a sheet whose detected schema is not offered by the current importer',await page.locator('#source-table input').count()===1&&!await page.locator('#transfer-error').isVisible());
  page.once('dialog',dialog=>dialog.dismiss());
  await page.locator('#import-file').setInputFiles({name:'another.xlsx',mimeType:'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',buffer:Buffer.from('different')});
  check('Cancelling a file change keeps the workbook under review',await page.locator('#source-table input').count()===1&&!await page.locator('#validate-import').isDisabled());
  page.once('dialog',dialog=>dialog.accept());
  await page.locator('#import-file').setInputFiles({name:'another.xlsx',mimeType:'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',buffer:Buffer.from('different')});
  check('Changing the file invalidates the previous workbook',await page.locator('#source-table input').count()===0&&await page.locator('#validate-import').isDisabled());
  await page.goto('http://localhost:5193/preferences-advanced.html');
  await page.waitForFunction(()=>!document.getElementById('pref-controls').disabled);
  failPreference=true;await page.locator('#pref-semester').selectOption('2');await page.locator('#pref-error').waitFor({state:'visible'});
  check('Advanced preferences cannot save stale data after a failed load',await page.locator('#pref-save').isDisabled()&&await page.locator('#pref-matrix select').count()===0);
  failPreference=false;await page.locator('#pref-semester').selectOption('1');await page.locator('#pref-matrix select').first().waitFor();
  expire=true;await page.locator('#pref-semester').selectOption('2');
  await page.locator('#pref-error').waitFor({state:'visible'});
  check('Advanced preferences stay disabled after the session expires',await page.locator('#pref-semester').isDisabled());
  check('Advanced preferences clear private data after expiry',await page.locator('#pref-faculty option').count()===0);
  expire=false;await page.goto('http://localhost:5193/preferences.html');
  await page.waitForFunction(()=>!document.getElementById('day-controls').disabled);
  failPreference=true;await page.locator('#day-semester').selectOption('2');await page.locator('#day-error').waitFor({state:'visible'});
  check('Day preferences cannot save stale data after a failed load',await page.locator('#day-save').isDisabled()&&await page.locator('#day-cards select').count()===0);
  failPreference=false;await page.locator('#day-semester').selectOption('1');await page.locator('#day-cards select').first().waitFor();
  await page.locator('#day-enabled').check();
  await page.locator('#day-save').click();
  await page.locator('#day-confirm button[value="yes"]').click();
  await page.locator('#day-changes tbody tr').waitFor();
  check('Successful optimization opens the changes view',await page.locator('#day-show-changes').getAttribute('aria-pressed')==='true'&&!await page.locator('#day-current').isVisible());
  expire=true;await page.locator('#day-reload').click();
  await page.locator('#auth-error').waitFor({state:'visible'});
  check('Day preferences return to login and clear private data after expiry',await page.locator('#auth-error').innerText().then(text=>text.includes('سجل الدخول'))&&await page.locator('#day-reload').isDisabled()&&await page.locator('#day-faculty option').count()===0);
  expire=false;await page.goto('http://localhost:5193/alternatives.html');
  await page.locator('#alt-original tbody tr').first().waitFor();
  failAlternative=true;await page.locator('#alt-semester').selectOption('2');await page.locator('#alt-error').waitFor({state:'visible'});
  check('Failed semester load clears the previous timetable and disables lock actions',await page.locator('#alt-original tbody tr').count()===0&&await page.locator('#alt-lock').isDisabled());
  failAlternative=false;await page.locator('#alt-reload').click();await page.locator('#alt-original tbody tr').first().waitFor();
  holdAlternative=true;await page.locator('#alt-generate').click();
  await page.locator('#alt-cancel').click();
  await page.waitForFunction(()=>document.getElementById('alt-message').textContent.includes('أُلغي'));
  check('Cancelling search preserves the current timetable',await page.locator('#alt-original tbody tr').count()===1);
  holdAlternative=false;await page.locator('#alt-generate').click();
  await page.locator('#alt-detail-view').waitFor();
  check('Generating alternatives selects the first result and opens its details',await page.locator('#alt-save').isEnabled()&&await page.locator('#alt-original-view').isHidden());
  expire=true;await page.locator('#alt-reload').click();
  await page.locator('#auth-error').waitFor({state:'visible'});
  check('Alternative controls stay disabled after the session expires',await page.locator('#alt-semester').isDisabled()&&await page.locator('#alt-lock').isDisabled());
  check('Alternatives clear private timetable data after expiry',await page.locator('#alt-original tbody tr').count()===0);
  loginMode=true;expire=false;await page.goto('http://localhost:5193/');
  await page.locator('#auth-submit').waitFor();
  await page.locator('#auth-form [name=userName]').fill('test');await page.locator('#auth-form [name=password]').fill('test');
  failCsrf=true;await page.locator('#auth-submit').click();
  await page.waitForTimeout(200);
  check('Login remains visible if secure session initialization fails',await page.locator('#auth').isVisible()&&await page.locator('#auth-error').isVisible());
  check('No uncaught browser errors',errors.length===0);
}finally{await browser.close();}
