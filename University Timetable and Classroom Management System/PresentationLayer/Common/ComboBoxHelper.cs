using Guna.UI2.WinForms;

namespace University_Timetable_and_Classroom_Management_System
{
    internal static class ComboBoxHelper
    {
        public static void Bind(Guna2ComboBox combo, IEnumerable<ComboOption> options, int selectedIndex = -1)
        {
            combo.DataSource = options.ToList();
            combo.DisplayMember = nameof(ComboOption.Text);
            combo.ValueMember = nameof(ComboOption.Id);
            combo.SelectedIndex = selectedIndex;
        }

        public static int GetSelectedRequiredId(Guna2ComboBox combo)
        {
            return GetSelectedOptionalId(combo) ?? 0;
        }

        public static int? GetSelectedOptionalId(Guna2ComboBox combo)
        {
            return combo.SelectedItem is ComboOption option ? option.Id : null;
        }

        public static void SelectValue(Guna2ComboBox combo, int? id, bool selectNullOption = true)
        {
            if (!id.HasValue && selectNullOption)
            {
                foreach (var item in combo.Items)
                {
                    if (item is ComboOption { Id: null })
                    {
                        combo.SelectedItem = item;
                        return;
                    }
                }
            }

            if (id.HasValue)
            {
                foreach (var item in combo.Items)
                {
                    if (item is ComboOption option && option.Id == id.Value)
                    {
                        combo.SelectedItem = item;
                        return;
                    }
                }
            }

            combo.SelectedIndex = -1;
        }

        public static void SelectText(Guna2ComboBox combo, string? text, bool addMissing = false)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                combo.SelectedIndex = -1;
                return;
            }

            foreach (var item in combo.Items)
            {
                if (string.Equals(item?.ToString(), text, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedItem = item;
                    return;
                }
            }

            if (addMissing)
            {
                combo.Items.Add(text);
                combo.SelectedItem = text;
                return;
            }

            combo.SelectedIndex = -1;
        }

        public static string? GetSelectedPlainText(Guna2ComboBox combo)
        {
            return combo.SelectedItem?.ToString();
        }

        public static void Clear(Guna2ComboBox combo)
        {
            combo.SelectedIndex = -1;
            combo.Text = string.Empty;
        }
    }
}
