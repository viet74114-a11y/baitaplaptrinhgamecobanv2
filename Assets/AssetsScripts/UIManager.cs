
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public GameObject screenMenu;
    public GameObject screenGame;
    public GameObject screenSettings;
    public GameObject screenGameOver;

    public GameManager game;

    void Start()
    {
        Show(screenMenu);
    }

    void Show(GameObject target)
    {
        screenMenu.SetActive(false);
        screenGame.SetActive(false);
        screenSettings.SetActive(false);
        screenGameOver.SetActive(false);

        target.SetActive(true);
    }

    public void ShowMenu()
    {
        Show(screenMenu);
    }

    public void ShowGame()
    {
        Show(screenGame);

        if (game != null)
            game.NewGame();
    }

    public void ShowSettings()
    {
        Show(screenSettings);
    }

    public void ShowGameOver()
    {
        Show(screenGameOver);
    }
}