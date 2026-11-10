using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Wireframe
{
    /// <summary>
    /// Draws a preview of an image URL in editor GUI. Each URL downloads once and stays cached for the session.
    /// </summary>
    public static class WebImagePreview
    {
        public static readonly string[] SupportedExtensions = { ".png", ".jpg", ".jpeg", ".gif", ".webp" };

        private const double DownloadDelaySeconds = 0.5;
        private const double FailedRetrySeconds = 30;
        private const int MaxCachedImages = 32;

        private static Dictionary<string, CachedImage> s_cache = new Dictionary<string, CachedImage>();
        private static bool s_registeredReloadCleanup;

        private enum ImageState
        {
            Waiting,
            Loading,
            Loaded,
            Failed
        }

        private class CachedImage
        {
            public ImageState State;
            public double FirstSeen;
            public double LastUsed;
            public double FinishedAt;
            public Texture2D Texture;
            public string Message;
        }

        /// <summary>
        /// True for an http or https URL whose path ends in one of the SupportedExtensions.
        /// </summary>
        public static bool IsImageURL(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return false;
            }

            return SupportedExtensions.Contains(GetExtension(uri));
        }

        /// <summary>
        /// The reason the cached download of this URL failed, if it has finished and failed.
        /// </summary>
        public static bool TryGetDownloadProblem(string url, out string problem)
        {
            if (!string.IsNullOrEmpty(url) && s_cache.TryGetValue(url, out CachedImage image) && image.State == ImageState.Failed)
            {
                problem = image.Message;
                return true;
            }

            problem = null;
            return false;
        }

        /// <summary>
        /// Draws the image, or why there is none, on its own row. Draws nothing for an empty URL.
        /// </summary>
        public static void Draw(string url, float indent = 0, float size = 64)
        {
            if (string.IsNullOrEmpty(url))
            {
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(indent);
                if (!IsImageURL(url))
                {
                    EditorGUILayout.HelpBox($"Must be an http:// or https:// link ending in {string.Join(", ", SupportedExtensions)}.", MessageType.Error);
                    return;
                }

                CachedImage image = GetImage(url);
                if (image.State == ImageState.Waiting || image.State == ImageState.Loading)
                {
                    EditorGUILayout.HelpBox("Loading image preview...", MessageType.None);
                }
                else if (image.State == ImageState.Failed)
                {
                    EditorGUILayout.HelpBox(image.Message, MessageType.Warning);
                }
                else if (image.Texture != null)
                {
                    Rect rect = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
                    GUI.DrawTexture(rect, image.Texture, ScaleMode.ScaleToFit);
                    GUILayout.Label(new GUIContent(image.Message), EditorStyles.miniLabel);
                    GUILayout.FlexibleSpace();
                }
                else
                {
                    EditorGUILayout.HelpBox(image.Message, MessageType.Info);
                }
            }
        }

        /// <summary>
        /// Forgets every cached image so the next draw downloads again.
        /// </summary>
        public static void ClearCache()
        {
            foreach (CachedImage image in s_cache.Values)
            {
                DestroyTexture(image);
            }

            s_cache.Clear();
        }

        private static string GetExtension(Uri uri)
        {
            return Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();
        }

        private static CachedImage GetImage(string url)
        {
            if (!s_registeredReloadCleanup)
            {
                s_registeredReloadCleanup = true;
                AssemblyReloadEvents.beforeAssemblyReload += ClearCache;
            }

            double now = EditorApplication.timeSinceStartup;
            if (!s_cache.TryGetValue(url, out CachedImage image))
            {
                EvictLeastRecentlyUsed();
                image = new CachedImage { State = ImageState.Waiting, FirstSeen = now };
                s_cache[url] = image;
                RepaintAfterDelay();
            }

            image.LastUsed = now;

            // Waits for typing to settle before the first download, and retries failures only after a while.
            bool waitedLongEnough = image.State == ImageState.Waiting && now - image.FirstSeen >= DownloadDelaySeconds;
            bool canRetry = image.State == ImageState.Failed && now - image.FinishedAt >= FailedRetrySeconds;
            if (waitedLongEnough || canRetry)
            {
                image.State = ImageState.Loading;
                Download(url, image);
            }

            return image;
        }

        private static void EvictLeastRecentlyUsed()
        {
            while (s_cache.Count >= MaxCachedImages)
            {
                string oldestURL = null;
                double oldestUse = double.MaxValue;
                foreach (KeyValuePair<string, CachedImage> pair in s_cache)
                {
                    if (pair.Value.State != ImageState.Loading && pair.Value.LastUsed < oldestUse)
                    {
                        oldestURL = pair.Key;
                        oldestUse = pair.Value.LastUsed;
                    }
                }

                if (oldestURL == null)
                {
                    return;
                }

                DestroyTexture(s_cache[oldestURL]);
                s_cache.Remove(oldestURL);
            }
        }

        private static async void RepaintAfterDelay()
        {
            await Task.Delay(TimeSpan.FromSeconds(DownloadDelaySeconds));
            InternalEditorUtility.RepaintAllViews();
        }

        private static async void Download(string url, CachedImage image)
        {
            try
            {
                using (RequestWrapper www = RequestWrapper.Get(url, RequestWrapper.MaskQuery(url)))
                {
                    RequestResult result = await www.SendAsync(null);
                    if (!s_cache.TryGetValue(url, out CachedImage current) || current != image)
                    {
                        return;
                    }

                    if (!result.IsSuccessful)
                    {
                        Fail(image, "Could not reach the image: " + result.Data);
                        return;
                    }

                    string extension = GetExtension(new Uri(url));
                    Texture2D texture = new Texture2D(2, 2);
                    texture.hideFlags = HideFlags.HideAndDontSave;
                    if (!texture.LoadImage(result.Bytes))
                    {
                        UnityEngine.Object.DestroyImmediate(texture);
                        Succeed(image, null, $"Image is reachable, but Unity can't preview {extension} files.");
                        return;
                    }

                    Succeed(image, texture, $"{texture.width}x{texture.height}");
                }
            }
            catch (Exception e)
            {
                Fail(image, "Could not reach the image: " + e.Message);
            }
            finally
            {
                InternalEditorUtility.RepaintAllViews();
            }
        }

        private static void Succeed(CachedImage image, Texture2D texture, string message)
        {
            DestroyTexture(image);
            image.State = ImageState.Loaded;
            image.Texture = texture;
            image.Message = message;
            image.FinishedAt = EditorApplication.timeSinceStartup;
        }

        private static void Fail(CachedImage image, string message)
        {
            DestroyTexture(image);
            image.State = ImageState.Failed;
            image.Message = message;
            image.FinishedAt = EditorApplication.timeSinceStartup;
        }

        private static void DestroyTexture(CachedImage image)
        {
            if (image.Texture != null)
            {
                UnityEngine.Object.DestroyImmediate(image.Texture);
            }

            image.Texture = null;
        }
    }
}
