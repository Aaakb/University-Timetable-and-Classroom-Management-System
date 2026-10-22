using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace University_Timetable_and_Classroom_Management_System
{
    internal static class UiMessages
    {
        public const string ConfirmDelete = "هل تريد حذف هذا السجل؟";
        public const string RecordAdded = "تمت إضافة السجل بنجاح.";
        public const string RecordUpdated = "تم تعديل السجل بنجاح.";
        public const string RecordDeleted = "تم حذف السجل بنجاح.";
        public const string RequiredFields = "يرجى إدخال جميع الحقول المطلوبة.";
        private const string UnexpectedError = "حدث خطأ غير متوقع. يرجى المحاولة مرة أخرى.";

        public static DialogResult ConfirmDeletion(IWin32Window? owner, string title)
        {
            if (!CanShow(owner)) return DialogResult.No;
            return MessageBox.Show(
                owner,
                ConfirmDelete,
                title,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
        }

        public static DialogResult Confirm(IWin32Window? owner, string message, string title)
        {
            if (!CanShow(owner)) return DialogResult.No;
            return MessageBox.Show(
                owner,
                ArabicUiText.Message(message),
                title,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
        }

        public static void ShowInformation(IWin32Window? owner, string message, string title)
        {
            if (!CanShow(owner)) return;
            MessageBox.Show(owner, ArabicUiText.Message(message), title, MessageBoxButtons.OK, MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button1, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
        }

        public static void ShowWarning(IWin32Window? owner, string message, string title)
        {
            if (!CanShow(owner)) return;
            MessageBox.Show(owner, ArabicUiText.Message(message), title, MessageBoxButtons.OK, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button1, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
        }

        public static void ShowError(IWin32Window? owner, string message, string title)
        {
            if (!CanShow(owner)) return;
            MessageBox.Show(owner, ArabicUiText.Message(message), title, MessageBoxButtons.OK, MessageBoxIcon.Error,
                MessageBoxDefaultButton.Button1, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
        }

        public static void ShowError(IWin32Window? owner, string message, string title, Exception exception)
        {
            if (!CanShow(owner)) return;
            MessageBox.Show(owner, $"{ArabicUiText.Message(message)}\n\n{GetUserSafeExceptionMessage(exception)}", title, MessageBoxButtons.OK, MessageBoxIcon.Error,
                MessageBoxDefaultButton.Button1, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
        }

        private static bool CanShow(IWin32Window? owner)
        {
            return owner is not Control control || (!control.IsDisposed && !control.Disposing);
        }

        public static string GetUserSafeExceptionMessage(Exception exception)
        {
            return exception switch
            {
                ArgumentException or KeyNotFoundException => ArabicUiText.Message(exception.Message),
                DbUpdateException { InnerException: SqlException { Number: 547 } } =>
                    "هذا السجل مرتبط بسجلات أخرى. احذف الارتباطات أو أعد تعيينها قبل حذفه أو تعديله.",
                DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } } =>
                    "يوجد سجل بالبيانات نفسها. حدّث الصفحة وتحقق من التكرار.",
                DbUpdateException { InnerException: SqlException { Number: 8152 or 2628 } } =>
                    "إحدى القيم المدخلة أطول من الحد المسموح. اختصرها وحاول مرة أخرى.",
                _ => UnexpectedError
            };
        }
    }
}
