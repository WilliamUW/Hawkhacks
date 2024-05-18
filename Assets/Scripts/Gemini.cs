using System.Collections;
using UnityEngine;
using UnityEngine.UI; // Required for UI Button interaction
using TMPro; // Required for TextMeshPro interaction
using System;
using static UnityEngine.Rendering.DebugUI;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Xml;
using System.Threading.Tasks;
using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Specialized;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
using Newtonsoft.Json;
using Meta.Voice.Samples.Dictation;
using System.Reflection;
using Button = UnityEngine.UI.Button;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
public class Gemini
{
    MethodInfo onClickMethod = typeof(Button).GetMethod("Press", BindingFlags.NonPublic | BindingFlags.Instance);

    public UnityEngine.UI.InputField textToSpeechInputTextField;
    public Button textToSpeechStartButton; // Reference to the UI Button
    public Button textToSpeechStopButton; // Reference to the UI Button

    private List<Dictionary<string, object>> conversation = new List<Dictionary<string, object>>();
    private string geminiApiKey = "AIzaSyBxjY0ZtQ3Rw4xedwZIrCscne2PZxagCmc";
    private string url = "https://generativelanguage.googleapis.com/v1beta/models/gemini-pro:generateContent";

    public Gemini(string initialPrompt, UnityEngine.UI.InputField textToSpeechInputTextField, Button textToSpeechStartButton)
    {
        this.textToSpeechInputTextField = textToSpeechInputTextField;
        this.textToSpeechStartButton = textToSpeechStartButton;

        speak("Hi there! Let me introduce myself.");

        conversation = new List<Dictionary<string, object>>();
        conversation.Add(new Dictionary<string, object>
                {
                    { "role", "user" },
                    { "parts", new List<object>
                        {
                            new { text = "Answer in first person as if you are without any prefix: " + initialPrompt },
                        }
                    }
                });
        conversation.Add(new Dictionary<string, object>
                {
                    { "role", "model" },
                    { "parts", new List<object>
                        {
                            new { text = "Ok." },
                        }
                    }
                });

    }

    public void speak(string text)
    {
        Debug.Log("Speak: " + text);
        textToSpeechInputTextField.text = text;
        onClickMethod?.Invoke(textToSpeechStartButton, null);
    }

    public void updateCaptureButtonText(string s)
    {
        Debug.Log(s);
    }
    public async void AskGemini(string userQuery, bool resetConversation = false, bool announceQuestion = true)
    {
        if (string.IsNullOrWhiteSpace(userQuery))
        {
            return;
        }
        if (announceQuestion)
        {
            speak("I heard: " + userQuery);
            // updateCaptureButtonText("I heard: " + userQuery);
        }
        var url = "https://generativelanguage.googleapis.com/v1beta/models/gemini-pro:generateContent";

        if (resetConversation)
        {
            conversation.Clear();
        }

        using (HttpClient client = new HttpClient())
        {
            client.BaseAddress = new Uri(url);
            var requestUri = $"?key={geminiApiKey}";

            // Append user response to the conversation history
            conversation.Add(new Dictionary<string, object>
                {
                    { "role", "user" },
                    { "parts", new List<object>
                        {
                            new { text = userQuery },
                        }
                    }
                });

            Debug.Log(JsonConvert.SerializeObject(conversation, Newtonsoft.Json.Formatting.Indented));
            var safetySettings = new List<object>
                {
                    new { category = "HARM_CATEGORY_HARASSMENT", threshold = "BLOCK_NONE" },
                    new { category = "HARM_CATEGORY_HATE_SPEECH", threshold = "BLOCK_NONE" },
                    new { category = "HARM_CATEGORY_SEXUALLY_EXPLICIT", threshold = "BLOCK_NONE" },
                    new { category = "HARM_CATEGORY_DANGEROUS_CONTENT", threshold = "BLOCK_NONE" }
                };



            // Existing objects: conversation and safetySettings

            var requestBody = new
            {
                contents = conversation,
                safetySettings = safetySettings,
            };
            Debug.Log(JsonConvert.SerializeObject(requestBody, Newtonsoft.Json.Formatting.Indented));

            // Serialize the payload
            var conversationJson = JsonConvert.SerializeObject(requestBody, Newtonsoft.Json.Formatting.None);
            HttpContent content = new StringContent(conversationJson, Encoding.UTF8, "application/json");

            try
            {
                HttpResponseMessage httpresponse = await client.PostAsync(requestUri, content);
                httpresponse.EnsureSuccessStatusCode();
                string responseBody = await httpresponse.Content.ReadAsStringAsync();
                Debug.Log(responseBody);

                JObject jsonResponse = JObject.Parse(responseBody);
                Debug.Log(jsonResponse);

                // string extractedText = (string)jsonResponse["candidates"][0]["content"]["parts"][0]["text"];

                // Attempt to extract text directly
                JToken jtokenResponse = jsonResponse["candidates"][0]["content"]["parts"][0]["text"];

                if (jtokenResponse != null)
                {
                    string extractedText = jtokenResponse.ToString();

                    Debug.Log("Extracted Text: " + extractedText);


                    // Parse and handle model response here if necessary
                    // Example: Append model response to the conversation
                    // This is a placeholder. You need to extract actual response from responseBody JSON.
                    conversation.Add(new Dictionary<string, object>
                    {
                        { "role", "model" },
                        { "parts", new List<object>
                            {
                                new { text = extractedText }
                            }
                        }
                    });

                    Debug.Log(JsonConvert.SerializeObject(conversation, Newtonsoft.Json.Formatting.Indented));

                    if (extractedText != null)
                    {
                        speak(extractedText);
                    }
                }
                else
                {
                    // Check if there is a function call
                    JToken functionCall = jsonResponse["candidates"][0]["content"]["parts"][0]["functionCall"];
                    if (functionCall != null)
                    {
                        string functionName = (string)functionCall["name"];
                        JObject args = (JObject)functionCall["args"];

                        // Based on function name, call the relevant method
                        Debug.Log($"Function Call Detected: {functionName}");
                        // ExecuteFunctionCall(functionName, args);
                        //if (functionName == "change_size")
                        //{
                        //    change_size(functionCall["args"]["magnitude"].ToString(), functionCall["args"]["body"].ToString());
                        //}
                    }
                    else
                    {
                        Debug.Log("No valid text or function call found in the response.");
                    }
                }

            }
            catch (HttpRequestException e)
            {
                Debug.Log("\nException Caught!");
                Debug.Log("Message :{0} " + e.Message);
            }
        }
    }
}