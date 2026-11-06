using University_Timetable_and_Classroom_Management_System.BusinessLayer;

namespace University_Timetable_and_Classroom_Management_System;

internal static class RosterExportDialog
{
    public static async Task ShowAsync(Control owner, string title, string nameHeader, string detailHeader, IReadOnlyList<RosterRow> rows)
    {
        if (rows.Count == 0) return;
        char[] invalid = Path.GetInvalidFileNameChars();
        using var dialog = new SaveFileDialog
        {
            Title = "تصدير القائمة المعروضة",
            Filter = "PDF (*.pdf)|*.pdf|Excel (*.xlsx)|*.xlsx",
            FileName = string.Concat(title.Select(character => invalid.Contains(character) ? ' ' : character)).Trim(),
            AddExtension = true,
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(owner) != DialogResult.OK) return;

        try
        {
            string path = dialog.FileName;
            await Task.Run(() => RosterExportService.Export(path, title, nameHeader, detailHeader, rows));
            UiMessages.ShowInformation(owner, $"تم تصدير {rows.Count} سجلًا.", "تصدير القائمة");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            UiMessages.ShowError(owner, "تعذّر حفظ الملف. تأكد من إغلاقه ومن إمكانية الكتابة في المجلد المحدد.", "تصدير القائمة");
        }
        catch (Exception ex)
        {
            UiMessages.ShowError(owner, "تعذّر تصدير القائمة.", "تصدير القائمة", ex);
        }
    }
}
