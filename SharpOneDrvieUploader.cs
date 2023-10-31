using System;
using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Web.Script.Serialization;

namespace SharpOneDriveUploader
{
    internal class Program
    {
        static string PDriveID = string.Empty;
        static string client_id = string.Empty;
        static string client_secret = string.Empty;
        static string refreshToken = string.Empty;

        static string PAccessToken = string.Empty;

        private static IWebProxy proxyHost = null;
		
        class UploadSessionInfo
        {
            public string uploadUrl { get; set; }
        }
		
        static void  Main(string[] args)
        {

            /// 根据官方指导 https://learn.microsoft.com/zh-cn/onedrive/developer/rest-api/api/driveitem_createuploadsession?view=odsp-graph-online
            /// 1. 获取accessToken
            /// 2. 获取upload url
            /// 3. 通过upload url put文件，文件测试560MB可正常上传
            /// 4. delete upload url完成上传
            /// 
            if (args.Length == 5)
            {
                refreshToken = args[0];
                client_id = args[1];
                client_secret = args[2];
                string localName = args[3];
                string cloudName = args[4];
                RefreshAccessToken();
                Send(localName,cloudName);
            }
            else
            {
                Console.WriteLine("\r\nUsage:\r\n\r\n  SharpOneDriveUploader.exe <refreshtoken> <Client_id> <Client_SECRET> <localName> <cloudName>");
            }
        }

        private static void Send(string localName, string cloudName)
        {
            string uploadUrl = GetUploadUrl(cloudName);
            if (uploadUrl != null)
            {
                SendFile(uploadUrl, localName);
                DeleteUploadSession(uploadUrl);
            }
        }
        private static void DeleteUploadSession(string uploadUrl)
        {
            // 完成上传并删除上传会话
            string deleteUrl = uploadUrl;
            HttpWebRequest deleteRequest = (HttpWebRequest)WebRequest.Create(deleteUrl);
            deleteRequest.Method = "DELETE";
            deleteRequest.Headers.Add("Authorization", "Bearer " + PAccessToken);

            HttpWebResponse deleteResponse = (HttpWebResponse)deleteRequest.GetResponse();
            if (deleteResponse.StatusCode == HttpStatusCode.NoContent)
            {
                Console.WriteLine("Upload session deleted successfully.");
            }
            else
            {
                Console.WriteLine($"Failed to delete upload session: {deleteResponse.StatusDescription}");
            }
        }

        public static string SendFile(string uploadURL, string filePathOrContent)
        {
            string SendResult = string.Empty;


            string url = uploadURL;
            Console.WriteLine(url);
            Console.WriteLine("PAccessToken：--> " + PAccessToken);

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "PUT";
            request.Headers["Authorization"] = "Bearer " + PAccessToken;
            request.Timeout = 120 * 1000;

            byte[] byteContent;
            if (File.Exists(filePathOrContent))
            {
                byteContent = File.ReadAllBytes(filePathOrContent);
            }
            else
            {
                byteContent = Encoding.Default.GetBytes(filePathOrContent);
            }

            request.ContentLength = byteContent.Length;

            try
            {
                using (Stream stream = request.GetRequestStream())
                {
                    stream.Write(byteContent, 0, byteContent.Length);
                }

                using (HttpWebResponse responsex = (HttpWebResponse)request.GetResponse())
                {
                    //Console.WriteLine( responsex.StatusCode);
                    using (StreamReader reader = new StreamReader(responsex.GetResponseStream()))
                    {
                    }
                }
            }
            catch (Exception ex)
            {                
                SendResult = ex.Message;
                Console.WriteLine($"Error: {SendResult}");
            }
            return SendResult;
        }

