using University_Timetable_and_Classroom_Management_System.BusinessLayer;

namespace University_Timetable_and_Classroom_Management_System;

public partial class SchedulesForm
{
    private readonly TimetableBackupService timetableBackupService = new();
    private async Task SaveTimetableBackupAsync()
    {
        if (IsScheduleUiUnavailable) return;
        using var dialog = new SaveFileDialog
        {
            Title = "حفظ نسخة من الجدول الكامل للفصلين — بغض النظر عن الفلاتر",
            Filter = "Timetable backup (*.uttbackup.json)|*.uttbackup.json",
            FileName = $"Timetable-{DateTime.Now:yyyyMMdd-HHmmss}.uttbackup.json",
            DefaultExt = "uttbackup.json", AddExtension = true, OverwritePrompt = true
        };
        if (dialog.ShowDialog(this)!=DialogResult.OK) return;
        SetScheduleActionsEnabled(false);
        try
        {
            var result=await timetableBackupService.SaveAsync(dialog.FileName);
            if (IsScheduleUiUnavailable) return;
            MessageBox.Show(this,$"حُفظت نسخة من {result.LectureCount} محاضرة.\nالفصل الأول: {result.SemesterOneCount} — الفصل الثاني: {result.SemesterTwoCount}\n\n{result.Path}\n\nتستعيد النسخة الجدول كاملًا؛ احتفظ أيضًا ببيانات المواد والتدريسيين والقاعات المرتبطة به.",
                "نسخة الجدول",MessageBoxButtons.OK,MessageBoxIcon.Information);
        }
        catch(Exception ex){ShowTimetableBackupError("تعذّر حفظ نسخة الجدول.",ex);}
        finally {SetScheduleActionsEnabled(true);}
    }
    private async Task RestoreTimetableBackupAsync()
    {
        if (IsScheduleUiUnavailable) return;
        using var dialog = new OpenFileDialog
        {
            Title = "اختيار نسخة جدول لاستعادتها",Filter = "Timetable backup (*.uttbackup.json)|*.uttbackup.json",CheckFileExists = true
        };
        if(dialog.ShowDialog(this)!=DialogResult.OK)return;
        SetScheduleActionsEnabled(false);
        try
        {
            var preview=await timetableBackupService.PreviewRestoreAsync(dialog.FileName);
            if(IsScheduleUiUnavailable)return;
            if(!preview.CanRestore)
            {
                MessageBox.Show(this,"لا يمكن استعادة هذه النسخة:\n\n"+string.Join(Environment.NewLine,preview.Issues.Take(10)),"فحص النسخة",MessageBoxButtons.OK,MessageBoxIcon.Warning);
                return;
            }
            string safetyPath=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(dialog.FileName))!,
                $"BeforeRestore-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.uttbackup.json");
            string summary=$"تاريخ النسخة: {preview.CreatedAtUtc.ToLocalTime():yyyy/MM/dd HH:mm}\n"+
                $"الجدول الحالي: {preview.CurrentLectureCount} محاضرة\nالنسخة المختارة: {preview.LectureCount} محاضرة\n"+
                $"الفصل الأول: {preview.SemesterOneCount} — الفصل الثاني: {preview.SemesterTwoCount}\n\n"+
                "ستُستبدل جميع محاضرات الفصلين بالنسخة المختارة، بغض النظر عن الفلاتر. لا تتغير المواد أو التدريسيون أو القاعات.\n\n"+
                "ستُحفظ نسخة أمان من الجدول الحالي قبل أي استبدال في:\n"+safetyPath+"\n\nهل تريد المتابعة؟";
            if(MessageBox.Show(this,summary,"تأكيد استعادة الجدول",MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)!=DialogResult.Yes)return;
            var result=await timetableBackupService.RestoreAsync(preview,true,safetyPath);
            if(IsScheduleUiUnavailable)return;
            await LoadSchedulesAsync();
            if(IsScheduleUiUnavailable)return;
            ClearScheduleForm();
            MessageBox.Show(this,$"استُعيدت {result.RestoredCount} محاضرة بمعرّفاتها وأوقاتها وقاعاتها الأصلية.\n\nنسخة الجدول السابق:\n{result.SafetyBackupPath}","استعادة الجدول",MessageBoxButtons.OK,MessageBoxIcon.Information);
        }
        catch(Exception ex){ShowTimetableBackupError("تعذّرت استعادة الجدول؛ لم يُحفظ استبدال جزئي.",ex);}
        finally {SetScheduleActionsEnabled(true);}
    }
    private void ShowTimetableBackupError(string operation,Exception ex)
    {
        if(IsScheduleUiUnavailable)return;
        string detail=ex is IOException or UnauthorizedAccessException or InvalidOperationException ? ex.Message : UiMessages.GetUserSafeExceptionMessage(ex);
        MessageBox.Show(this,operation+"\n\n"+detail,"نسخة الجدول",MessageBoxButtons.OK,MessageBoxIcon.Warning);
    }
}
