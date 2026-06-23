using UnityEngine;
using UnityEngine.UI;

public class StatusImage : MonoBehaviour
{
    public static StatusImage instance;

    Image image;
    public Sprite[] sprites;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (instance == null)
            instance = this;

        image = GetComponent<Image>();
    }

    public void ChangeImage(int index, bool isSkill)
    {
        if(isSkill)
        {
            image.sprite = sprites[index + 4];
        }
        else
        {
            image.sprite = sprites[index];
        }
    }
}
