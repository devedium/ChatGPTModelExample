using AI.Dev.OpenAI.GPT;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using System.Text;

namespace ChatGPTModelExample
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var config = new ConfigurationBuilder()
               .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("env") ?? "dev"}.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var apiKey = config.GetSection("apiKey").Value;

            using var httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri("https://api.openai.com");
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            string apiUrl = "/v1/chat/completions";

            JArray conversation = new JArray
            {
                new JObject
                {
                    ["role"] = "system",
                    ["content"] = "Assistant is a large language model trained by OpenAI."
                }
            };

            int maxResponseTokens = 250;
            int tokenLimit = 4096;

            JObject input = new JObject
            {
                ["model"] = "gpt-3.5-turbo",
                ["messages"] = conversation,
                ["max_tokens"] = maxResponseTokens
            };
            

            while (true)
            {
                StringBuilder userInput = new StringBuilder();
                string? line;

                while ((line = Console.ReadLine()) != string.Empty)
                {
                    userInput.AppendLine(line);
                }

                conversation.Add(new JObject 
                {
                    ["role"] = "user",
                    ["content"] = userInput.ToString()
                });

                var tokenNum = GetConversationTokenNum(conversation);
                while (tokenNum + maxResponseTokens > tokenLimit)
                {
                    conversation.RemoveAt(1);
                    tokenNum = GetConversationTokenNum(conversation);
                }

                HttpContent content = new StringContent(input.ToString(), Encoding.UTF8, "application/json");
                HttpResponseMessage response = await httpClient.PostAsync(apiUrl, content);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"API call failed with status code {response.StatusCode}: {response.ReasonPhrase}");
                    return;
                }

                string responseContent = await response.Content.ReadAsStringAsync();
                JObject jsonResponse = JObject.Parse(responseContent);
                var responseMessage = jsonResponse["choices"][0]["message"]["content"].ToString();
                Console.WriteLine(responseMessage);
                Console.WriteLine();
                conversation.Add(new JObject
                {
                    ["role"] = "assistant",
                    ["content"] = responseMessage
                });
                if (jsonResponse["choices"][0]["finish_reason"].ToString() == "length")
                {
                    Console.WriteLine("[...]");
                    Console.WriteLine();
                }
            }
        }

        static int GetConversationTokenNum(JArray conversation)
        {
            int num = 0;
            num = conversation.Sum(m=> GPT3Tokenizer.Encode(m["role"].ToString()).Count + GPT3Tokenizer.Encode(m["content"].ToString()).Count + 4);
            return num + 2;
        }
    }
}