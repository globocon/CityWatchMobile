
using CommunityToolkit.Maui.Views;

namespace C4iSytemsMobApp.Views;

public partial class EditLogPopup : Popup
{
    private readonly DictionaryWrapper _item;
    private readonly Dictionary<string, Entry> _editEntries = new();

    public EditLogPopup(DictionaryWrapper item)
    {
        InitializeComponent();
        _item = item;
        CreateEditableFields();
    }

    private void CreateEditableFields()
    {

       // var color = Application.Current.RequestedTheme == AppTheme.Dark ? Colors.White : Colors.Black;

        foreach (var kv in _item.KeyValues)
        {
            bool _isReadOnly = false;
            _isReadOnly = kv.Key.ToLower().Replace(" ", "").Equals("timeslot");
            var label = new Label
            {
                Text = kv.Key,
                FontAttributes = FontAttributes.Bold
            };
            label.SetAppThemeColor(Label.TextColorProperty, Colors.Black, Colors.White);

            var entry = new Entry
            {
                Text = kv.Value,
                Placeholder = $"Enter {kv.Key} value",
                IsReadOnly = _isReadOnly,
                //TextColor = (Color)new AppThemeBindingExtension
                //{
                //    Light = Colors.Black,
                //    Dark = Colors.White
                //}.ProvideValue(null)
            };
            entry.SetAppThemeColor(Entry.TextColorProperty, Colors.Black, Colors.White);

            _editEntries[kv.Key] = entry;

            EditFieldsContainer.Add(label);
            EditFieldsContainer.Add(entry);
        }
    }

    private void OnCancelClicked(object sender, EventArgs e)
    {
        Close(); // Just close without returning anything
    }

    private void OnSaveClicked(object sender, EventArgs e)
    {
        // Update KeyValues with edited values
        foreach (var key in _editEntries.Keys)
        {
            var existingPair = _item.KeyValues.FirstOrDefault(kv => kv.Key == key);
            if (!existingPair.Equals(default(KeyValuePair<string, string>)))
            {
                int index = _item.KeyValues.IndexOf(existingPair);
                _item.KeyValues[index] = new KeyValuePair<string, string>(key, _editEntries[key].Text);
            }
        }

        Close(_item); // Return updated data to caller
    }
}
