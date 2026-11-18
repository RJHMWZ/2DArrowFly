
public class CountryUtil
{

    public static string GetCountryCode()
    {
#if IVY_SDK_DY || IVY_SDK_WX
        return "CN";
#elif IVY_SDK_TT
        
#endif

        return "CN";
    }

}