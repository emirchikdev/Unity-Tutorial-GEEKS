using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class VariableBox : MonoBehaviour
{
    [Header("Поля ввода (на Canvas)")]
    public TMP_InputField nameInput;     // имя переменной
    public TMP_Dropdown typeDropdown;    // тип: int, float, string, bool
    public TMP_InputField valueInput;    // значение

    [Header("Кнопки (на Canvas), необязательно")]
    public Button putButton;             // "Положить в коробку"
    public Button clearButton;           // "Очистить"

    [Header("Сама коробка")]
    public Transform boxRoot;            // родитель коробки (его трясём и надуваем)
    public GameObject[] boxModels;       // 4 модели по порядку: int, float, string, bool
    public TMP_Text boxNameLabel;        // наклейка с именем
    public TMP_Text boxTypeLabel;        // форма коробки (тип)
    public TMP_Text boxValueLabel;       // что лежит внутри
    public GameObject contentsObject;    // предмет внутри коробки (виден, только когда в коробке что-то есть)

    [Header("Подсказки")]
    public TMP_Text codeLine;            // например: int age = 5;
    public TMP_Text messageText;         // что сказала коробка (должен переносить строки!)
    public string emptyText = "";        // что написано на пустой коробке (можно вписать "пусто")

    [Header("Цвета сообщений")]
    public Color okColor = new Color(0.3f, 1f, 0.3f);
    public Color errorColor = new Color(1f, 0.35f, 0.35f);
    public Color noteColor = new Color(1f, 0.85f, 0.3f);   // цвет пояснения под сообщением

    private enum VarType { Int, Float, String, Bool }

    private readonly string[] typeNames = { "int", "float", "string", "bool" };
    private readonly string[] typeHints =
    {
        "целые числа",
        "дробные числа",
        "текст",
        "да / нет (true / false)"
    };

    // Слова, которые нельзя использовать как имя переменной
    private readonly string[] keywords =
    {
        "int", "float", "string", "bool", "true", "false", "null",
        "class", "if", "else", "for", "while", "new", "public",
        "private", "static", "void", "return"
    };

    private Vector3 baseScale;
    private bool hasValue = false;
    private VarType currentType;
    private string currentName = "";
    private string currentValue = "";

    void Start()
    {
        baseScale = boxRoot.localScale;

        // Первый пункт списка - подсказка, дальше типы по порядку
        List<string> options = new List<string>();
        options.Add("Выберите тип данных");
        options.AddRange(typeNames);

        typeDropdown.ClearOptions();
        typeDropdown.AddOptions(options);
        typeDropdown.SetValueWithoutNotify(0);
        typeDropdown.RefreshShownValue();
        typeDropdown.onValueChanged.AddListener(OnTypeChanged);

        // Если кнопки перетащены в поля, подключаем их сами
        if (putButton != null) putButton.onClick.AddListener(Put);
        if (clearButton != null) clearButton.onClick.AddListener(Clear);

        OnTypeChanged(0);   // заодно делает коробку пустой
    }

    // Меняем форму коробки при выборе типа
    private void OnTypeChanged(int dropdownIndex)
    {
        // 0 - это подсказка "Выберите тип данных", настоящие типы начинаются с 1
        int typeIndex = dropdownIndex - 1;

        for (int i = 0; i < boxModels.Length; i++)
        {
            if (boxModels[i] != null)
            {
                boxModels[i].SetActive(i == typeIndex);
            }
        }

        // Другая форма коробки - это новая пустая коробка
        ResetBox(false);

        if (typeIndex < 0)
        {
            // Тип ещё не выбран: коробки нет
            boxTypeLabel.text = "";
            return;
        }

        boxTypeLabel.text = typeNames[typeIndex] + " - " + typeHints[typeIndex];
        PlayPunch();
    }

    // Кнопка "Положить в коробку"
    public void Put()
    {
        // 0 - тип не выбран
        if (typeDropdown.value == 0)
        {
            Reject("Сначала выбери тип данных.",
                "Тип - это форма коробки. От неё зависит, что в коробку можно положить.");
            return;
        }

        string varName = nameInput.text.Trim();
        VarType type = (VarType)(typeDropdown.value - 1);

        // 1. Проверяем имя
        string nameProblem = CheckName(varName);
        if (nameProblem != null)
        {
            Reject(nameProblem, null);
            return;
        }

        // 2. Проверяем значение под выбранный тип
        string display;      // что показываем на коробке
        string codeValue;    // как значение выглядит в коде
        string note;         // пояснение под сообщением

        if (type == VarType.String)
        {
            string text = valueInput.text;
            string trimmed = text.Trim();
            string lower = trimmed.ToLower();
            float asNumber;

            display = Quote(text);
            codeValue = Quote(text);

            if (text.Length == 0)
            {
                note = "Пустой текст - это тоже текст. В коде это две кавычки подряд.";
            }
            else if (TryParseFloat(trimmed, out asNumber))
            {
                note = "Цифры внутри string - это просто текст, а не число. Например, "
                     + Quote("5") + " + " + Quote("5") + " даст " + Quote("55") + ", а не 10.";
            }
            else if (lower == "true" || lower == "false")
            {
                note = text + " в string - это просто слово, а не значение bool. Настоящее true лежит в коробке bool.";
            }
            else
            {
                note = "Текст в коде всегда стоит в кавычках, чтобы C# не путал его с именами переменных.";
            }
        }
        else
        {
            string raw = valueInput.text.Trim();
            string lower = raw.ToLower();

            if (raw.Length == 0)
            {
                Reject("Поле значения пустое. Что положить в коробку?",
                    "Для текста пустое значение допустимо, а для чисел и bool нет.");
                return;
            }

            if (type == VarType.Int)
            {
                int number;
                if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
                {
                    float asFloat;
                    if (lower == "true" || lower == "false")
                    {
                        Reject("Коробка int берёт только целые числа, а " + Quote(raw) + " - это значение для bool.",
                            "Выбери коробку bool.");
                    }
                    else if ((raw.Contains(".") || raw.Contains(",")) && TryParseFloat(raw, out asFloat))
                    {
                        Reject(Quote(raw) + " - дробное число, а коробка int берёт только целые.",
                            "Дробные числа живут в коробке float.");
                    }
                    else if (IsWholeNumberText(raw))
                    {
                        Reject("Число " + Quote(raw) + " слишком большое для int.",
                            "В int помещаются числа примерно от -2 миллиардов до +2 миллиардов.");
                    }
                    else
                    {
                        Reject("Коробка int берёт только целые числа, а " + Quote(raw) + " - не число.",
                            "Слова и текст кладут в коробку string.");
                    }
                    return;
                }

                display = number.ToString(CultureInfo.InvariantCulture);
                codeValue = display;
                note = "int хранит только целые числа. Например, 5 / 2 в int даст 2: дробная часть отбрасывается.";
            }
            else if (type == VarType.Float)
            {
                float number;
                if (!TryParseFloat(raw, out number))
                {
                    if (lower == "true" || lower == "false")
                    {
                        Reject("Коробка float берёт только числа, а " + Quote(raw) + " - это значение для bool.",
                            "Выбери коробку bool.");
                    }
                    else
                    {
                        Reject("Коробка float берёт только числа, а " + Quote(raw) + " - не число.",
                            "Слова и текст кладут в коробку string.");
                    }
                    return;
                }

                display = number.ToString(CultureInfo.InvariantCulture);
                codeValue = display + "f";

                if (raw.Contains(","))
                {
                    note = "В коде дробную часть пишут через точку: " + codeValue + ", а не через запятую.";
                }
                else if (!raw.Contains("."))
                {
                    note = "Целое число тоже подходит для float. В коде оно пишется так: " + codeValue + ".";
                }
                else
                {
                    note = "Буква f в конце значения говорит C#: это число типа float.";
                }
            }
            else // Bool
            {
                if (lower != "true" && lower != "false")
                {
                    Reject("Коробка bool берёт только true или false.",
                        "Не 1 и 0, не да и нет, не yes и no. Только эти два слова.");
                    return;
                }

                display = lower;
                codeValue = lower;

                if (raw != lower)
                {
                    note = "В C# пишется только маленькими буквами: true, а не " + raw + ". Мы исправили за тебя.";
                }
                else
                {
                    note = "bool - это всегда один из двух вариантов: true или false. Третьего не бывает.";
                }
            }
        }

        // 3. Всё подошло - кладём
        string oldValue = currentValue;
        bool replaced = hasValue && currentType == type && currentName == varName;

        hasValue = true;
        currentType = type;
        currentName = varName;
        currentValue = display;

        boxNameLabel.text = varName;
        boxValueLabel.text = display;

        if (replaced)
        {
            // Во второй раз тип не пишем: коробка с таким именем уже есть
            codeLine.text = varName + " = " + codeValue + ";";
            ShowMessage("Заменили: было " + oldValue + ", стало " + display + ".", okColor,
                "Старое значение пропало: в коробке всегда лежит что-то одно. Тип второй раз не пишем, коробка уже есть.");
        }
        else
        {
            codeLine.text = typeNames[(int)type] + " " + varName + " = " + codeValue + ";";
            ShowMessage("Готово! Коробка приняла значение.", okColor, note);
        }

        if (contentsObject != null)
        {
            contentsObject.SetActive(true);
        }

        PlayPunch();
    }

    // Кнопка "Очистить": коробка пустая, поля ввода тоже очищаются
    public void Clear()
    {
        ResetBox(true);
        PlayPunch();
    }

    // Делает коробку пустой: ни имени, ни значения, ни кода, ни сообщения
    private void ResetBox(bool clearInputs)
    {
        hasValue = false;
        currentName = "";
        currentValue = "";

        boxNameLabel.text = "";
        boxValueLabel.text = emptyText;
        codeLine.text = "";
        messageText.text = "";

        if (contentsObject != null)
        {
            contentsObject.SetActive(false);
        }

        if (clearInputs)
        {
            nameInput.text = "";
            valueInput.text = "";
        }
    }

    // ---------- вспомогательное ----------

    private void Reject(string text, string note)
    {
        ShowMessage(text, errorColor, note);
        PlayShake();
    }

    // Основное сообщение своим цветом, пояснение под ним жёлтым
    private void ShowMessage(string text, Color color, string note)
    {
        messageText.color = color;

        if (string.IsNullOrEmpty(note))
        {
            messageText.text = text;
        }
        else
        {
            messageText.text = text + "\n<color=#" + ColorUtility.ToHtmlStringRGB(noteColor) + ">" + note + "</color>";
        }
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

    private string Quote(string text)
    {
        return "\"" + text + "\"";
    }

    // Принимаем и 2.5, и 2,5
    private bool TryParseFloat(string raw, out float result)
    {
        return float.TryParse(raw.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }

    // Только цифры (можно со знаком в начале)
    private bool IsWholeNumberText(string text)
    {
        int start = 0;
        if (text.StartsWith("-") || text.StartsWith("+"))
        {
            start = 1;
        }
        if (text.Length <= start)
        {
            return false;
        }
        for (int i = start; i < text.Length; i++)
        {
            if (!char.IsDigit(text[i]))
            {
                return false;
            }
        }
        return true;
    }

    // Возвращает объяснение, что не так с именем, или null, если имя хорошее
    private string CheckName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return "Напиши имя переменной. Это наклейка на коробке.";
        }
        if (name.Contains(" "))
        {
            return "В имени не бывает пробелов. Вместо пробела ставят _ , например my_age.";
        }
        if (char.IsDigit(name[0]))
        {
            return "Имя не может начинаться с цифры. Так можно: age1, а так нельзя: 1age.";
        }
        foreach (char c in name)
        {
            if (!char.IsLetterOrDigit(c) && c != '_')
            {
                return "В имени можно использовать только буквы, цифры и _ . Символ " + Quote(c.ToString()) + " не подходит.";
            }
        }
        foreach (string word in keywords)
        {
            if (name == word)
            {
                return "Слово " + word + " уже занято в C#, у него свой смысл. Придумай другое имя.";
            }
        }
        return null;
    }
}