        public static string RefreshAccessToken()
        {
            string rrr = string.Empty;

            HttpWebRequest request = HttpWebRequest.CreateHttp("https://login.microsoftonline.com/consumers/oauth2/v2.0/token");
            request.Method = "POST";
            request.Timeout = 10 * 1000;
            if (proxyHost != null) { request.Proxy = proxyHost; }

            byte[] postData = Encoding.Default.GetBytes($"refresh_token={refreshToken}&client_id={client_id}&client_secret={client_secret}&grant_type=refresh_token");
            try
            {
                using (Stream stream = request.GetRequestStream()) { stream.Write(postData, 0, postData.Length); }

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    // 在此处理响应。
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        using (StreamReader sr = new StreamReader(response.GetResponseStream()))
                        {
                            string[] accessArray = sr.ReadToEnd().Split(',')[4].Split(':')[1].Split('"'); // 从返回包的内容截取accessToken
                            foreach (string access in accessArray)
                            {
                                if (access.Length > 100)
                                {

                                    PAccessToken = access;
                                    break;
                                }
                            }
                            // Console.WriteLine(PAccessToken);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                rrr = ex.Message;
            }

            return rrr;
        }

        private static string GetUploadUrl(string couldName)
        {
            string uploadUrl = string.Empty;
            string ReqUrl = $"https://graph.microsoft.com/v1.0/me/drive/root:/{couldName}:/createUploadSession";
            WebClient webClient = new WebClient();

            webClient.Headers["Content-Type"] = "application/json";
            webClient.Headers.Add("Authorization", "Bearer " + PAccessToken);
            string body = $@"
    {{
    ""{couldName}"": {{
    ""@odata.type"": ""microsoft.graph.driveItemUploadableProperties"",
    ""@microsoft.graph.conflictBehavior"": ""replace"",
    }}
    }}
    ";
            try
            {
                byte[] responseArray = webClient.UploadData(couldName, Encoding.UTF8.GetBytes(body));
                string josnObject = Encoding.UTF8.GetString(responseArray);
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                uploadUrl = serializer.Deserialize<UploadSessionInfo>(josnObject).uploadUrl;
                
            }
            catch (WebException ex)
            {
                Console.WriteLine("[!] Exception: " + ex.Message);
            }
            // Console.WriteLine(uploadUrl);
            return uploadUrl;

        }

        /// <summary>
        /// 以下代码是根据官方文档中对大文件上传的指导编写的，但实测情况是把大文件分片后，上传第三个片段就会超时，故放弃，待有缘人测试成功，望告之。
        /// https://learn.microsoft.com/zh-cn/onedrive/developer/rest-api/api/driveitem_createuploadsession?view=odsp-graph-online
        /// </summary>
        private static async void uploadLarge()
        {
            string accessToken = PAccessToken;
            string uploadUrl = "https://graph.microsoft.com/v1.0/me/drive/root:/file12.txt:/createUploadSession";

            // Initialize HttpClient
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                // Create an upload session
                var sessionResponse = await client.PostAsync(uploadUrl, null);
                if (sessionResponse.IsSuccessStatusCode)
                {
                    string sessionResponseContent = await sessionResponse.Content.ReadAsStringAsync();
                    JavaScriptSerializer Serializer = new JavaScriptSerializer();
                    var sessionInfo = Serializer.Deserialize<UploadSessionInfo>(sessionResponseContent);



                    
                     // Initialize the FileStream
                    using (FileStream fileStream = new FileStream("Symantec_Encryption_Desktop_10.5.1_MP1_Windows.7z", FileMode.Open, FileAccess.Read))
                    {
                        long fileSize = fileStream.Length;

                        // Initialize the HttpClient for uploading
                        using (HttpClient uploadClient = new HttpClient())
                        {
                            uploadClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                            // Upload in chunks
                            long chunkSize = 327680; // 320 KB
                            byte[] buffer = new byte[chunkSize];
                            long offset = 0;

                            while (offset < fileSize)
                            {
                                long bytesToRead = Math.Min(chunkSize, fileSize - offset);
                                fileStream.Read(buffer, 0, (int)bytesToRead);

                                using (var byteContent = new ByteArrayContent(buffer))
                                {
                                    byteContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                                    byteContent.Headers.ContentRange = new ContentRangeHeaderValue(
                                        offset, offset + bytesToRead - 1, fileSize);

                                    string uploadRange = $"bytes {offset}-{offset + bytesToRead - 1}/{fileSize}";


                                    HttpWebRequest uploadRequest = (HttpWebRequest)WebRequest.Create(sessionInfo.uploadUrl); // 通过uploadURL初始化一个HttpWebRequest
                                    uploadRequest.Method = "PUT";
                                    uploadRequest.Headers.Add("Authorization", "Bearer " + accessToken);
                                    uploadRequest.Headers.Add("Content-Range", uploadRange);

                                    using (Stream requestStream = uploadRequest.GetRequestStream())
                                    {
                                        requestStream.Write(buffer, 0, (int)bytesToRead);
                                    }

                                    HttpWebResponse uploadResponse = (HttpWebResponse)uploadRequest.GetResponse();

                                    if (uploadResponse.StatusCode == HttpStatusCode.Created || uploadResponse.StatusCode == HttpStatusCode.OK || uploadResponse.StatusCode == HttpStatusCode.Accepted)
                                    {
                                        offset += bytesToRead;
                                        Console.WriteLine("Upload ", bytesToRead);
                                    }
                                    else
                                    {
                                        Console.WriteLine($"Upload failed: {uploadResponse.StatusDescription}");
                                        break;
                                    }
                                }
                            }

                            if (offset == fileSize)
                            {
                                Console.WriteLine("File uploaded successfully.");

                                // 完成上传并删除上传会话
                                string deleteUrl = sessionInfo.uploadUrl;
                                HttpWebRequest deleteRequest = (HttpWebRequest)WebRequest.Create(deleteUrl);
                                deleteRequest.Method = "DELETE";
                                deleteRequest.Headers.Add("Authorization", "Bearer " + accessToken);

                                HttpWebResponse deleteResponse = (HttpWebResponse)deleteRequest.GetResponse();
                                if (deleteResponse.StatusCode == HttpStatusCode.NoContent)
                                {
                                    Console.WriteLine("Upload session deleted successfully.");
                                }
                                else
                                {
                                    Console.WriteLine($"Failed to delete upload session: {deleteResponse.StatusDescription}");
                                }
                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"Failed to create an upload session: {sessionResponse.StatusCode}");
                }
            }
        }
    }
}