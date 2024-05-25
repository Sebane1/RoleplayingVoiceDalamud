
using Newtonsoft.Json;
using System;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RoleplayingSpeechDalamud.GPT_API
{
    public class GPTRequestSender
    {
        public async Task<string> GetGPTResponse(string sender, string message, string personality, bool local)
        {
            var httpWebRequest = (HttpWebRequest)WebRequest.Create(local ?
                "http://localhost:5000/api/v1/chat" :
                "https://ai.hubujubu.com:5696");
            httpWebRequest.ContentType = "application/json";
            ////if (!local)
            ////{
            ////    httpWebRequest.Headers.Add("Authorization: Bearer sk-w6u9o5Mz8GaPj7G31pYtUXVZBFc9sTsoBjTTfKDPK7SGkZFG");
            ////}
            httpWebRequest.Method = "POST";

            try
            {
                using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
                {
                    string json = JsonConvert.SerializeObject(new GPTRequest(sender, message));
                    streamWriter.Write(json);
                }
                var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    var result = streamReader.ReadToEnd();
                    var response = JsonConvert.DeserializeObject<GPTOpenAIResult>(result);
                    return ResponseCleaner(response.choices[0].text);
                }
            }
            catch
            {
                return await GetGPTResponse(sender, message, personality, local);
            }
        }

        public async Task<string> GetGPTResponse(string sender, string message, string personality)
        {
            string value = "";
            using (ClientWebSocket websocket = new ClientWebSocket())
            {
                await websocket.ConnectAsync(new System.Uri("ws://localhost:5005/api/v1/stream"), default);
                string json = JsonConvert.SerializeObject(new GPTRequest(sender, message));
                await SendString(websocket, json, default);
                while (true)
                {
                    GPTStreamResponse response = JsonConvert.DeserializeObject<GPTStreamResponse>(await ReadString(websocket));
                    if (response.@event == "text_stream")
                    {
                        value += response.text;
                    }
                    else if (response.@event == "stream_end")
                    {
                        break;
                    }
                }
                websocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "closing", default);
            }
            return ResponseCleaner(value).Replace("You", personality);
        }
        public string ResponseCleaner(string response)
        {
            string[] values = response.Split(". ");
            string newValue = values[0];
            for (int i = 1; i < values.Length; i++)
            {
                if (i < values.Length - 1 || (values[i].EndsWith(".") || values[i].EndsWith("\"")))
                {
                    {
                        newValue += ". " + values[i];
                    }
                }
            }
            return newValue.TrimStart('\n').Split("\n")[0].Split("]")[0].Replace("----", @"shakes ""Appologies, I forget myself sometimes""");
        }
        public static Task SendString(ClientWebSocket ws, String data, CancellationToken cancellation)
        {
            var encoded = Encoding.UTF8.GetBytes(data);
            var buffer = new ArraySegment<Byte>(encoded, 0, encoded.Length);
            return ws.SendAsync(buffer, WebSocketMessageType.Text, true, cancellation);
        }

        public static async Task<String> ReadString(ClientWebSocket ws)
        {
            ArraySegment<Byte> buffer = new ArraySegment<byte>(new Byte[8192]);

            WebSocketReceiveResult result = null;

            using (var ms = new MemoryStream())
            {
                do
                {
                    result = await ws.ReceiveAsync(buffer, CancellationToken.None);
                    ms.Write(buffer.Array, buffer.Offset, result.Count);
                }
                while (!result.EndOfMessage);

                ms.Seek(0, SeekOrigin.Begin);

                using (var reader = new StreamReader(ms, Encoding.UTF8))
                    return reader.ReadToEnd();
            }
        }
    }
}
