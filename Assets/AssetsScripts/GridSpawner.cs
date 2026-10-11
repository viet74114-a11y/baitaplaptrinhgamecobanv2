using UnityEngine;
using UnityEngine.UI;

public class GridSpawner : MonoBehaviour
{
    public GameObject blockPrefab;
    public Sprite[] sprites;
    public int count = 36;

    void Start()
    {
        for (int i = 0; i < count; i++)
        {
            GameObject go = Instantiate(blockPrefab, transform);
            go.GetComponent<Image>().sprite = sprites[Random.Range(0, sprites.Length)];
        }
    }
}