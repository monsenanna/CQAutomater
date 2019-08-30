using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;

namespace PlayFab.Internal
{
    /// <summary>
    /// This is a base-class for all Api-request objects.
    /// It is currently unfinished, but we will add result-specific properties,
    ///   and add template where-conditions to make some code easier to follow
    /// </summary>
    public class PlayFabRequestCommon
    {
    }

    /// <summary>
    /// This is a base-class for all Api-result objects.
    /// It is currently unfinished, but we will add result-specific properties,
    ///   and add template where-conditions to make some code easier to follow
    /// </summary>
    public class PlayFabResultCommon
    {
    }

    public class PlayFabJsonError
    {
        public int code;
        public string status;
        public string error;
        public int errorCode;
        public string errorMessage;
        public Dictionary<string, string[]> errorDetails = null;
    }

    public class PlayFabJsonSuccess<TResult> where TResult : PlayFabResultCommon
    {
        public int code;
        public string status;
        public TResult data;
    }

    public static class PlayFabHttp
    {
        private static readonly IPlayFabHttp _http;

        static PlayFabHttp()
        {
            try
            {
                var httpInterfaceType = typeof(IPlayFabHttp);
                using (StreamWriter sw = new StreamWriter("ErrorLog.txt", true))
                {
                    sw.WriteLine(DateTime.Now + "\n\t" + "httpInterfaceType" + "\n\t" + httpInterfaceType.ToString());
                }
                var types = typeof(PlayFabHttp).GetAssembly().GetTypes();
                foreach (var eachType in types)
                {
                    using (StreamWriter sw = new StreamWriter("ErrorLog.txt", true))
                    {
                        sw.WriteLine(DateTime.Now + "\n\t" + "eachType" + "\n\t" + eachType.ToString());
                    }
                    if (httpInterfaceType.IsAssignableFrom(eachType) && !eachType.IsAbstract)
                    {
                        _http = (IPlayFabHttp)Activator.CreateInstance(eachType.AsType());
                        using (StreamWriter sw = new StreamWriter("ErrorLog.txt", true))
                        {
                            sw.WriteLine(DateTime.Now + "\n\t" + "eachType CreateInstance done");
                        }
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                using (StreamWriter sw = new StreamWriter("ErrorLog.txt", true))
                {
                    sw.WriteLine(DateTime.Now + "\n\t" + "Error in PlayFabHttp" + "\n\t" + ex.Message);
                }
            }
            throw new Exception("Cannot find a valid IPlayFabHttp type");
        }

        public static async Task<object> DoPost(string urlPath, PlayFabRequestCommon request, string authType, string authKey, Dictionary<string, string> extraHeaders)
        {
            if (PlayFabSettings.TitleId == null)
                throw new Exception("You must set your titleId before making an api call");
            return await _http.DoPost(urlPath, request, authType, authKey, extraHeaders);
        }
    }
}
