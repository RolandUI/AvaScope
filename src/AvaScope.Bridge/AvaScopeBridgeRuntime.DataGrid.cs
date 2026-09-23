using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Threading;
using AvaScope.Protocol;

namespace AvaScope.Bridge;

public sealed partial class AvaScopeBridgeRuntime
{
    // The host's optional DataGrid assembly owns its types. Referencing the package
    // here would make it a mandatory shared dependency of every standalone host.
    // All reflection below addresses documented public control/view/column APIs.
    private sealed class DataGridTable
    {
        private const string AssemblyName = "Avalonia.Controls.DataGrid";
        private readonly Type _gridType;

        public DataGridTable(Control control)
        {
            Dispatcher.UIThread.VerifyAccess();
            Control = control;
            _gridType = FindType(control.GetType(), "Avalonia.Controls.DataGrid")
                ?? throw new NotSupportedException("Select an Avalonia DataGrid with the supported public collection-view API.");
            if (_gridType.Assembly.GetName().Version?.Major != 12)
                throw new NotSupportedException("Table operations require an Avalonia 12 DataGrid. The validated package line is DataGrid 12.1.x.");
            _ = View;
        }

        public Control Control { get; }
        public object View => Read(Control, "CollectionView") ?? throw new NotSupportedException("The DataGrid has no current public collection view.");
        public bool ReadOnly => Read(Control, "IsReadOnly") is true;
        public bool Valid => Read(Control, "IsValid") is true;
        public bool Editing => Read(View, "IsEditingItem") is true || Read(View, "IsAddingNew") is true;
        public object? SelectedItem => Read(Control, "SelectedItem");
        public IReadOnlyList<object> SelectedItems
        {
            get
            {
                var items = (Read(Control, "SelectedItems") as IEnumerable)?.Cast<object>().Take(65).ToArray() ?? [];
                return items.Length <= 64 ? items : throw new NotSupportedException("At most 64 selected rows can be observed completely.");
            }
        }

        public (object View, object[] Rows, int? Total, bool Complete) Rows(int maximum, Action budget)
        {
            var view = View;
            var rows = new List<object>();
            int? total = view is ICollection collection ? collection.Count : null;
            if (view is not IEnumerable enumerable) throw new NotSupportedException("The public table view does not enumerate rows.");
            var iterator = enumerable.GetEnumerator();
            var complete = false;
            try
            {
                while (rows.Count <= maximum)
                {
                    budget();
                    if (!iterator.MoveNext()) { complete = true; break; }
                    var item = iterator.Current ?? throw new NotSupportedException("Null/new-item placeholder rows have no safe stable identity.");
                    if (item.GetType().IsValueType) throw new NotSupportedException("Table row identity requires reference-type rows; boxed value rows are not stable across reads.");
                    rows.Add(item);
                }
            }
            finally { (iterator as IDisposable)?.Dispose(); }
            if (rows.Count > maximum) rows.RemoveAt(rows.Count - 1);
            return (view, rows.ToArray(), total ?? (complete ? rows.Count : null), complete);
        }

