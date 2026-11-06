import { api } from './api.js';

const params = new URLSearchParams(location.search);
const view = params.get('view');
if (view === 'students') {
  params.delete('view');
  location.replace('/#students' + (params.size ? '?' + params : ''));
} else {
  const workspace = document.getElementById('workspace');
  const form = document.getElementById('list-import');
  const error = document.getElementById('error');
  const status = document.getElementById('status');
  if (view === 'subjects' || view === 'faculty') {
    form.elements.kind.value = view;
    document.getElementById('return-link').href = view === 'subjects' ? '/#subjects' : '/#facultymembers';
  }
  try {
    const user = await api('auth/me');
    if (!user.canManage) throw new Error('هذه الصفحة متاحة لمسؤولي النظام.');
    workspace.hidden = false;
  } catch (ex) {
    error.textContent = ex.message;
    error.hidden = false;
  }
  form.addEventListener('submit', async event => {
    event.preventDefault();
    if (workspace.inert || !confirm('حفظ الأسماء الموجودة في الملف؟')) return;
    const body = new FormData(form);
    const kind = body.get('kind');
    body.delete('kind');
    workspace.inert = true;
    error.hidden = true;
    status.textContent = '';
    try {
      const result = await api(`list-imports/${kind}`, { method: 'POST', body });
      form.reset();
      form.elements.kind.value = kind;
      status.textContent = `أضيف ${result.added}، وتُخطّي ${result.skipped}.`;
    } catch (ex) {
      error.textContent = ex.message;
      error.hidden = false;
    } finally { workspace.inert = false; }
  });
}
