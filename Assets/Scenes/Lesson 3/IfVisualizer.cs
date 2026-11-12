using UnityEngine;
using TMPro;

public class IfVisualizer : MonoBehaviour
{
    [Header("Строки кода на панели (TextMeshPro)")]
    public TMP_Text ifLine;        // if (number == 1)
    public TMP_Text elseIfLine;    // else if (number == 2)
    public TMP_Text elseLine;      // else

    [Header("Лампы над путями (3D объекты)")]
    public Renderer lampLeft;
    public Renderer lampRight;
    public Renderer lampStraight;

    [Header("Цвета")]
    public Color activeText = new Color(0.3f, 1f, 0.3f);
    public Color dimText = new Color(1f, 1f, 1f, 0.3f);
    public Color normalText = Color.white;
    public Color lampOn = new Color(0.3f, 1f, 0.3f);
    public Color lampOff = new Color(0.2f, 0.2f, 0.2f);

    void Start()
    {
        Clear();
    }

    // branch: 0 - сработал if, 1 - сработал else if, 2 - сработал else
    public void Show(int branch)
    {
        SetText(ifLine, branch == 0);
        SetText(elseIfLine, branch == 1);
        SetText(elseLine, branch == 2);

        SetLamp(lampLeft, branch == 0);
        SetLamp(lampRight, branch == 1);
        SetLamp(lampStraight, branch == 2);
    }

    // Сбросить: весь код обычного цвета, все лампы погашены
    public void Clear()
    {
        SetPlain(ifLine);
        SetPlain(elseIfLine);
        SetPlain(elseLine);

        SetLamp(lampLeft, false);
        SetLamp(lampRight, false);
        SetLamp(lampStraight, false);
    }

    private void SetText(TMP_Text text, bool active)
    {
        if (text == null)
        {
            return;
        }
        text.color = active ? activeText : dimText;
        text.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
    }

    private void SetPlain(TMP_Text text)
    {
        if (text == null)
        {
            return;
        }
        text.color = normalText;
        text.fontStyle = FontStyles.Normal;
    }

    private void SetLamp(Renderer lamp, bool on)
    {
        if (lamp == null)
        {
            return;
        }

        Color color = on ? lampOn : lampOff;
        lamp.material.color = color;

        // Свечение (работает с материалом Standard / Lit)
        if (on)
        {
            lamp.material.EnableKeyword("_EMISSION");
            lamp.material.SetColor("_EmissionColor", color * 2f);
        }
        else
        {
            lamp.material.SetColor("_EmissionColor", Color.black);
        }
    }
}