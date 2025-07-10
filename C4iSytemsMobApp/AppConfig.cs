
namespace C4iSytemsMobApp
{
    class AppConfig
    {
        //live url and test url ,test only work in localhost ,need https for api
        //public static string ApiBaseUrl { get; set; } = "https://cws-ir.com/api/";
        //public static string MobileSignalRBaseUrl { get; set; } = "https://cws-ir.com";

        // ### Test Url ####
        //public static string ApiBaseUrl { get; set; } = "http://test.c4i-system.com/api/";
        //public static string MobileSignalRBaseUrl { get; set; } = "http://test.c4i-system.com";


        //public static string ApiBaseUrl { get; set; } = "https://localhost:44356/api/";

        //#### Added for local testing by Binoy        
        public static string ApiBaseUrl { get; set; } = "http://192.168.1.36:91/api/";
        public static string MobileSignalRBaseUrl { get; set; } = "http://192.168.1.36:91";

    }
}
