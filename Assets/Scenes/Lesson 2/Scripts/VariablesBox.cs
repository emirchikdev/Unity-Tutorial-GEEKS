using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using TMPro;
using DG.Tweening;

public class VariableBox : MonoBehaviour
{
    [Header("Поля ввода (на Canvas)")]
    public TMP_InputField nameInput;     // имя переменной
    public TMP_Dropdown typeDropdown;    // тип: int, float, string, bool
    public TMP_InputField valueInput;    // значение

    [Header("Сама коробка")]
    public Transform boxRoot;            // родитель коробки (его трясём и надуваем)
    public GameObject[] boxModels;       // 4 модели по порядку: int, float, string, bool
    public TMP_Text boxNameLabel;        // наклейка с именем
    public TMP_Text boxTypeLabel;        // форма коробки (тип)
    public TMP_Text boxValueLabel;       // что лежит внутри

    [Header("Подсказки")]
    public TMP_Text codeLine;            // например: int age = 5;
    public TMP_Text messageText;         // что сказала коробка

    [Header("Цвета сообщений")]
    public Color okColor = new Color(0.3f, 1f, 0.3f);
    public Color errorColor = new Color(1f, 0.35f, 0.35f);

    private enum VarType { Int, Float, String, Bool }

    private readonly string[] typeNames = { "int", "float", "string", "bool" };
    private readonly string[] typeHints =
    {
        "целые числа",
        "дробные числа",
        "текст",
        "да / нет (true / false)"
    };

    private Vector3 baseScale;
    private bool hasValue = false;
    private VarType currentType;
    private string currentValue = "";

    void Start()
    {
        baseScale = boxRoot.localScale;

        // Заполняем список типов сами, чтобы порядок совпадал со скриптом
        typeDropdown.ClearOptions();
        typeDropdown.AddOptions(new List<string>(typeNames));
        typeDropdown.onValueChanged.AddListener(OnTypeChanged);

        OnTypeChanged(0);
        Clear();
    }

    // Меняем форму коробки при выборе типа
    private void OnTypeChanged(int index)
    {
        for (int i = 0; i < boxModels.Length; i++)
        {
            if (boxModels[i] != null)
            {
                boxModels[i].SetActive(i == index);
            }
        }

        boxTypeLabel.text = typeNames[index] + " — " + typeHints[index];
        PlayPunch();
    }

    // Кнопка "Положить в коробку"
    public void Put()
    {
        string varName = nameInput.text.Trim();
        VarType type = (VarType)typeDropdown.value;

        // 1. Проверяем имя
        if (!IsValidName(varName))
        {
            Reject("Имя не подходит. Без пробелов, не с цифры, только буквы, цифры и _ .");
            return;
        }

        // 2. Проверяем значение под выбранный тип
        string display;      // что показываем на коробке
        string codeValue;    // как значение выглядит в коде

        if (type == VarType.String)
        {
            display = valueInput.text;
            codeValue = "\"" + valueInput.text + "\"";
        }
        else
        {
            string raw = valueInput.text.Trim();

            if (type == VarType.Int)
            {
                int number;
                if (!int.TryParse(raw, out number))
                {
                    float asFloat;
                    if (TryParseFloat(raw, out asFloat))
                    {
                        Reject("«" + raw + "» — дробное число. Коробка int берёт только целые. Попробуй float.");
                    }
                    else
                    {
                        Reject("Коробка int берёт только целые числа, а «" + raw + "» не число.");
                    }
                    return;
                }
                display = number.ToString();
                codeValue = display;
            }
            else if (type == VarType.Float)
            {
                float number;
                if (!TryParseFloat(raw, out number))
                {
                    Reject("Коробка float берёт только числа, а «" + raw + "» не число.");
                    return;
                }
                display = number.ToString(CultureInfo.InvariantCulture);
                codeValue = display + "f";
            }
            else // Bool
            {
                string lower = raw.ToLower();
                if (lower != "true" && lower != "false")
                {
                    Reject("Коробка bool берёт только true или false.");
                    return;
                }
                display = lower;
                codeValue = lower;
            }
        }

        // 3. Всё подошло - кладём
        string oldValue = currentValue;
        bool replaced = hasValue && currentType == type;

        hasValue = true;
        currentType = type;
        currentValue = display;

        boxNameLabel.text = varName;
        boxValueLabel.text = display;
        codeLine.text = typeNames[(int)type] + " " + varName + " = " + codeValue + ";";

        if (replaced)
        {
            ShowMessage("Заменили: было «" + oldValue + "», стало «" + display + "».", okColor);
        }
        else
        {
            ShowMessage("Готово! Коробка приняла значение.", okColor);
        }

        PlayPunch();
    }

    // Кнопка "Очистить"
    public void Clear()
    {
        hasValue = false;
        currentValue = "";
        boxNameLabel.text = "";
        boxValueLabel.text = "пусто";
        codeLine.text = "";
        messageText.text = "";
    }

    // ---------- вспомогательное ----------

    private void Reject(string text)
    {
        ShowMessage(text, errorColor);
        PlayShake();
    }

    private void ShowMessage(string text, Color color)
    {
        messageText.text = text;
        messageText.color = color;
    }

    private void PlayPunch()
    {
        boxRoot.DOKill(true);
        boxRoot.localScale = baseScale;
        boxRoot.DOPunchScale(baseScale * 0.2f, 0.4f);
    }

    private void PlayShake()
    {
        boxRoot.DOKill(true);
        boxRoot.DOShakePosition(0.5f, 0.3f, 20);
    }

    // Принимаем и 2.5, и 2,5
    private bool TryParseFloat(string raw, out float result)
    {
        return float.TryParse(raw.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }

    // Имя: не пустое, не с цифры, только буквы, цифры и _
    private bool IsValidName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }
        if (char.IsDigit(name[0]))
        {
            return false;
        }
        foreach (char c in name)
        {
            if (!char.IsLetterOrDigit(c) && c != '_')
            {
                return false;
            }
        }
        return true;
    }
}