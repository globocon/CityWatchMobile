
namespace C4iSytemsMobApp.Models
{
    public class SelectListItem
    {
        public string Text { get; set; }
        public string Value { get; set; }
        
        public override string ToString()
        {
            // This is what will show in the Picker dropdown
            return Text;
        }
    }

}
