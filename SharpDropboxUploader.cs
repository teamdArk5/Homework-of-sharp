using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net;
using System.Text;
using System.IO;

namespace SharpDropboxUploader
{
    internal class Program
    {
        static void Main(string[] args)
        {
            /// 1. 通过Refresh Token获取AccessToken
            /// 2. 读取文件上传
            string refreshtoken = string.Empty;
            string Client_id = string.Empty;
            string Client_SECRET  = string.Empty;
            string localName = string.Empty;
            string cloudName = string.Empty;

            if (args.Length == 5)
            {
                refreshtoken = args[0];
                Client_id = args[1];
                Client_SECRET = args[2];
                localName = args[3];
                cloudName = args[4];

                string accessToken = refreshToken(refreshtoken, Client_id, Client_SECRET);
                Console.WriteLine(accessToken);
                sendFile(accessToken, localName, cloudName);
            }
            else
            {
                Console.WriteLine("\r\nUsage:\r\n\r\n  SharpDropboxUploader.exe <refreshtoken> <Client_id> <Client_SECRET> <localName> <cloudName>");
            }
            
            //string accessToken = "sl.Bo5xtVA1kSAbR_Ba7YFl1gaZBeUvEa_PntE1vgtLN_JcnyrTsC32Q1GOxk1p4LFQi2B6SfRG3dahrvl8_OdxgJa9-xTnDu0IwfIGk-4eO7-d85AVqWH861qUY6BEvBTPEWC0zZEzKowp";
            
            
        }

        /// <summary>
        /// 通过RefreshToken获取AccessToken
        /// </summary>
        /// <param name="refreshToken"></param>
        /// <param name="CLIENT_ID"></param>
        /// <param name="CLIENT_SECRET"></param>
        /// <param name="proxyHost"></param>
        /// <returns></returns>
        public static string refreshToken(string refreshToken = "pGE4QWl3fIkAAAAAAAAAAZVfBaF6oyk7WMGA7T-dMD6zZRmAhx8IX2A34nNog-vg", string CLIENT_ID = "4hdxalwxl0gxajo", string CLIENT_SECRET = "q39b3vddxjb6ek3", IWebProxy proxyHost = null)
        {
            string AccessToken = "";

            const string TOKEN_URL = "https://api.dropbox.com/oauth2/token";


            var parameters = new Dictionary<string, string>
            {
                { "grant_type", "refresh_token" },
                { "refresh_token", refreshToken },
                { "client_id", CLIENT_ID },
                { "client_secret", CLIENT_SECRET }
            };

            if (!string.IsNullOrEmpty(refreshToken))
            {


                var handler = new HttpClientHandler();
                if (proxyHost != null)
                {
                    handler.Proxy = proxyHost;
                    handler.UseProxy = true;
                }

                using (var client = new HttpClient(handler))
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, TOKEN_URL)
                    {
                        Content = new FormUrlEncodedContent(parameters)
                    };

                    var response = client.SendAsync(request).Result;

                    var content = response.Content.ReadAsStringAsync().Result;

                    if (response.IsSuccessStatusCode)
                    {
                        AccessToken = content.Split('"')[3];
                    }
                    else
                    {

                    }
                }
            }


            return AccessToken;
        }

        /// <summary>
        /// 发送内容到DropBox上
        /// </summary>
        /// <param name="accessToken"></param>
        /// <param name="filePath"></param>
        /// <param name="dstFileName"></param>
        /// <returns></returns>
        public static void sendFile(string accessToken, string filePath, string dstFileName, IWebProxy proxyHost = null)
        {
            byte[] byteContents;
            if (File.Exists(filePath))
            {
                if (IsTextFile(filePath))
                {
                    // Read text file, line by line
                    using (StreamReader reader = new StreamReader(filePath))
                    using (MemoryStream memoryStream = new MemoryStream())
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            byte[] lineBytes = Encoding.Default.GetBytes(line + Environment.NewLine);
                            memoryStream.Write(lineBytes, 0, lineBytes.Length);
                        }

                        byteContents = memoryStream.ToArray();
                    }
                }
                else
                {
                    // Read binary file, in chunks
                    const int bufferSize = 4096;
                    using (FileStream stream = File.OpenRead(filePath))
                    using (MemoryStream memoryStream = new MemoryStream())
                    {
                        byte[] buffer = new byte[bufferSize];
                        int bytesRead;
                        while ((bytesRead = stream.Read(buffer, 0, bufferSize)) > 0)
                        {
                            memoryStream.Write(buffer, 0, bytesRead);
                        }

                        byteContents = memoryStream.ToArray();
                    }
                }
            }
            else
            {
                byteContents = Encoding.Default.GetBytes(filePath);
            }


            var handler = new HttpClientHandler()
            {
                Proxy = proxyHost,
                UseProxy = true
            };

            // 配置请求头
            HttpClient httpClient = new HttpClient(); ;
            if (proxyHost != null)
            {
                httpClient = new HttpClient(handler);
            }

            httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + accessToken);
            httpClient.DefaultRequestHeaders.Add("Dropbox-API-Arg", "{\"autorename\":false,\"mode\":\"overwrite\",\"mute\":false,\"path\":\"/" + dstFileName + "\",\"strict_conflict\":false}");
            //httpClient.DefaultRequestHeaders.Add("Content-Type", "application/octet-stream");

            // 发送POST请求
            using (var content = new ByteArrayContent(byteContents))
            {
                content.Headers.TryAddWithoutValidation("Content-Type", "application/octet-stream");
                //var response = httpClient.PostAsync("https://content.dropboxapi.com/2/files/upload", content).Result;
                var response = httpClient.PostAsync("https://content.dropboxapi.com/2/files/upload", content).Result;
                //return response.Content.ReadAsStringAsync().Result;
                Console.WriteLine(response.Content.ReadAsStringAsync().Result);
            }
        }

        private static bool IsTextFile(string filePath)
        {
            // Determine if file is a text file based on its extension
            string ext = Path.GetExtension(filePath).ToLower();
            return ext == ".txt" || ext == ".csv" || ext == ".xml" || ext == ".json";
        }
    }
}
