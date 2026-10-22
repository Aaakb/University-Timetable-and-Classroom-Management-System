let csrf;
export class ApiError extends Error { constructor(message, status) { super(message); this.status = status; } }
export async function refreshCsrf() {
  const response = await fetch('/api/auth/csrf', { cache: 'no-store', credentials: 'same-origin' });
  if (!response.ok) throw new ApiError('تعذر تهيئة الاتصال الآمن بالخادم.', response.status);
  csrf = (await response.json()).token;
}
export async function api(path, options = {}) {
  const method = options.method || 'GET';
  const form = options.body instanceof FormData;
  const headers = { ...options.headers };
  if (method !== 'GET') {
    if (!csrf) await refreshCsrf();
    headers['X-CSRF-TOKEN'] = csrf;
    if (options.body !== undefined && !form) headers['Content-Type'] = 'application/json';
  }
  let response;
  try { response = await fetch(`/api/${path}`, { ...options, method, headers, cache: 'no-store', credentials: 'same-origin', body: options.body === undefined ? undefined : form ? options.body : JSON.stringify(options.body) }); }
  catch { throw new ApiError('تعذر الوصول إلى الخادم. تحقق من الاتصال وتشغيل التطبيق. إذا أرسلت عملية حفظ، حدّث البيانات للتحقق من نتيجتها قبل إعادة المحاولة.', 0); }
  if (!response.ok) {
    const data = await response.json().catch(() => ({}));
    const message = data.errors ? Object.values(data.errors).flat().join('\n') : data.title;
    if (response.status === 401 && !path.startsWith('auth/')) document.dispatchEvent(new Event('session-expired'));
    throw new ApiError(message || ({ 401: 'يرجى تسجيل الدخول.', 403: 'ليست لديك صلاحية.', 429: 'محاولات كثيرة. انتظر دقيقة ثم حاول مجددًا.' }[response.status] || 'تعذر إتمام الطلب.'), response.status);
  }
  if (options.blob) return response.blob();
  return response.status === 204 ? null : response.json();
}
export async function all(resource) {
  const rows = []; let page = 1;
  while (true) {
    const result = await api(`${resource}?page=${page++}&pageSize=200`);
    rows.push(...result.items);
    if (rows.length >= result.total || !result.items.length) return rows;
  }
}
