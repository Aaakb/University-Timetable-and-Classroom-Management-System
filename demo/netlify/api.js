import {createMockData} from './mock-data.js';
const STORAGE='university-timetable-demo-v1',clone=value=>JSON.parse(JSON.stringify(value));
let memory=null;
export class ApiError extends Error{constructor(message,status=400){super(message);this.status=status;}}
function read(){if(memory)return memory;try{const saved=localStorage.getItem(STORAGE);if(saved){const data=JSON.parse(saved);if(data.format==='UniversityTimetable.Demo.v1'&&Array.isArray(data.records?.subjects)&&Array.isArray(data.records?.schedules))return memory=data.records;}}catch{}memory=createMockData();write();return memory;}
function write(){try{localStorage.setItem(STORAGE,JSON.stringify({format:'UniversityTimetable.Demo.v1',records:memory}));}catch{/* In-memory viewing still works when browser storage is unavailable. */}}
export function resetDemo(){memory=createMockData();write();}
export async function refreshCsrf(){}
const keyField={branches:'branchID',studyyears:'studyYearID',facultymembers:'facultyMemberID',classrooms:'classroomID',sections:'sectionID',subjects:'subjectID',timeslots:'timeSlotID',schedules:'scheduleID'};
const key=(resource,row)=>resource==='assignments'?`${row.facultyMemberID}-${row.subjectID}`:String(row[keyField[resource]]);
function version(row){let hash=2166136261;for(const c of JSON.stringify(row)){hash^=c.charCodeAt(0);hash=Math.imul(hash,16777619);}return 'demo-'+(hash>>>0).toString(16);}
const envelope=row=>({data:clone(row),version:version(row)});
function list(db,resource){return resource==='subjects'?db.subjects.filter(s=>!s.isDeleted):resource==='assignments'?db.assignments.map(a=>{const s=db.subjects.find(s=>s.subjectID===a.subjectID);return {...a,studyYearID:s?.studyYearID,branchID:s?.branchID,semesterNumber:s?.semesterNumber,subjectName:s?.subjectName,isSubjectDeleted:!!s?.isDeleted};}):db[resource];}
function details(db){return db.schedules.map(s=>{const subject=db.subjects.find(x=>x.subjectID===s.subjectID),faculty=db.facultymembers.find(x=>x.facultyMemberID===s.facultyMemberID),room=db.classrooms.find(x=>x.classroomID===s.classroomID),slot=db.timeslots.find(x=>x.timeSlotID===s.timeSlotID),section=db.sections.find(x=>x.sectionID===s.sectionID);return {...s,subjectName:subject?.subjectName,facultyMemberName:faculty?.fullName,classroomNumber:room?.classroomNumber,sectionName:section?.sectionName,yearName:db.studyyears.find(x=>x.studyYearID===s.studyYearID)?.yearName,branchName:db.branches.find(x=>x.branchID===s.branchID)?.branchName??'عام',startTime:slot?.startTime,endTime:slot?.endTime};});}
function matching(row,query){return [...query.entries()].every(([name,value])=>!value||String(row[({semester:'semesterNumber',studyYearId:'studyYearID',branchId:'branchID',sectionId:'sectionID',facultyMemberId:'facultyMemberID',classroomId:'classroomID'})[name]||name]??'')===value);}
function rules(db){return {years:db.studyyears.map(y=>({id:y.studyYearID,usesBranches:y.studyYearID>2,usesGeneralSections:y.studyYearID<=2,sectionNames:['A','B']})),sections:db.sections.map(s=>({id:s.sectionID,allowsWholeSectionPractical:s.studyYearID===4,groups:s.studyYearID===4?[]:[s.sectionName+'1',s.sectionName+'2']})),subjectSections:db.subjects.filter(s=>!s.isDeleted).map(s=>({id:s.subjectID,sectionIds:db.sections.filter(x=>x.studyYearID===s.studyYearID&&x.branchID===s.branchID).map(x=>x.sectionID)}))};}
function currentLectures(db,query){return details(db).filter(s=>matching(s,query)).map(s=>({id:s.scheduleID,subject:s.subjectName,faculty:s.facultyMemberName,section:s.sectionName,lectureType:s.lectureType,groupName:s.groupName,day:s.dayOfWeek,period:s.startTime.slice(0,5)+' – '+s.endTime.slice(0,5),room:s.classroomNumber,locked:s.isLocked,changed:false}));}
const unavailable=()=>{throw new ApiError('هذه عملية تحتاج خادم النظام الحقيقي. النسخة الحالية للعرض ببيانات تجريبية فقط.',501);};
export async function api(path,options={}){
  if(options.signal?.aborted)throw new DOMException('Aborted','AbortError');
  const method=(options.method||'GET').toUpperCase(),[pathname,search='']=path.split('?'),parts=pathname.split('/').filter(Boolean),[resource,id,action]=parts,query=new URLSearchParams(search),db=read();
  if(resource==='auth'){if(id==='logout')return null;return {id:0,userName:'demo',fullName:'زائر النسخة التجريبية'};}
  if(pathname==='overview')return {classrooms:db.classrooms.length,subjects:list(db,'subjects').length,facultyMembers:db.facultymembers.length,sections:db.sections.length,schedules:db.schedules.length,assignments:db.assignments.length,unassignedSubjects:db.subjects.filter(s=>!s.isDeleted&&!db.assignments.some(a=>a.subjectID===s.subjectID)).length,updatedAt:new Date().toISOString()};
  if(pathname==='overview/rules')return rules(db);
  if(pathname==='schedules/details')return {items:details(db).filter(s=>matching(s,query))};
  if(pathname==='faculty-preferences/lookups')return {data:{faculty:db.facultymembers.map(f=>({id:f.facultyMemberID,name:f.fullName})),periods:db.timeslots.map(s=>({id:s.timeSlotID,name:s.startTime.slice(0,5)+' – '+s.endTime.slice(0,5)})),days:['Sunday','Monday','Tuesday','Wednesday','Thursday']},configured:[]};
  if(resource==='faculty-preferences'&&method==='GET'&&parts.length===3){const filter=new URLSearchParams({facultyMemberId:id,semester:action});return {profile:null,current:currentLectures(db,filter)};}
  if(pathname==='timetable-alternatives'&&method==='GET')return {title:'الجدول التجريبي الحالي',status:'Current',version:version(db.schedules),lectures:currentLectures(db,query),metrics:{preferencePenalty:0,facultyGapMinutes:0,studentGapMinutes:0,peakStudentDailyMinutes:0,changedLectures:0,affectedFaculty:0}};
  if(pathname==='timetable-alternatives/locks'&&method==='POST'){
    const body=options.body;if(!body.confirmed||body.version!==version(db.schedules))throw new ApiError('حدّث الجدول قبل تعديل التثبيت.',409);
    const rows=db.schedules.filter(s=>s.semesterNumber===body.semester&&body.ids.includes(s.scheduleID));for(const row of rows)row.isLocked=!!body.locked;write();return {changed:rows.length};
  }
  if(resource==='imports'&&id==='schemas')return [];
  if(resource==='imports'||resource==='timetable-backups'||resource==='faculty-preferences'||resource==='timetable-alternatives'||options.blob||options.body instanceof FormData)unavailable();
  if(resource==='schedules'&&id==='generation-state')unavailable();
  if(!Object.hasOwn(keyField,resource)&&resource!=='assignments')throw new ApiError('المسار غير متاح في نسخة العرض.',404);
  const rows=list(db,resource);
  if(method==='GET'&&!id){const page=Math.max(1,Number(query.get('page'))||1),pageSize=Math.max(1,Math.min(200,Number(query.get('pageSize'))||100));return {items:rows.slice((page-1)*pageSize,page*pageSize).map(envelope),total:rows.length,page,pageSize};}
  const row=rows.find(r=>key(resource,r)===String(id));
  if(method==='GET'){if(!row)throw new ApiError('السجل غير موجود.',404);return envelope(row);}
  if(method==='PUT'||method==='DELETE'){
    if(!row)throw new ApiError('السجل غير موجود.',404);
    const expected=(options.headers?.['If-Match']||'').replaceAll('"','');if(expected!==version(row))throw new ApiError('تغيرت البيانات التجريبية. حدّث الصفحة وأعد المحاولة.',412);
  }
  if(resource==='schedules'&&action==='lock'&&method==='PUT'){row.isLocked=!!options.body.locked;write();return envelope(row);}
  if(method==='DELETE'){
    const referenced=Object.keys(keyField).filter(r=>r!==resource).some(r=>db[r].some(item=>keyField[resource]&&item[keyField[resource]]===row[keyField[resource]]));
    if(resource==='subjects')db.subjects.find(s=>s.subjectID===row.subjectID).isDeleted=true;
    else {if(referenced)throw new ApiError('هذا السجل مرتبط ببيانات أخرى في المثال. احذف الارتباط أولاً.',409);db[resource]=db[resource].filter(r=>key(resource,r)!==String(id));}
    write();return null;
  }
  if(method!=='POST'&&method!=='PUT')unavailable();
  const value=clone(options.body||{});
  if(resource==='schedules'){
    const subject=db.subjects.find(s=>s.subjectID===value.subjectID&&!s.isDeleted),section=db.sections.find(s=>s.sectionID===value.sectionID);
    if(!subject||!section||!db.facultymembers.some(f=>f.facultyMemberID===value.facultyMemberID)||!db.classrooms.some(r=>r.classroomID===value.classroomID)||!db.timeslots.some(t=>t.timeSlotID===value.timeSlotID))throw new ApiError('اختر موارد موجودة في البيانات التجريبية.');
    Object.assign(value,{studyYearID:subject.studyYearID,branchID:subject.branchID,semesterNumber:subject.semesterNumber,isLocked:row?.isLocked??false});
    if(db.schedules.some(s=>s.scheduleID!==row?.scheduleID&&s.semesterNumber===value.semesterNumber&&s.dayOfWeek===value.dayOfWeek&&s.timeSlotID===value.timeSlotID&&(s.facultyMemberID===value.facultyMemberID||s.classroomID===value.classroomID||s.sectionID===value.sectionID)))throw new ApiError('يوجد تعارض في التدريسي أو القاعة أو الشعبة.');
  }
  if(resource==='assignments'){
    if(!db.subjects.some(s=>s.subjectID===value.subjectID&&!s.isDeleted)||!db.facultymembers.some(f=>f.facultyMemberID===value.facultyMemberID))throw new ApiError('اختر المادة والتدريسي من القوائم.');
    if(db.assignments.some(a=>key(resource,a)===key(resource,value)&&key(resource,a)!==id))throw new ApiError('التكليف موجود بالفعل.',409);
  }else value[keyField[resource]]=row?.[keyField[resource]]??Math.max(0,...db[resource].map(r=>r[keyField[resource]]))+1;
  if(method==='PUT'){const index=db[resource].findIndex(r=>key(resource,r)===String(id));db[resource][index]=value;}else db[resource].push(value);
  write();return envelope(list(db,resource).find(r=>key(resource,r)===key(resource,value)));
}
export async function all(resource){const rows=[];let page=1;while(true){const result=await api(`${resource}?page=${page++}&pageSize=200`);rows.push(...result.items);if(rows.length>=result.total||!result.items.length)return rows;}}