        public IReadOnlyList<DataGridColumnInfo> Columns()
        {
            if (Read(Control, "Columns") is not IEnumerable values) throw new NotSupportedException("The table does not expose public columns.");
            var columns = values.Cast<object>().Take(65).ToArray();
            if (columns.Length > 64) throw new NotSupportedException("At most 64 column identities can be inspected safely; narrow the UI table first.");
            var result = new List<DataGridColumnInfo>();
            foreach (var column in columns)
            {
                if (Read(column, "IsVisible") is false) continue;
                var binding = Read(column, "Binding");
                var path = binding is ReflectionBinding reflection ? reflection.Path
                    : binding is CompiledBinding compiled ? compiled.Path?.ToString() : null;
                var scalarPath = path is { Length: > 0 and <= 128 } && path.All(character => char.IsLetterOrDigit(character) || character == '_') ? path : null;
                var header = Read(column, "Header") as string;
                var tag = Read(column, "Tag") as string;
                var id = tag is { Length: > 0 and <= 128 } ? "tag:" + tag
                    : scalarPath is not null ? "binding:" + scalarPath
                    : header is { Length: > 0 and <= 128 } ? "header:" + header : null;
                if (id is null) throw new NotSupportedException("Each visible column needs a bounded string Tag, simple binding path or string header for identity.");
                var declaredType = column.GetType();
                var supported = declaredType.Assembly == _gridType.Assembly
                    && declaredType.FullName is "Avalonia.Controls.DataGridTextColumn" or "Avalonia.Controls.DataGridCheckBoxColumn";
                // Only an ordinary row-context binding identifies this row's public
                // property. Element/ancestor/explicit-source bindings are not rows.
                var source = binding is null ? null : Read(binding, "Source");
                var converter = binding is null ? null : Read(binding, "Converter");
                supported &= binding is not null && (source is null || ReferenceEquals(source, AvaloniaProperty.UnsetValue))
                    && Read(binding, "RelativeSource") is null && string.IsNullOrEmpty(Read(binding, "ElementName") as string)
                    && string.IsNullOrEmpty(Read(binding, "StringFormat") as string)
                    && (converter is null || converter.GetType().Assembly == _gridType.Assembly
                        && converter.GetType().FullName == "Avalonia.Controls.DataGridValueConverter");
                var sortPath = Read(column, "SortMemberPath") as string;
                sortPath = string.IsNullOrWhiteSpace(sortPath) ? scalarPath : sortPath;
                result.Add(new(column, id, scalarPath, header, Read(column, "DisplayIndex") as int? ?? result.Count,
                    supported && scalarPath is not null, ReadOnly || Read(column, "IsReadOnly") is not false
                        || binding is not null && Read(binding, "Mode") is BindingMode.OneWay or BindingMode.OneTime or BindingMode.OneWayToSource,
                    Read(Control, "CanUserSortColumns") is true && Read(column, "CanUserSort") is true && !string.IsNullOrWhiteSpace(sortPath), sortPath));
            }
            if (result.Select(column => column.Id).Distinct(StringComparer.Ordinal).Count() != result.Count)
                throw new NotSupportedException("Visible columns have ambiguous identities. Set distinct public string Tags or binding paths.");
            return result.OrderBy(column => column.DisplayIndex).ToArray();
        }

        public IReadOnlyList<RuntimeTableSort> Sorts()
        {
            if (Read(View, "SortDescriptions") is not IEnumerable descriptions) return [];
            var items = descriptions.Cast<object>().Take(9).ToArray();
            if (items.Length > 8) throw new NotSupportedException("At most eight current sort descriptions can be inspected.");
            return items.Select(item => new RuntimeTableSort(Read(item, "PropertyPath") is string { Length: <= 512 } path ? path : null,
                Read(item, "Direction") is ListSortDirection direction ? direction == ListSortDirection.Ascending ? "ascending" : "descending" : "unknown")).ToArray();
        }

        public Control? Cell(object row, DataGridColumnInfo column) =>
            column.Column.GetType().GetMethod("GetCellContent", BindingFlags.Public | BindingFlags.Instance, [typeof(object)])?.Invoke(column.Column, [row]) as Control;

        public void Select(object row) => _gridType.GetProperty("SelectedItem")!.SetValue(Control, row);
        public void Reveal(object row, DataGridColumnInfo? column) => InvokeGrid("ScrollIntoView", row, column?.Column);
        public void SetCurrentColumn(DataGridColumnInfo column) => _gridType.GetProperty("CurrentColumn")!.SetValue(Control, column.Column);
        public bool BeginEdit()
        {
            if (Editing) throw new InvalidOperationException("An application edit/add transaction is already active; finish it explicitly first.");
            return InvokeGrid("BeginEdit") is true;
        }
        public bool CommitEdit() => InvokeGrid("CommitEdit") is true;
        public void Sort(DataGridColumnInfo column, string direction) => column.Column.GetType()
            .GetMethod("Sort", BindingFlags.Public | BindingFlags.Instance, [typeof(ListSortDirection)])!
            .Invoke(column.Column, [direction == "ascending" ? ListSortDirection.Ascending : ListSortDirection.Descending]);

