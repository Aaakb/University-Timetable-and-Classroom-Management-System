import {resetDemo} from './api.js';
const message='نسخة عرض ببيانات افتراضية. التغييرات محفوظة في هذا المتصفح فقط.';
const banner=document.createElement('div');banner.className='demo-banner';banner.setAttribute('role','note');
const label=document.createElement('span');label.textContent=message;banner.append(label);
const reset=document.createElement('button');reset.textContent='إعادة البيانات التجريبية';reset.className='secondary';banner.append(reset);document.body.prepend(banner);
reset.onclick=()=>{if(confirm('إعادة جميع بيانات العرض الافتراضية؟ سيُستبدل ما عدّلته في هذه النسخة التجريبية على هذا المتصفح.')){resetDemo();location.reload();}};
const unsupported=['generate','export','open-import','download-template','validate-import','skip-import','generate-import','save-import','download-backup','preview-restore','restore-backup','day-save','pref-save','pref-preview','pref-commit','alt-generate','alt-save'];
function decorate(){
  for(const id of unsupported){const button=document.getElementById(id);if(button){button.disabled=true;button.title='تتطلب هذه العملية خادم النظام الحقيقي؛ غير متاحة في نسخة العرض.';}}
  const footer=document.querySelector('.sidebar-footer');if(footer&&footer.dataset.demo!=='true'){footer.dataset.demo='true';footer.replaceChildren();footer.textContent='بيانات تجريبية — مثال توضيحي';}
  const note=document.querySelector('#auth .fine'),noteText='عرض تجريبي فقط؛ لا تستخدم حساب الجامعة أو كلمة مرور حقيقية.';if(note&&note.textContent!==noteText)note.textContent=noteText;
  const description=document.getElementById('auth-description'),descriptionText='أدخل أي اسم وكلمة مرور تجريبية للعودة إلى العرض.';if(description&&description.textContent!==descriptionText)description.textContent=descriptionText;
  const register=document.getElementById('auth-switch');if(register)register.hidden=true;
}
decorate();new MutationObserver(decorate).observe(document.body,{childList:true,subtree:true});
