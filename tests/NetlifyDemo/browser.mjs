import path from 'node:path';
import fs from 'node:fs/promises';
import {pathToFileURL} from 'node:url';
import assert from 'node:assert/strict';
import {createMockData} from '../../demo/netlify/mock-data.js';
const modulePath=process.env.PLAYWRIGHT_MODULE||'C:/Users/aloli/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright/index.mjs';
const {chromium}=await import(pathToFileURL(modulePath));
const browser=await chromium.launch({channel:'msedge',headless:true});
const context=await browser.newContext({viewport:{width:1366,height:960}}),page=await context.newPage(),errors=[],checks=[];
page.on('pageerror',e=>errors.push(e.message));page.on('dialog',dialog=>dialog.accept());
const check=(name,value=true)=>{assert.ok(value,name);checks.push(name);console.log('PASS '+name);};
const output=path.resolve('.tmp/NetlifyDemo');await fs.mkdir(output,{recursive:true});
try{
  await page.goto('http://localhost:5193');await page.locator('.stats').waitFor();
  check('Mock data visible immediately without a server login',await page.locator('.stat strong').allTextContents().then(values=>values.join(',')==='6,24,8,8'));
  check('Demo notice is clear',await page.locator('.demo-banner').innerText().then(t=>t.includes('افتراضية')));
  await page.screenshot({path:path.join(output,'dashboard.png'),fullPage:true});
  for(const resource of ['classrooms','branches','studyyears','sections','subjects','facultymembers','assignments','timeslots','schedules']){
    await page.locator(`nav a[href="#${resource}"]`).click();await page.locator('#loading').waitFor({state:'hidden'});
    check(resource+' displays related records',!await page.locator('#page-error').isVisible()&&await page.locator('tbody tr').count()>0);
    await page.locator('#add').click();await page.locator('#editor').waitFor();await page.locator('#cancel-editor').click();
    if(resource==='subjects'||resource==='assignments'){
      await page.locator('button[data-delete]').first().click();
      check(resource+' deletion explains removal of linked lectures',await page.locator('#confirm-message').innerText().then(t=>t.includes('محاضرات')&&t.includes('المثبتة')));
      await page.locator('#confirmation button[value=cancel]').click();
    }
    if(resource==='subjects'){
      await page.locator('button[data-edit="1"]').click();
      await page.locator('#editor [name=mergePracticalGroups]').selectOption('true');
      await page.locator('#save').click();await page.locator('#editor').waitFor({state:'hidden'});await page.locator('#loading').waitFor({state:'hidden'});
      await page.locator('button[data-edit="1"]').click();
      check('Merged practical setting persists when a subject is edited',await page.locator('#editor [name=mergePracticalGroups]').inputValue()==='true');
      await page.locator('#cancel-editor').click();
    }
    if(['sections','subjects','assignments'].includes(resource)){
      await page.locator('[data-filter=studyYearID]').selectOption('3');
      check(resource+' filters branches by year',await page.locator('[data-filter=branchID] option').evaluateAll(options=>options.map(o=>o.value).filter(Boolean).join(',')==='1,2'));
      await page.locator('[data-filter=branchID]').selectOption('2');
      await page.locator('[data-filter=studyYearID]').selectOption('1');
      check(resource+' clears a branch incompatible with the new year',await page.locator('[data-filter=branchID]').inputValue()==='');
    }
  }
  check('Schedule list shows the desktop columns',await page.locator('#table-host th').allTextContents().then(h=>['اليوم','الوقت','المرحلة','الفرع','الفصل الدراسي','المعرف','التثبيت'].every(x=>h.includes(x))));
  const filterOptions=async name=>page.locator(`[data-filter=${name}] option`).evaluateAll(options=>options.map(option=>option.value).filter(Boolean));
  await page.locator('[data-filter=studyYearID]').selectOption('3');
  check('Year filter narrows branches to that year',JSON.stringify(await filterOptions('branchID'))===JSON.stringify(['1','2']));
  check('Year filter narrows sections to that year',JSON.stringify(await filterOptions('sectionID'))===JSON.stringify(['5','6']));
  check('Year filter offers only applicable groups',JSON.stringify(await filterOptions('groupName'))===JSON.stringify(['A1','A2']));
  await page.locator('[data-filter=branchID]').selectOption('2');
  check('Branch filter narrows sections',JSON.stringify(await filterOptions('sectionID'))===JSON.stringify(['6']));
  await page.locator('[data-filter=sectionID]').selectOption('6');
  await page.locator('[data-filter=groupName]').selectOption('A1');
  const relevant=createMockData().schedules.filter(row=>row.studyYearID===3&&row.branchID===2&&row.sectionID===6&&(row.lectureType==='Theory'||row.groupName==='A1'));
  const teachers=[...new Set(relevant.map(row=>String(row.facultyMemberID)))].sort();
  check('Teachers follow the selected academic filters',JSON.stringify((await filterOptions('facultyMemberID')).sort())===JSON.stringify(teachers));
  check('Group filter keeps theory and its matching practical group',await page.locator('#result-count').innerText().then(text=>text.startsWith(String(relevant.length)+' ')));
  await page.locator('[data-filter=facultyMemberID]').selectOption(teachers[0]);
  const rooms=[...new Set(relevant.filter(row=>String(row.facultyMemberID)===teachers[0]).map(row=>String(row.classroomID)))].sort();
  check('Rooms follow the selected teacher',JSON.stringify((await filterOptions('classroomID')).sort())===JSON.stringify(rooms));
  await page.locator('[data-filter=classroomID]').selectOption(rooms[0]);
  const roomRows=relevant.filter(row=>String(row.facultyMemberID)===teachers[0]&&String(row.classroomID)===rooms[0]);
  const availableDays=['Sunday','Monday','Tuesday','Wednesday','Thursday'].filter(day=>roomRows.some(row=>row.dayOfWeek===day));
  check('Days follow the selected teacher and room in weekday order',JSON.stringify(await filterOptions('dayOfWeek'))===JSON.stringify(availableDays));
  await page.locator('[data-filter=dayOfWeek]').selectOption(availableDays[0]);
  check('Lecture types follow the selected day',JSON.stringify(await filterOptions('lectureType'))===JSON.stringify(['Theory','Practical'].filter(type=>roomRows.some(row=>row.dayOfWeek===availableDays[0]&&row.lectureType===type))));
  await page.locator('[data-filter=dayOfWeek]').selectOption('');
  await page.locator('[data-filter=classroomID]').selectOption('');
  await page.locator('[data-filter=facultyMemberID]').selectOption('');
  await page.locator('[data-filter=semesterNumber]').selectOption('2');
  check('Changing semester preserves compatible branch, section and group',await page.locator('[data-filter=branchID]').inputValue()==='2'&&await page.locator('[data-filter=sectionID]').inputValue()==='6'&&await page.locator('[data-filter=groupName]').inputValue()==='A1');
  await page.locator('[data-filter=semesterNumber]').selectOption('');
  await page.locator('[data-filter=studyYearID]').selectOption('4');
  check('Changing year clears incompatible branch, section and group',await page.locator('[data-filter=branchID]').inputValue()===''&&await page.locator('[data-filter=sectionID]').inputValue()===''&&await page.locator('[data-filter=groupName]').inputValue()==='');
  check('Fourth year has no split practical groups',(await filterOptions('groupName')).length===0);
  await page.locator('[data-filter=studyYearID]').selectOption('');
  await page.locator('#add').click();
  await page.locator('#editor [name=subjectID]').selectOption('1');
  await page.locator('#editor [name=sectionID]').selectOption('1');
  await page.locator('#editor [name=lectureType]').selectOption('Practical');
  check('Merged practical lesson uses the whole section',await page.locator('#editor [name=groupName]').isDisabled());
  await page.locator('#cancel-editor').click();await page.locator('#confirmation button[value=yes]').click();
  check('Real generation is clearly unavailable in demo',!await page.locator('#generate').isEnabled());
  await page.locator('#timetable-view').click();check('Weekly timetable renders mock lectures',await page.locator('.lesson').count()===80);
  check('All weekdays appear in chronological right-to-left order',await page.locator('.timetable thead th').evaluateAll(cells=>cells.slice(1).map(cell=>cell.textContent.trim()).join(',')==='الأحد,الإثنين,الثلاثاء,الأربعاء,الخميس'&&cells.slice(1).every((cell,index,items)=>index===0||items[index-1].getBoundingClientRect().left>cell.getBoundingClientRect().left)));
  const timeRows=page.locator('.timetable tbody tr');
  check('Timetable separates the two semesters into their own time rows',await timeRows.count()===8);
  const periodLabels=await timeRows.locator('th').allTextContents();
  check('Semester rows are ordered and labeled',periodLabels.slice(0,4).every(x=>x.includes('الفصل 1'))&&periodLabels.slice(4).every(x=>x.includes('الفصل 2')));
  check('Each lesson is shown in its own semester row',await page.locator('.timetable tbody tr').evaluateAll(rows=>rows.every(row=>[...row.querySelectorAll('.lesson')].every(card=>card.dataset.semester===row.dataset.semester))));
  await page.locator('[data-filter=semesterNumber]').selectOption('2');
  check('Semester filter keeps only the selected semester',await page.locator('.lesson').count()===40&&await timeRows.count()===4);
  check('Single-semester timetable does not repeat the semester label',!(await timeRows.first().locator('th').innerText()).includes('الفصل'));
  await page.locator('[data-filter=semesterNumber]').selectOption('');
  await page.screenshot({path:path.join(output,'timetable.png'),fullPage:true});
  for(const width of [1366,768,390,360,320]){await page.setViewportSize({width,height:900});check('No overflow at '+width,await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1));}
  await page.goto('http://localhost:5193/preferences.html');await page.waitForFunction(()=>!document.getElementById('day-controls').disabled);
  check('Preference page displays mock faculty',await page.locator('#day-faculty option').count()===8);
  check('Optimization stays disabled',!await page.locator('#day-save').isEnabled());
  await page.goto('http://localhost:5193/alternatives.html');await page.locator('#alt-original tbody tr').first().waitFor();
  check('Alternative page displays current mock semester',await page.locator('#alt-original tbody tr').count()===40);
  await page.goto('http://localhost:5193');await page.locator('.stats').waitFor();
  await page.getByRole('button',{name:'إعادة البيانات التجريبية'}).click();await page.locator('.stats').waitFor();
  check('Reset preserves seeded data',await page.locator('.stat strong').allTextContents().then(v=>v[1]==='24'));
  check('No JavaScript errors',errors.length===0);
}finally{await fs.writeFile(path.join(output,'results.json'),JSON.stringify({checks,errors},null,2));await browser.close();}
