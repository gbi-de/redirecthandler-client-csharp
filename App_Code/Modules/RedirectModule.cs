using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;

/// <summary>
/// Summary description for RedirectModule
/// </summary>
public class RedirectModule : IHttpModule
{
    public void Dispose()
    {
    }

    public void Init(HttpApplication context)
    {
        context.MapRequestHandler += Context_MapRequestHandler;
    }

    private void Context_MapRequestHandler(object sender, EventArgs e)
    {
        //if we get a 404 error, check if a redirect exists in table
        if(HttpContext.Current.Response.StatusCode == 404)
        {
            string currentUrl = HttpUtility.UrlEncode(HttpContext.Current.Request.Url.ToString());
            int timeout = int.Parse(ConfigurationManager.AppSettings["RedirectProcessorTimeout"]);

            HttpClientHandler httpClientHandler = new HttpClientHandler();
            httpClientHandler.AllowAutoRedirect = false;

            //client config
            HttpClient client = new HttpClient(httpClientHandler);
            client.Timeout = new TimeSpan(hours: 0, minutes: 0, seconds: timeout);

            //request config
            HttpRequestMessage request = new HttpRequestMessage();
            request.Method = HttpMethod.Get;
            request.Headers.Add("X-gbi-key", ConfigurationManager.AppSettings["xGbiKey"]);

            //add original headers to new request
            NameValueCollection headers = HttpContext.Current.Request.Headers;
            foreach(string header in headers.AllKeys)
            {
                if(header.Equals("host", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                request.Headers.Add(header, headers[header]);
            }
            
            //get dispacther urls for random access
            List<string> dispatcherUrls = ConfigurationManager.AppSettings["RedirectProcessorUrls"].Split(';').ToList();
            int urlCount = dispatcherUrls.Count;
            while(urlCount > 0)
            {
                //get a random redirect processor url
                Random rnd = new Random();
                int pos = rnd.Next(urlCount);
                string processorUrl = dispatcherUrls[pos];
                dispatcherUrls.RemoveAt(pos);

                // build request url
                string url = string.Format("{0}?r={1}", processorUrl, currentUrl);
                request.RequestUri = new Uri(url);

                //send request
                HttpResponseMessage response = client.SendAsync(request).Result;

                //check status code
                int statusCode = (int)response.StatusCode;
                if(statusCode >= 300 && statusCode < 400)
                {
                    string redirectUrl = response.Headers.Location.AbsoluteUri;
                    HttpContext.Current.Response.Redirect(redirectUrl);
                }
                else if(statusCode >= 400 && statusCode < 500)
                {
                    return;
                }

                urlCount--;
            }
        }
    }
}