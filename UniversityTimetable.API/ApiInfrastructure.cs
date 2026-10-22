using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ImportUiText = University_Timetable_and_Classroom_Management_System.ImportUiText;

namespace UniversityTimetable.API;

public sealed class ApiFailure(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}

public sealed class ApiErrors(RequestDelegate next, ILogger<ApiErrors> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            var (status, message) = ex switch
            {
                ApiFailure e => (e.Status, e.Message),
                AntiforgeryValidationException => (400, "انتهت صلاحية رمز الحماية. أعد تحميل الصفحة ثم حاول مجددًا."),
                ValidationException or ArgumentException => (400, ImportUiText.Message(ex.Message)),
                KeyNotFoundException => (404, ex.Message),
                JsonException => (400, "صيغة البيانات غير صحيحة."),
                DbUpdateConcurrencyException => (409, "تغير السجل. حدّث البيانات قبل المحاولة مجددًا."),
                DbUpdateException => (409, "تعذر الحفظ: تحقق من التكرار والسجلات المرتبطة، ثم حدّث البيانات."),
                SqlException e when e.Number is 1205 or 1222 => (409, "البيانات مستخدمة حاليًا. حدّث الصفحة وحاول مجددًا."),
                SqlException => (503, "تعذر الاتصال بقاعدة البيانات أو إتمام الطلب. تحقق من خدمة SQL Server والإعدادات."),
                _ => (500, "تعذر إتمام العملية. راجع سجل الخادم باستخدام رقم التتبع.")
            };
            // Do not log exception text: provider messages can contain submitted data.
            logger.LogWarning("API failure {Type}, status {Status}, trace {Trace}", ex.GetType().Name, status, context.TraceIdentifier);
            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(new { title = message, status, traceId = context.TraceIdentifier });
        }
    }
}

public static class Versions
{
    public static string Of(object value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    public static void Require(HttpRequest request, object current)
    {
        var expected = request.Headers.IfMatch.ToString().Trim('"');
        if (string.IsNullOrEmpty(expected)) throw new ApiFailure(428, "يجب تحديث السجل قبل تعديله أو حذفه.");
        if (expected != Of(current)) throw new ApiFailure(412, "تم تعديل السجل منذ تحميله. حدّث البيانات وأعد المحاولة.");
    }
    public static object Envelope(object value) => new { data = value, version = Of(value) };
}

public sealed class MutationGate
{
    private readonly SemaphoreSlim gate = new(1, 1);
    public async Task<IDisposable> EnterAsync(CancellationToken cancellationToken)
    {
        if (!await gate.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken))
            throw new ApiFailure(409, "توجد عملية حفظ أو توليد قيد التنفيذ. حاول بعد اكتمالها.");
        return new Release(gate);
    }
    private sealed class Release(SemaphoreSlim gate) : IDisposable { public void Dispose() => gate.Release(); }
}
