namespace WinFormsApp1;

/// <summary>
/// Универсальное диалоговое окно. Каждое поле располагается в собственной строке:
/// слева подпись, справа элемент ввода. Списки выбора используются там,
/// где значение должно соответствовать перечислению предметной области.
/// </summary>
public sealed class InputDialog : Form
{
    private readonly Dictionary<string, Control> _controls = new(StringComparer.Ordinal);

    public InputDialog(string title, params (string Label, string Value)[] fields)
    {
        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        MinimumSize = new Size(560, 220);
        Width = 650;
        Height = Math.Min(190 + fields.Length * 54,
            Screen.FromPoint(Cursor.Position).WorkingArea.Height - 80);
        Font = new Font("Segoe UI", 9.5f);
        AutoScaleMode = AutoScaleMode.Font;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14),
            ColumnCount = 2,
            RowCount = fields.Length + 1,
            AutoScroll = true,
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 205));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        for (int i = 0; i < fields.Length; i++)
        {
            var field = fields[i];
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

            var label = new Label
            {
                Text = field.Label,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Margin = new Padding(3, 3, 10, 3)
            };

            Control editor = CreateEditor(field.Label, field.Value);
            editor.Dock = DockStyle.Fill;
            editor.Margin = new Padding(3, 8, 3, 8);
            editor.MinimumSize = new Size(180, 26);

            layout.Controls.Add(label, 0, i);
            layout.Controls.Add(editor, 1, i);
            _controls[field.Label] = editor;
        }

        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 5, 0, 0),
            Margin = new Padding(0)
        };

        var save = new Button
        {
            Text = "Сохранить",
            DialogResult = DialogResult.OK,
            Width = 115,
            Height = 30,
            AutoSize = false
        };
        var cancel = new Button
        {
            Text = "Отмена",
            DialogResult = DialogResult.Cancel,
            Width = 95,
            Height = 30,
            AutoSize = false
        };
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        layout.Controls.Add(buttons, 0, fields.Length);
        layout.SetColumnSpan(buttons, 2);

        AcceptButton = save;
        CancelButton = cancel;
        Controls.Add(layout);
    }

    private static Control CreateEditor(string label, string value)
    {
        string[] options = label switch
        {
            "Категория" or "Категория клиента" => ["Взрослый", "Ребёнок", "Студент", "Группа"],
            "Тип услуги" => ["РазовоеПосещение", "Абонемент", "ОбучениеПлаванию", "ГрупповоеЗанятие"],
            "Способ оплаты" => ["Наличные", "БанковскаяКарта", "БезналичныйРасчёт"],
            "Тип основания" => ["Билет", "Абонемент"],
            _ => []
        };

        if (options.Length > 0)
        {
            var combo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                IntegralHeight = true
            };
            combo.Items.AddRange(options);
            int selected = Array.FindIndex(options,
                item => string.Equals(item, value, StringComparison.OrdinalIgnoreCase));
            combo.SelectedIndex = selected >= 0 ? selected : 0;
            return combo;
        }

        return new TextBox
        {
            Text = value,
            BorderStyle = BorderStyle.FixedSingle
        };
    }

    public string Value(string label)
    {
        if (!_controls.TryGetValue(label, out var control))
            throw new ArgumentException($"Поле «{label}» не найдено.", nameof(label));

        return control switch
        {
            TextBox textBox => textBox.Text.Trim(),
            ComboBox comboBox => Convert.ToString(comboBox.SelectedItem)?.Trim() ?? "",
            _ => ""
        };
    }
}
