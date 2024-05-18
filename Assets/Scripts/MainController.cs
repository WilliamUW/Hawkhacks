using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics; // For BigInteger
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Nethereum.Web3;
using Nethereum.Contracts;
using Nethereum.ABI.FunctionEncoding.Attributes;
using TMPro;
using UnityEngine.Networking;
using System.Net.Http;
using System.Numerics;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
public class MainController : MonoBehaviour
{
    private Web3 web3;
    private Contract contract;
    private string contractAddress = "0x71c943B5e10abD83290AB54C6DFC65Cba22B60F3";
    private string rpcUrl = "https://polygon-amoy.infura.io/v3/4764f6f6a4bb4aae8672c9d2627d0b05";

    public TextMeshProUGUI artworkText; // UI Text element to display artwork details
    public TMP_Dropdown artworkDropdown; // TMP Dropdown for selecting artworks
    public List<Canvas> canvasElements; // List of canvas elements
    private ArtworkRegistryService artworkRegistryService;
    private List<ArtworkRegistryService.ArtworkDTO> artworks;
    public Image artworkImage;

    private Gemini gemini;
    public UnityEngine.UI.InputField textToSpeechInputTextField;
    public Button textToSpeechStartButton; // Reference to the UI Button

    void Start()
    {
        artworkRegistryService = new ArtworkRegistryService(rpcUrl, contractAddress);
        artworks = new List<ArtworkRegistryService.ArtworkDTO>();
        StartCoroutine(InitializeArtworks());
    }

    public async void AskQuestion(string question)
    {
        gemini.AskGemini(question);
        // Debug.Log("Gemini Response: " + response);
    }

    private IEnumerator InitializeArtworks()
    {
        // Run the async method to fetch artworks
        var getArtworksTask = GetArtworksAsync();
        yield return new WaitUntil(() => getArtworksTask.IsCompleted);

        if (getArtworksTask.Exception != null)
        {
            Debug.LogError("Failed to fetch artworks: " + getArtworksTask.Exception);
            yield break;
        }

        // Start the coroutine to process artworks
        StartCoroutine(FetchArtworksCoroutine());
    }

    private async Task GetArtworksAsync()
    {
        try
        {
            await artworkRegistryService.GetArtworksAsync();
            artworks = artworkRegistryService.Artworks;
            Debug.Log("Artworks fetched successfully");
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Failed to fetch artworks: " + ex);
        }
    }


    IEnumerator FetchArtworksCoroutine()
    {
        var getArtworkCountTask = artworkRegistryService.GetArtworkCountAsync();
        yield return new WaitUntil(() => getArtworkCountTask.IsCompleted);

        if (getArtworkCountTask.Exception != null)
        {
            Debug.LogError("Error fetching artwork count: " + getArtworkCountTask.Exception);
            yield break;
        }

        BigInteger artworkCount = getArtworkCountTask.Result;
        artworkText.text = $"Total Artworks: {artworkCount}\n";
        Debug.Log($"Total Artworks: {artworkCount}");

        for (BigInteger i = 0; i < artworkCount; i++)
        {
            var getArtworkTask = artworkRegistryService.GetArtworkAsync(i);
            yield return new WaitUntil(() => getArtworkTask.IsCompleted);

            if (getArtworkTask.Exception != null)
            {
                Debug.LogError($"Error fetching artwork {i}: " + getArtworkTask.Exception);
                continue;
            }

            var artwork = getArtworkTask.Result;
            artworks.Add(artwork);
            Debug.Log($"Fetched Artwork {i}: {artwork.Name}");

            StartCoroutine(DownloadImage(artwork, (artworkObj, texture) =>
            {
                artworkObj.ImageTexture = texture;
                Debug.Log($"DownloadImage done for: {artworkObj.Name}");


                int index = artworks.IndexOf(artworkObj);
                if (index < canvasElements.Count)
                {
                    var imageComponent = canvasElements[index].GetComponentInChildren<Image>();
                    if (imageComponent != null)
                    {
                        Debug.Log("Setting image texture for canvas element: " + index);
                        imageComponent.sprite = Sprite.Create(artworkObj.ImageTexture, new Rect(0, 0, artworkObj.ImageTexture.width, artworkObj.ImageTexture.height), new UnityEngine.Vector2(0.5f, 0.5f));
                    }
                }
            }));
        }

        for (int i = 0; i < artworkCount; i++)
        {
            var artwork = artworks[i];
            artworkDropdown.options.Add(new TMP_Dropdown.OptionData(artwork.Name));
        }

        artworkDropdown.onValueChanged.AddListener(OnArtworkSelected);
    }

