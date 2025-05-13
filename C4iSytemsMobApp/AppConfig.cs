using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace C4iSytemsMobApp
{
    class AppConfig
    {
        //live url and test url ,test only work in localhost ,need https for api
        //public static string ApiBaseUrl { get; set; } = "https://cws-ir.com/api/";
        //public static string ApiBaseUrl { get; set; } = "http://test.c4i-system.com/api/";
        //public static string ApiBaseUrl { get; set; } = "https://localhost:44356/api/";

        //Added for local testing by Binoy
        public static string ApiBaseUrl { get; set; } = "http://localhost:91/api/";

    }
}
