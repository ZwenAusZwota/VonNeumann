using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuController : MonoBehaviour
{

    [Header("Menu Settings")]
    public TextMeshProUGUI textStart;
    public Button buttonStart;
    public Button buttonExit;

    void OnInitialize()
    {
        // Initialize menu buttons and text
        buttonStart.onClick.AddListener(startGame);
        buttonExit.onClick.AddListener(closeGame);
        // textStart.text = "Start Game"; // Set default text
    }

    public void closeGame()
    {
        //save game state if necessary
        Application.Quit();
    }

    public void startGame()
    {
        // Load the game scene or start a new game
        SceneManager.LoadScene("Loading");
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Initialize menu text
        // if saved game existst -> textStart.text = "Continue Game";
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
