using System.ComponentModel;

namespace University_Timetable_and_Classroom_Management_System
{
    internal static class DesignerEnvironment
    {
        internal static bool IsDesignTime(Control control)
        {
            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime) return true;
            for (Control? current = control; current is not null; current = current.Parent)
                if (current.Site?.DesignMode == true) return true;
            return false;
        }
    }
}
