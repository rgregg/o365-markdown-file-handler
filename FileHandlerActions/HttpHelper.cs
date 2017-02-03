﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace FileHandlerActions
{
    public class HttpHelper
    {
        private const int MAX_UPLOAD_SIZE = 4 * 1024 * 1024;
        private HttpClient httpClient = new HttpClient();

        public static readonly HttpHelper Default = new HttpHelper();

        public async Task<Microsoft.Graph.DriveItem> UploadFileFromStreamAsync(Stream fileStream, string baseUrl, Microsoft.Graph.ItemReference folder, string filename, string accessToken)
        {
            if (fileStream.Length > MAX_UPLOAD_SIZE)
            {
                throw new Exception("File stream is longer than allowed for simple PUT upload action.");
            }

            var requestUrl = $"{baseUrl}/drives/{folder.DriveId}/items/{folder.Id}:/{filename}:/content";
            return await PutFileStreamToUrlAsync(fileStream, accessToken, requestUrl);
        }

        public async Task PatchItemMetadataAsync(object patchBody, string itemUrl, string accessToken)
        {
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, itemUrl);
            requestMessage.Headers.TryAddWithoutValidation("X-HTTP-Method", "PATCH");

            if (!string.IsNullOrEmpty(accessToken))
            {
                requestMessage.Headers.TryAddWithoutValidation("Authorization", "Bearer " + accessToken);
            }

            var contentText = Newtonsoft.Json.JsonConvert.SerializeObject(patchBody);
            var content = new StringContent(contentText, System.Text.Encoding.UTF8, "application/json");
            requestMessage.Content = content;

            var responseMessage = await httpClient.SendAsync(requestMessage);
            responseMessage.EnsureSuccessStatusCode();
        }

        public async Task<bool> UploadFileContentsFromStreamAsync(Stream fileStream, string itemUrl, string accessToken)
        {
            if (fileStream.Length > MAX_UPLOAD_SIZE)
            {
                throw new Exception("File stream is longer than allowed for simple PUT upload action.");
            }

            var item = await GetMetadataForUrlAsync<Microsoft.Graph.DriveItem>(itemUrl, accessToken);

            var baseUrl = ActionHelpers.ParseBaseUrl(itemUrl);
            var contentUrl = ActionHelpers.BuildApiUrl(baseUrl, item.ParentReference.DriveId, item.Id, "content");

            item = await PutFileStreamToUrlAsync(fileStream, accessToken, contentUrl);
            if (item != null && !string.IsNullOrEmpty(item.Id))
                return true;

            throw new Exception("Save failed.");

        }

        private async Task<Microsoft.Graph.DriveItem> PutFileStreamToUrlAsync(Stream fileStream, string accessToken, string contentUrl)
        {
            var requestMessage = new HttpRequestMessage(HttpMethod.Put, contentUrl);

            if (!string.IsNullOrEmpty(accessToken))
            {
                requestMessage.Headers.TryAddWithoutValidation("Authorization", "Bearer " + accessToken);
            }

            var content = new StreamContent(fileStream);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            requestMessage.Content = content;

            var responseMessage = await httpClient.SendAsync(requestMessage);
            responseMessage.EnsureSuccessStatusCode();

            return await ParseJsonFromResponseAsync<Microsoft.Graph.DriveItem>(responseMessage);
        }

        private async Task<T> ParseJsonFromResponseAsync<T>(HttpResponseMessage response)
        {
            if (response.Content.Headers.ContentType.MediaType.ToLower() != "application/json")
            {
                throw new InvalidOperationException($"MediaType for the response message was {response.Content.Headers.ContentType.MediaType} instead of \"application/json\".");
            }

            var responseData = await response.Content.ReadAsStringAsync();
            return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(responseData);
        }

        public async Task<T> GetMetadataForUrlAsync<T>(string requestUri, string accessToken)
        {
            var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri);

            if (!string.IsNullOrEmpty(accessToken))
            {
                requestMessage.Headers.TryAddWithoutValidation("Authorization", "Bearer " + accessToken);
            }

            var responseMessage = await httpClient.SendAsync(requestMessage);

            responseMessage.EnsureSuccessStatusCode();
            return await ParseJsonFromResponseAsync<T>(responseMessage);
        }

        public async Task<FileData> GetStreamContentForItemUrlAsync(string itemUrl, string accessToken)
        {
            var item = await GetMetadataForUrlAsync<Microsoft.Graph.DriveItem>(itemUrl, accessToken);
            var baseUrl = ActionHelpers.ParseBaseUrl(itemUrl);
            var contentUrl = ActionHelpers.BuildApiUrl(baseUrl, item.ParentReference.DriveId, item.Id, "content");
            var stream = await GetStreamContentForUrlAsync(contentUrl, accessToken);


            return new FileData { ContentStream = stream, Filename = item.Name };
        }

        public async Task<Stream> GetStreamContentForUrlAsync(string requestUri, string accessToken)
        {
            var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri);

            if (!string.IsNullOrEmpty(accessToken))
            {
                requestMessage.Headers.TryAddWithoutValidation("Authorization", "Bearer " + accessToken);
            }

            var responseMessage = await httpClient.SendAsync(requestMessage);
            responseMessage.EnsureSuccessStatusCode();

            MemoryStream ms = new MemoryStream();
            var responseStream = await responseMessage.Content.ReadAsStreamAsync();
            await responseStream.CopyToAsync(ms);
            ms.Seek(0, SeekOrigin.Begin);
            return ms;
        }

        public async Task<string> GetSharingLinkAsync(string baseUrl)
        {
            throw new NotImplementedException();
        }


    }

    public class FileData
    {
        public Stream ContentStream { get; set; }

        public string Filename { get; set; }
    }
}
