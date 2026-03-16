using System.ComponentModel.DataAnnotations;

namespace C4iSytemsMobApp.Enums
{
    public enum HrDateType
    {
        [Display(Name = "DOI / DOE")]
        Both = 0,
        [Display(Name = "DOI")]
        DOI = 1,
        [Display(Name = "DOE")]
        DOE = 2
    }
}
