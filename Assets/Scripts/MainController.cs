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

    void Start()
    {
        artworkRegistryService = new ArtworkRegistryService(rpcUrl, contractAddress);
        artworks = new List<ArtworkRegistryService.ArtworkDTO>();
        StartCoroutine(FetchArtworksCoroutine());
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

        if (artwork.ImageTexture != null)
        {
            artworkImage.sprite = Sprite.Create(artwork.ImageTexture, new Rect(0, 0, artwork.ImageTexture.width, artwork.ImageTexture.height), new UnityEngine.Vector2(0.5f, 0.5f));
        }
    }
}

public class ArtworkRegistryService
{
    private Web3 web3;
    private Contract contract;

    public ArtworkRegistryService(string rpcUrl, string contractAddress)
    {
        web3 = new Web3(rpcUrl);
        string abi = @"[{
            ""constant"": true,
            ""inputs"": [],
            ""name"": ""getArtworkCount"",
            ""outputs"": [{""name"": """", ""type"": ""uint256""}],
            ""payable"": false,
            ""stateMutability"": ""view"",
            ""type"": ""function""
        },{
            ""constant"": true,
            ""inputs"": [{""name"": ""_index"", ""type"": ""uint256""}],
            ""name"": ""getArtwork"",
            ""outputs"": [
                {""name"": """", ""type"": ""string""},
                {""name"": """", ""type"": ""string""},
                {""name"": """", ""type"": ""string""},
                {""name"": """", ""type"": ""uint256""},
                {""name"": """", ""type"": ""string""},
                {""name"": """", ""type"": ""string""},
                {""name"": """", ""type"": ""string""}
            ],
            ""payable"": false,
            ""stateMutability"": ""view"",
            ""type"": ""function""
        }]";
        contract = web3.Eth.GetContract(abi, contractAddress);
    }

    public Task<BigInteger> GetArtworkCountAsync()
    {
        var getArtworkCountFunction = contract.GetFunction("getArtworkCount");
        return getArtworkCountFunction.CallAsync<BigInteger>();
    }

    public Task<ArtworkDTO> GetArtworkAsync(BigInteger index)
    {
        var getArtworkFunction = contract.GetFunction("getArtwork");
        return getArtworkFunction.CallDeserializingToObjectAsync<ArtworkDTO>(index);
    }

    [FunctionOutput]
    public class ArtworkDTO : IFunctionOutputDTO
    {
        [Parameter("string", "name", 1)] public string Name { get; set; }
        [Parameter("string", "artistName", 2)] public string ArtistName { get; set; }
        [Parameter("string", "artistDescription", 3)] public string ArtistDescription { get; set; }
        [Parameter("uint256", "dateCreated", 4)] public BigInteger DateCreated { get; set; }
        [Parameter("string", "location", 5)] public string Location { get; set; }
        [Parameter("string", "description", 6)] public string Description { get; set; }
        [Parameter("string", "imageUrl", 7)] public string ImageUrl { get; set; }
        public Texture2D ImageTexture { get; set; } // Add this property
    }
}
