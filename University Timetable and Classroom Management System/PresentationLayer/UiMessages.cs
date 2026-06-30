namespace University_Timetable_and_Classroom_Management_System
{
    internal static class UiMessages
    {
        public const string ConfirmDelete = "Are you sure you want to delete this item?";
        public const string RecordAdded = "Record added successfully.";
        public const string RecordUpdated = "Record updated successfully.";
        public const string RecordDeleted = "Record deleted successfully.";
        public const string RequiredFields = "Please fill in all required fields.";
        private const string UnexpectedError = "An unexpected error occurred. Please try again.";

        public static DialogResult ConfirmDeletion(IWin32Window? owner, string title)
        {
            return MessageBox.Show(
                owner,
                ConfirmDelete,
                title,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
        }

        public static DialogResult Confirm(IWin32Window? owner, string message, string title)
        {
            return MessageBox.Show(
                owner,
                message,
                title,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
        }

        public static void ShowInformation(IWin32Window? owner, string message, string title)
        {
            MessageBox.Show(owner, message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public static void ShowWarning(IWin32Window? owner, string message, string title)
        {
            MessageBox.Show(owner, message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        public static void ShowError(IWin32Window? owner, string message, string title)
        {
            MessageBox.Show(owner, message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public static void ShowError(IWin32Window? owner, string message, string title, Exception exception)
        {
            MessageBox.Show(owner, $"{message}\n\n{GetUserSafeExceptionMessage(exception)}", title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public static string GetUserSafeExceptionMessage(Exception exception)
        {
            return exception switch
            {
                ArgumentException or KeyNotFoundException => exception.Message,
                _ => UnexpectedError
            };
        }
    }
}
