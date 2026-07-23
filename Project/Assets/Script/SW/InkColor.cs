using UnityEngine;
using UnityEngine.UI;

//InkColor: 잉크 바의 종류에 따른 잉크 최소 요구량 표시
public class InkColor : MonoBehaviour
{
    [SerializeField] private int version;
    [SerializeField] private Color defaultColor;
    [SerializeField] private Color nextColor;
    [SerializeField] private Image ink;

    // Update is called once per frame
    void Update()
    {
        if (version == 1)
        {
            if (Player.instance.status.ink > Player.instance.status.minInk)
            {
                ink.color = defaultColor;
            }
            else
            {
                ink.color = nextColor;
            }
        }
        if (version == 2)
        {
            if (Player.instance.status.specialInk > Player.instance.status.minSpecialInk)
            {
                ink.color = defaultColor;
            }
            else
            {
                ink.color = nextColor;
            }
        }
    }
}
