
namespace C4iSytemsMobApp.Models
{
    public class ApiResponse
    {
        public bool IsSuccess { get; set; }
        public string message { get; set; }
    }

    public class ApiResponse<T>
    {
        public bool isSuccess { get; set; }
        public string message { get; set; }
        public T? data { get; set; }
    }

}
