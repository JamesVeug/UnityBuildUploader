using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace Wireframe
{
    public readonly struct RequestResult
    {
        public readonly bool IsSuccessful;
        public readonly string Data;
        public readonly byte[] Bytes;
            
        private RequestResult(bool isSuccessful, string data, byte[] bytes)
        {
            IsSuccessful = isSuccessful;
            Data = data;
            Bytes = bytes;
        }
            
        public static RequestResult Successful(string text, byte[] bytes)
        {
            return new RequestResult(true, text, bytes);
        }
            
        public static RequestResult Failed(string reason)
        {
            return new RequestResult(false, reason, null);
        }
    }
    
    /// <summary>
    /// A wrapper for sending web requests from Unity so
    /// - Sending a request via different unity versions has 1 entry point
    /// - Any sensitive information is hidden (tokens, secrets... etc)
    /// - Any requests sent a logged to the task report to assist with debugging 
    /// </summary>
    public class RequestWrapper : IDisposable
    {
        public const string MaskedSecret = "xxx-xxx-xxx";

        private UnityWebRequest www;
        private string m_maskedURL;

        /// <summary>
        /// maskedURL is written to the task report instead of url, so it must not contain secrets. Pass url when it has none.
        /// </summary>
        public RequestWrapper(string url, string maskedURL, string method)
        {
            if (maskedURL == null)
            {
                throw new ArgumentNullException(nameof(maskedURL), "Pass the url itself when it holds no secrets.");
            }

            www = new UnityWebRequest(url, method);
            www.downloadHandler = new DownloadHandlerBuffer();
            m_maskedURL = maskedURL;
        }
        
        ~RequestWrapper()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (www != null)
            {
                www.Dispose();
            }
        }

        public async Task<RequestResult> SendAsync(UploadTaskReport.StepResult result, bool updateProgress = false)
        {
            result?.AddLog("Sending request: " + www.method + " " + m_maskedURL);
            
            Stopwatch stopwatch = new Stopwatch();
            UnityWebRequestAsyncOperation operation = www.SendWebRequest();
            while (!www.isDone)
            {
                if (updateProgress)
                {
                    result?.SetPercentComplete(operation.progress);
                }
                await Task.Yield();
            }
            stopwatch.Stop();
            string duration = $"{stopwatch.ElapsedMilliseconds}ms";
            
            if (!IsSuccessful())
            {
                string downloadHandlerText = www.downloadHandler.text;
                string error = $"Request Failed: {www.responseCode} - {downloadHandlerText} over {duration}";
                result?.AddError(error);
                return RequestResult.Failed(error);
            }
            
            result?.AddLog($"Request successful: {www.method} {m_maskedURL} - responseCode = {www.responseCode} over {duration}");
            return RequestResult.Successful(www.downloadHandler.text, www.downloadHandler.data);
        }

        public static RequestWrapper Post(string url, string maskedURL)
        {
            return new RequestWrapper(url, maskedURL, "POST");
        }

        public static RequestWrapper Get(string url, string maskedURL)
        {
            return new RequestWrapper(url, maskedURL, "GET");
        }

        public static RequestWrapper Delete(string url, string maskedURL)
        {
            return new RequestWrapper(url, maskedURL, "DELETE");
        }

        public static RequestWrapper Patch(string url, string maskedURL)
        {
            return new RequestWrapper(url, maskedURL, "PATCH");
        }

        public static RequestWrapper Put(string url, string maskedURL)
        {
            return new RequestWrapper(url, maskedURL, "PUT");
        }

        /// <summary>
        /// Replaces every query value, for signed or keyed URLs. eg: https://a.com/b?key=xxx-xxx-xxx
        /// </summary>
        public static string MaskQuery(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return url;
            }

            int queryStart = url.IndexOf('?');
            if (queryStart < 0)
            {
                return url;
            }

            string[] parameters = url.Substring(queryStart + 1).Split('&');
            for (int i = 0; i < parameters.Length; i++)
            {
                int equals = parameters[i].IndexOf('=');
                parameters[i] = (equals < 0 ? parameters[i] : parameters[i].Substring(0, equals)) + "=" + MaskedSecret;
            }

            return url.Substring(0, queryStart + 1) + string.Join("&", parameters);
        }

        /// <summary>
        /// Keeps only the scheme and host, for webhook URLs whose path is the secret. eg: https://a.com/xxx-xxx-xxx
        /// </summary>
        public static string MaskPathAndQuery(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return url;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            {
                return MaskedSecret;
            }

            return uri.GetLeftPart(UriPartial.Authority) + "/" + MaskedSecret;
        }

        public void SetRequestHeader(string key, string value)
        {
            www.SetRequestHeader(key, value);
        }

        public void SetHeaders(List<Tuple<string, string>> headers)
        {
            foreach (Tuple<string, string> header in headers)
            {
                www.SetRequestHeader(header.Item1, header.Item2);
            }
        }

        public bool IsSuccessful()
        {
#if UNITY_2022_3_OR_NEWER
            return www.result == UnityWebRequest.Result.Success;
#else
            return !(www.isHttpError || www.isNetworkError);
#endif
        }
        
        public void SetJSONData(string json)
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            
            SetRequestHeader("Content-Type", "application/json");
        }

        public void SetJSONData(object data)
        {
            SetJSONData(JSON.SerializeObject(data));
        }

        public void SetOctetStreamData(byte[] data)
        {
            www.uploadHandler = new UploadHandlerRaw(data);
            www.SetRequestHeader("Content-Type", "application/octet-stream");
        }

        public void SetFormData(string formBody)
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(formBody);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");
        }
    }
}