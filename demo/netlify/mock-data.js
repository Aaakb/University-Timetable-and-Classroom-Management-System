// Synthetic fixtures only. No university records or credentials are included.
export function createMockData() {
  const db={
    branches:[{branchID:1,branchName:'علوم الحاسوب'},{branchID:2,branchName:'شبكات الحاسوب'},{branchID:3,branchName:'تقنية المعلومات'}],
    studyyears:['المرحلة الأولى','المرحلة الثانية','المرحلة الثالثة','المرحلة الرابعة'].map((yearName,i)=>({studyYearID:i+1,yearName})),
    facultymembers:['أحمد علي','سارة حسن','عمر خالد','مريم سامي','حيدر كريم','نور عباس','يوسف مازن','زينب عادل'].map((fullName,i)=>({facultyMemberID:i+1,fullName:'د. '+fullName,academicTitle:i%3===0?'أستاذ مساعد':'مدرس'})),
    classrooms:[...Array.from({length:4},(_,i)=>({classroomID:i+1,classroomNumber:'قاعة '+(101+i),capacity:80,roomType:'Lecture'})),{classroomID:5,classroomNumber:'مختبر البرمجة',capacity:40,roomType:'Lab'},{classroomID:6,classroomNumber:'مختبر الشبكات',capacity:40,roomType:'Lab'}],
    timeslots:['08:30','10:15','12:00','13:45'].map((start,i)=>({timeSlotID:i+1,startTime:start+':00',endTime:['10:00:00','11:45:00','13:30:00','15:15:00'][i]})),
    sections:[],subjects:[],assignments:[],schedules:[],profiles:{}
  };
  for(let year=1;year<=4;year++)for(let i=0;i<2;i++)db.sections.push({sectionID:db.sections.length+1,sectionName:year<=2?(i?'B':'A'):'A',studentCount:year===4?28:48,studyYearID:year,branchID:year<=2?null:i===0?1:year===3?2:3});
  const titles=[['مبادئ البرمجة','رياضيات الحاسوب','مقدمة في الحاسوب'],['البرمجة الكائنية','هياكل البيانات','قواعد البيانات'],['هندسة البرمجيات','تصميم الخوارزميات','شبكات الحاسوب'],['الذكاء الاصطناعي','أمن المعلومات','تطبيقات الويب']];
  for(let year=1;year<=4;year++)for(let term=1;term<=2;term++)for(let i=0;i<3;i++){
    const subjectID=db.subjects.length+1,branchID=year<=2?null:i<2?1:year===3?2:3;
    db.subjects.push({subjectID,subjectName:titles[year-1][i]+(term===2?' — متقدم':''),studyYearID:year,branchID,semesterNumber:term,theoreticalHours:2,practicalHours:i===1?0:2,creditUnits:3,theoreticalLectureCount:1,practicalLectureCount:i===1?0:1});
    db.assignments.push({facultyMemberID:(subjectID-1)%8+1,subjectID,studyYearID:year,branchID,semesterNumber:term,sectionID:null,lectureType:'Both'});
  }
  const days=['Sunday','Monday','Tuesday','Wednesday','Thursday'];
  for(const subject of db.subjects){
    const facultyMemberID=db.assignments.find(a=>a.subjectID===subject.subjectID).facultyMemberID;
    for(const section of db.sections.filter(s=>s.studyYearID===subject.studyYearID&&s.branchID===subject.branchID)){
      const lessons=[{lectureType:'Theory',groupName:null},...(subject.practicalLectureCount?(subject.studyYearID===4?[{lectureType:'Practical',groupName:null}]:[1,2].map(g=>({lectureType:'Practical',groupName:section.sectionName+g}))):[])];
      for(const lesson of lessons){
        let placed=false;
        for(const dayOfWeek of days){for(const slot of db.timeslots){for(const room of db.classrooms.filter(r=>lesson.lectureType==='Practical'?r.roomType==='Lab':r.roomType==='Lecture')){
          const conflict=db.schedules.some(s=>s.semesterNumber===subject.semesterNumber&&s.dayOfWeek===dayOfWeek&&s.timeSlotID===slot.timeSlotID&&(s.facultyMemberID===facultyMemberID||s.sectionID===section.sectionID||s.classroomID===room.classroomID));
          if(conflict)continue;
          db.schedules.push({scheduleID:db.schedules.length+1,subjectID:subject.subjectID,facultyMemberID,classroomID:room.classroomID,timeSlotID:slot.timeSlotID,sectionID:section.sectionID,studyYearID:subject.studyYearID,branchID:subject.branchID,semesterNumber:subject.semesterNumber,dayOfWeek,...lesson,isLocked:subject.subjectID===1&&lesson.lectureType==='Theory'});placed=true;break;
        }if(placed)break;}if(placed)break;}
        if(!placed)throw new Error('Mock fixture exceeds its configured capacity.');
      }
    }
  }
  return db;
}