    IEnumerator DownloadImage(ArtworkRegistryService.ArtworkDTO artwork, Action<ArtworkRegistryService.ArtworkDTO, Texture2D> callback)
    {
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(artwork.ImageUrl))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Error downloading image: {request.error}");
                yield break;
            }

            Texture2D texture = DownloadHandlerTexture.GetContent(request);
            callback(artwork, texture);
        }
    }

    void OnArtworkSelected(int index)
    {
        if (index < 0 || index >= artworks.Count) return;

        var artwork = artworks[index];
        artworkText.text = $"Artwork {index}:\n";
        artworkText.text += $"  Name: {artwork.Name}\n";
        artworkText.text += $"  Artist: {artwork.ArtistName}\n";
        artworkText.text += $"  Artist Description: {artwork.ArtistDescription}\n";
        artworkText.text += $"  Date Created: {artwork.DateCreated}\n";
        artworkText.text += $"  Location: {artwork.Location}\n";
        artworkText.text += $"  Description: {artwork.Description}\n";
        artworkText.text += $"  Image URL: {artwork.ImageUrl}\n";

        gemini = new Gemini("You are the artwork: " + artwork.Name + " by " + artwork.ArtistName + " Additional Context: " + artworkText.text, textToSpeechInputTextField, textToSpeechStartButton);
        
        // Example of asking a question
        AskQuestion("Introduce yourself.");
        

        if (artwork.ImageTexture != null)
        {
            artworkImage.sprite = Sprite.Create(artwork.ImageTexture, new Rect(0, 0, artwork.ImageTexture.width, artwork.ImageTexture.height), new UnityEngine.Vector2(0.5f, 0.5f));
        }
    }
}


public class ArtworkRegistryService
{
    private static readonly HttpClient httpClient = new HttpClient();
    private List<ArtworkDTO> artworks = new List<ArtworkDTO>();

    public ArtworkRegistryService(string rpcUrl, string contractAddress)
    {
        // Initialization logic if needed
        // GetArtworksAsync();
        // Debug.Log("Artworks fetched");
        // Debug.Log(artworks.Count);
        // Debug.Log(artworks);
    }

    public async Task GetArtworksAsync()
    {
        string url = "https://hello-near-examples.onrender.com/artworks";
        HttpResponseMessage response = await httpClient.GetAsync(url);

        if (response.IsSuccessStatusCode)
        {
            string jsonResponse = await response.Content.ReadAsStringAsync();
            JArray artworksArray = JArray.Parse(jsonResponse);
            artworks = new List<ArtworkDTO>();

            foreach (JArray artwork in artworksArray)
            {
                artworks.Add(new ArtworkDTO
                {
                    Name = artwork[0].ToString(),
                    ArtistName = artwork[1].ToString(),
                    ArtistDescription = artwork[2].ToString(),
                    DateCreated = BigInteger.Parse(artwork[3].ToString()),
                    Location = artwork[4].ToString(),
                    Description = artwork[5].ToString(),
                    ImageUrl = artwork[6].ToString()
                });
            }
        }
        else
        {
            throw new Exception("Failed to fetch artworks");
        }
    }

    public Task<BigInteger> GetArtworkCountAsync()
    {
        return Task.FromResult((BigInteger)artworks.Count);
    }

    public Task<ArtworkDTO> GetArtworkAsync(BigInteger index)
    {
        return Task.FromResult(artworks[(int)index]);
    }

    public List<ArtworkDTO> Artworks => artworks;

    public class ArtworkDTO
    {
        public string Name { get; set; }
        public string ArtistName { get; set; }
        public string ArtistDescription { get; set; }
        public BigInteger DateCreated { get; set; }
        public string Location { get; set; }
        public string Description { get; set; }
        public string ImageUrl { get; set; }
        public Texture2D ImageTexture { get; set; }
    }
}