        private object? InvokeGrid(string method, params object?[] args)
        {
            var member = _gridType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .SingleOrDefault(candidate => candidate.Name == method && candidate.GetParameters().Length == args.Length)
                ?? throw new NotSupportedException("The table does not expose the expected public " + method + " operation.");
            return member.Invoke(Control, args);
        }

        public static (string? Key, string Status) Key(object row, string property)
        {
            var member = row.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.Instance);
            if (member?.GetMethod?.IsPublic != true || member.GetIndexParameters().Length > 0 || !IsItemKeyType(member.PropertyType))
                return (null, "unavailable");
            var value = member.GetValue(row);
            var text = value switch { string content => content, Guid id => id.ToString("D"), IFormattable number => number.ToString(null, CultureInfo.InvariantCulture), _ => null };
            return string.IsNullOrWhiteSpace(text) || text.Length > 256 ? (null, "unavailable") : (text, "present");
        }

        public static (string Type, string Status, JsonElement? Value) Value(object row, DataGridColumnInfo column)
        {
            if (!column.Supported || column.Path is null) return ("unknown", "unsupported_binding", null);
            var member = row.GetType().GetProperty(column.Path, BindingFlags.Public | BindingFlags.Instance);
            if (member?.GetMethod?.IsPublic != true || member.GetIndexParameters().Length > 0) return ("unknown", "unavailable", null);
            var type = Nullable.GetUnderlyingType(member.PropertyType) ?? member.PropertyType;
            var kind = ValueType(type);
            if (kind == "unknown") return (kind, "unsupported_value", null);
            var value = member.GetValue(row);
            if (value is null) return (kind, "null", JsonSerializer.SerializeToElement<object?>(null));
            if (type.IsEnum)
            {
                value = Enum.GetName(type, value);
                if (value is null) return (kind, "unsupported_value", null);
            }
            if (value is string text && text.Length > 4096) return (kind, "value_limit", null);
            if (value is double number && !double.IsFinite(number) || value is float single && !float.IsFinite(single))
                return (kind, "unavailable", null);
            return (kind, "present", JsonSerializer.SerializeToElement(value));
        }

        public static string ValueType(Type type) => type == typeof(bool) ? "boolean"
            : type == typeof(string) || type.IsEnum || type == typeof(Guid) || type == typeof(DateTime) || type == typeof(DateTimeOffset) ? "string"
            : type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) || type == typeof(ushort) || type == typeof(int) || type == typeof(uint)
                || type == typeof(long) || type == typeof(ulong) || type == typeof(decimal) || type == typeof(float) || type == typeof(double) ? "number" : "unknown";

        public static object? Read(object instance, string property)
        {
            var member = instance.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.Instance);
            member ??= instance.GetType().GetInterfaces().Where(type => type.Assembly.GetName().Name == AssemblyName)
                .Select(type => type.GetProperty(property, BindingFlags.Public | BindingFlags.Instance)).FirstOrDefault(member => member is not null);
            return member?.GetMethod?.IsPublic == true && member.GetIndexParameters().Length == 0 ? member.GetValue(instance) : null;
        }

        public static Type? FindType(Type? type, string name)
        {
            for (; type is not null; type = type.BaseType)
                if (type.FullName == name && type.Assembly.GetName().Name == AssemblyName) return type;
            return null;
        }
    }

    private sealed record DataGridColumnInfo(object Column, string Id, string? Path, string? Header, int DisplayIndex,
        bool Supported, bool ReadOnly, bool Sortable, string? SortPath);
}
