const text = (key, label, max = 100, optional = false) => ({ key, label, type: 'text', max, optional });
const number = (key, label, min = 0, step = '1') => ({ key, label, type: 'number', min, step });
const lookup = (key, label, resource, optional = false) => ({ key, label, type: 'select', resource, optional });
const choice = (key, label, options, optional = false) => ({ key, label, type: 'select', options, optional });
export const days = [['Sunday','الأحد'],['Monday','الإثنين'],['Tuesday','الثلاثاء'],['Wednesday','الأربعاء'],['Thursday','الخميس']];
export const types = [['Theory','نظري'],['Practical','عملي']];
export const modules = {
  classrooms: { title:'القاعات والمختبرات', singular:'قاعة', icon:'▣', description:'إدارة المساحات التعليمية وسعتها ونوع الاستخدام.', key:'classroomID', label:'classroomNumber', fields:[text('classroomNumber','اسم / رقم القاعة'),number('capacity','السعة',1),choice('roomType','نوع القاعة',[['Lecture','قاعة محاضرات'],['Lab','مختبر'],['Shared Lab','مختبر مشترك']],true)] },
  branches: { title:'الفروع', singular:'فرع', icon:'⑂', description:'تنظيم الفروع الأكاديمية المرتبطة بالمواد والشعب.', key:'branchID', label:'branchName', fields:[text('branchName','اسم الفرع')] },
  studyyears: { title:'المراحل الدراسية', singular:'مرحلة دراسية', icon:'▤', description:'إدارة المراحل المعتمدة في الهيكل الدراسي.', key:'studyYearID', label:'yearName', fields:[text('yearName','اسم المرحلة')] },
  sections: { title:'الشعب', singular:'شعبة', icon:'◫', description:'توزيع الطلبة على الشعب وفق المرحلة والفرع.', key:'sectionID', label:'sectionName', fields:[text('sectionName','اسم الشعبة',50),number('studentCount','عدد الطلبة',1),lookup('studyYearID','المرحلة','studyyears'),lookup('branchID','الفرع','branches',true)] },
  subjects: { title:'المواد الدراسية', singular:'مادة', icon:'▥', description:'المواد وعدد المحاضرات النظرية والعملية الأسبوعية.', key:'subjectID', label:'subjectName', fields:[text('subjectName','اسم المادة'),lookup('studyYearID','المرحلة','studyyears'),lookup('branchID','الفرع','branches',true),choice('semesterNumber','الفصل الدراسي',[[1,'الأول'],[2,'الثاني']]),{...number('theoreticalLectureCount','عدد المحاضرات النظرية أسبوعياً'),lectureCount:true,maxValue:100},{...number('practicalLectureCount','عدد العملي أسبوعياً لكل مجموعة'),lectureCount:true,maxValue:100}] },
  facultymembers: { title:'التدريسيون', singular:'تدريسي', icon:'♙', description:'إدارة أسماء أعضاء الهيئة التدريسية وألقابهم العلمية.', key:'facultyMemberID', label:'fullName', fields:[text('fullName','الاسم الكامل',150),text('academicTitle','اللقب العلمي',100,true)] },
  assignments: { title:'التكليفات التدريسية', singular:'تكليف', icon:'⇄', description:'ربط كل مادة بالتدريسيين المخوّلين بتدريسها.', key:null, label:null, fields:[lookup('facultyMemberID','التدريسي','facultymembers'),lookup('subjectID','المادة','subjects')] },
  timeslots: { title:'الأوقات الدراسية', singular:'فترة دراسية', icon:'◷', description:'فترات المحاضرات المستخدمة في بناء الجدول.', key:'timeSlotID', label:null, fields:[{key:'startTime',label:'وقت البداية',type:'time'},{key:'endTime',label:'وقت النهاية',type:'time'}] },
  schedules: { title:'الجدول الدراسي', singular:'محاضرة', icon:'▦', description:'اعرض المحاضرات، عدّل توزيعها أو ولّد جدولًا جديدًا.', key:'scheduleID', label:null, fields:[lookup('subjectID','المادة','subjects'),lookup('facultyMemberID','التدريسي','facultymembers'),lookup('sectionID','الشعبة','sections'),lookup('classroomID','القاعة','classrooms'),lookup('timeSlotID','الفترة','timeslots'),choice('dayOfWeek','اليوم',days),choice('lectureType','نوع المحاضرة',types),choice('groupName','المجموعة العملية',[],true)] }
};
export function keyOf(resource, row) { return resource === 'assignments' ? `${row.facultyMemberID}-${row.subjectID}` : row[modules[resource].key]; }


export function lectureCounts(subject) {
  return {
    theoreticalLectureCount: subject.theoreticalLectureCount ?? (subject.theoreticalHours > 0 || !(subject.practicalHours > 0) ? 1 : 0),
    practicalLectureCount: subject.practicalLectureCount ?? (subject.practicalHours > 0 ? 1 : 0)
  };
}
