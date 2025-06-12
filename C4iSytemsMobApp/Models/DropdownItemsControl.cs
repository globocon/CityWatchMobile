
namespace C4iSytemsMobApp.Models
{
    public class DropdownItemsControl
    {
        public int Id { get; set; }
        public required string Name { get; set; }

        public override string ToString()
        {
            // This is what will show in the Picker dropdown
            return Name;
        }
    }
}
