using System;
using System.Collections;
using System.Collections.Generic;
using KModkit;
using KeepCoding;
using UnityEngine;
using PeterO.Numbers;
using Eltrick.WCFloat;
using Rnd = UnityEngine.Random;
using System.Linq;

public class FPIScript : ModuleScript
{
    private KMBombInfo _Bomb;
    private KMBombModule _Module;
    private System.Random _Rnd;

    [SerializeField]
    private AudioClip[] _AudioClips;

    [SerializeField]
    private KMSelectable[] _Buttons;
    [SerializeField]
    private GameObject _Display;
    private TextMesh _DisplayText;

    private Float.Precision _Precision;
    private Float.Precision[] _Precisions = { Float.Precision.Half, Float.Precision.Single, Float.Precision.Double };
    private Float _CurrentNumber, _InitialNumber;

    private List<EInteger[]> _StageInfo = new List<EInteger[]>();
    private string[] _componentTypes = new string[] { "sign bit", "exponent", "mantissa" };
    private string _input = "", _expectedAnswer;
    private int _seed, _stageCount, _currentStage, _digitsToInput, _moduleCount;
    private float _threshold = 5f;
    private bool _isModuleSolved, _isSeedSet, _isInputTime, _isStageRecovery;

    private Prng _prng;

    // Use this for initialization
    void Start()
    {
        _Bomb = Get<KMBombInfo>();

        if (!_isSeedSet)
        {
            _seed = Rnd.Range(int.MinValue, int.MaxValue);
            Log("The seed is: " + _seed.ToString());
            _isSeedSet = true;
        }
        for (int i = 0; i < _Buttons.Length; i++)
        {
            int x = i;
            _Buttons[x].Assign(onInteract: () => HandlePress(x));
        }

        _Rnd = new System.Random(_seed);
        // SET SEED ABOVE IN CASE OF BUGS!!
        // _rnd = new System.Random(loggedSeed);
        _prng = new Prng(_Rnd.Next());
        _Module = Get<KMBombModule>();

        _DisplayText = _Display.GetComponentInChildren<TextMesh>();

        _moduleCount = _Bomb.GetSolvableModuleNames().Count();
        _stageCount = CustomSigmoid(_moduleCount) - 1;

        if (_stageCount == -1)
        {
            Log("No stages possible to be generated.");
            StopAllCoroutines();
            SetAllButtons(Color.white);
            _isInputTime = true;
            StartCoroutine(DisplayScroll("NO STAGE"));
            return;
        }

        _Precision = _Precisions[_Rnd.Next(_Precisions.Length)];
        ShowPrecision();

        _CurrentNumber = new Float(_Rnd.Next(), _Precision);

        while (_CurrentNumber.ToDecimal().Length > Mathf.FloorToInt(_threshold * _moduleCount))
        {
            _CurrentNumber.ModifyComponent(0, 1, _Rnd.Next());
            _CurrentNumber.ModifyComponent(0, 2, _prng.Next(_CurrentNumber.MaxMantissa()));
        }

        while (_CurrentNumber.ToDecimal().Contains(".0") || _CurrentNumber.Get()[1].Equals(_CurrentNumber.MaxExponent() - 1))
            _CurrentNumber.ModifyComponent(0, 1, 1);

        _InitialNumber = new Float(_CurrentNumber.Get(), _Precision);
        if (_stageCount == 0)
            StartCoroutine(DisplayScroll(_CurrentNumber.Get().Select(x => x.ToString()).Join(", ")));
        else
            StartCoroutine(DisplayScroll(_CurrentNumber.ToDecimal()));

        EInteger[] components = _CurrentNumber.Get();

        // op, argument, offset, c0, c1, c2
        _StageInfo.Add(new EInteger[] { -1, -1, -1, components[0], components[1], components[2] });

        Log("The starting decimal number is: " + _CurrentNumber.ToDecimal());
        Log("The starting components are: " + components.Select(x => x.ToString()).Join(", "));
        GenerateStages();

        _expectedAnswer = _CurrentNumber.ToDecimal();
        Log("The final number, in decimal, is: " + _expectedAnswer);

        TrimExpectedAnswer();
        Log("Trimming the number to the required number of characters results in: " + _expectedAnswer);
    }

    private int CustomSigmoid(int x)
    {
        return Mathf.FloorToInt((Mathf.Pow((float)Math.E, x / 32f) / (1 + Mathf.Pow((float)Math.E, x / 32f)) - .5f) * 110);
    }

    private void TrimExpectedAnswer()
    {
        // _digitsToInput = Math.Min(Mathf.FloorToInt(_threshold * _moduleCount), _expectedAnswer.Length);
        _digitsToInput = Math.Min(Mathf.FloorToInt(0.4f * Mathf.Pow(_moduleCount, 1.2f)) + 10, _expectedAnswer.Length);
        // _digitsToInput = _expectedAnswer.Length;
        _expectedAnswer = _expectedAnswer.Substring(0, _digitsToInput);
    }

    private void GenerateStages()
    {
        for (int i = 0; i < _stageCount; i++)
        {
            EInteger operate = i == _stageCount - 1 ? 0 : _Rnd.Next(2);
            EInteger argument = operate.Equals(1) ? _prng.Next(_CurrentNumber.BitCount()) : (i != _stageCount - 1 ? _Rnd.Next(3) : 1);
            if (operate.Equals(1))
            {
                _CurrentNumber.ModifyComponent(operate.ToInt32Checked(), argument, 0);
                Log("Stage " + (i + 1).ToString() + ": Invert bit " + argument.ToString());

                EInteger[] c = _CurrentNumber.Get();
                _StageInfo.Add(new EInteger[] { operate, argument, argument, c[0], c[1], c[2] });
            }
            else
            {
                EInteger limit = argument.Equals(1) ? _CurrentNumber.MaxExponent() : _CurrentNumber.MaxMantissa();
                EInteger offset = argument.Equals(0) ? 1 : _prng.Next(limit);

                _CurrentNumber.ModifyComponent(operate.ToInt32Checked(), argument, offset);
                if (i == _stageCount - 1)
                    while (_CurrentNumber.Get()[1].Equals(_CurrentNumber.MaxExponent() - 1) || _CurrentNumber.Get()[1] < _CurrentNumber.MaxExponent() / 2 - 6)
                    {
                        EInteger rnd = _prng.Next(limit);
                        _CurrentNumber.ModifyComponent(operate.ToInt32Checked(), argument, rnd);
                        offset = (offset + rnd) % _CurrentNumber.MaxExponent();
                    }

                Log("Stage " + (i + 1).ToString() + ": Modify the " + _componentTypes[argument.ToInt32Checked()] + " by " + offset.ToString());

                EInteger[] components = _CurrentNumber.Get();

                _StageInfo.Add(new EInteger[] { operate, argument, offset, components[0], components[1], components[2] });
            }
        }
    }

    private void SetAllButtons(Color color)
    {
        for (int i = 0; i < _Buttons.Length; i++)
            SetColour(_Buttons[i].gameObject, color);
    }

    private void ShowPrecision()
    {
        SetAllButtons(Color.black);

        KMSelectable[] buttons = new KMSelectable[_Buttons.Length];
        Array.Copy(_Buttons, buttons, _Buttons.Length);

        foreach (KMSelectable b in buttons.Shuffle().Take(4 + (int)_Precision))
            SetColour(b.gameObject, Color.white);
    }

    private void ShowStageNumber()
    {
        SetAllButtons(Color.black);

        SetColour(_Buttons[_currentStage % 10].gameObject, Color.white);
    }

    private void SetColour(GameObject gameObject, Color color)
    {
        if (color == Color.white)
        {
            gameObject.transform.Find("Base").GetComponent<MeshRenderer>().material.color = Color.black;
            gameObject.GetComponent<MeshRenderer>().material.color = Color.white;
            gameObject.GetComponentInChildren<TextMesh>().color = Color.black;
            return;
        }
        gameObject.transform.Find("Base").GetComponent<MeshRenderer>().material.color = Color.white;
        gameObject.GetComponent<MeshRenderer>().material.color = Color.black;
        gameObject.GetComponentInChildren<TextMesh>().color = Color.white;
    }

    private void HandlePress(int i)
    {
        if (_isModuleSolved)
            return;

        StopAllCoroutines();

        if (_stageCount == -1)
        {
            PlaySound(_Buttons[i].transform, false, _AudioClips[1]);
            _isModuleSolved = true;
            _Module.HandlePass();
            _DisplayText.text = "NONE";
            Log("A button was pressed. Module solved.");
            return;
        }

        if (!_isInputTime)
        {
            PlaySound(_Buttons[i].transform, false, _AudioClips[0]);
            if (i == 11 && _currentStage == 0)
            {
                StopAllCoroutines();
                _currentStage--;
                ShowNextStage();
                return;
            }
            if (_currentStage < _stageCount)
            {
                ShowNextStage();
                return;
            }
            _isStageRecovery = false;
            _isInputTime = true;
            StopAllCoroutines();
            SetAllButtons(Color.white);
            StartCoroutine(DisplayScroll("INPUT " + _digitsToInput.ToString()));
            return;
        }

        if (i == 11)
        {
            if (_input.Contains("."))
            {
                PlaySound(_Buttons[i].transform, false, _AudioClips[1]);
                if ((_input[_input.Length - 1] == '.' && _input.Substring(0, _input.Length - 1) == _expectedAnswer) || _input == _expectedAnswer)
                {
                    _isModuleSolved = true;
                    _Module.HandlePass();
                    _DisplayText.text = "SOLVE";
                    Log("Correct answer submitted. Module solved!");
                    return;
                }
                _Module.HandleStrike();
                _currentStage = -1;
                _DisplayText.text = "";
                _isStageRecovery = true;
                _isInputTime = false;
                Log("Incorrect answer submitted: " + (_input[_input.Length - 1] == '.' ? _input.Substring(0, _input.Length - 1) : _input) + "; Strike.");
                _input = "";
                Log("Showing stages once again...");
                return;
            }
        }
        PlaySound(_Buttons[i].transform, false, _AudioClips[0]);
        _input += _Buttons[i].GetComponentInChildren<TextMesh>().text;
        _DisplayText.text = ("     " + _input).Substring(_input.Length, 5);
    }

    private void ShowNextStage()
    {
        _currentStage++;
        ShowStageNumber();
        Log("Now showing: Stage " + _currentStage.ToString());
        StopAllCoroutines();
        if (_StageInfo[_currentStage][0].Equals(0) && _StageInfo[_currentStage][1].Equals(0))
        {
            _DisplayText.text = "";
            SetColour(_Display, _Rnd.Next(2) == 1 ? Color.white : Color.black);
            return;
        }

        if (_currentStage == 0)
        {
            ShowPrecision();
            if (_isStageRecovery || _stageCount == 0)
                StartCoroutine(DisplayScroll(_InitialNumber.Get().Select(x => x.ToString()).Join(", ")));
            else
                StartCoroutine(DisplayScroll(_InitialNumber.ToDecimal()));
            return;
        }

        SetColour(_Display, _StageInfo[_currentStage][1].Equals(1) ? Color.white : Color.black);
        StartCoroutine(DisplayScroll((_StageInfo[_currentStage][0].Equals(0) ? "A " : "I ") + _StageInfo[_currentStage][2].ToString()));
    }

    private IEnumerator DisplayScroll(string stageInfo)
    {
        _DisplayText.text = "     ";
        string modifierText = "      " + stageInfo + "      ";

        while (true)
        {
            for (int i = 0; i < modifierText.Length - 5; i++)
            {
                yield return new WaitForSeconds(.5f);
                _DisplayText.text = modifierText.Substring(i, 5);
            }
        }
    }

    //// Update is called once per frame
    //void Update()
    //{
    //    int currentNonIgnoredSolveCount = _Bomb.GetSolvedModuleIDs().Count(x => !_ignoredList.Contains(x));

    //    if (_isInputTime)
    //        return;

    //    if (_stageCount == -1)
    //    {
    //        StopAllCoroutines();
    //        SetAllButtons(Color.white);
    //        _isInputTime = true;
    //        StartCoroutine(DisplayScroll("NO STAGE"));
    //    }
    //    else if (currentNonIgnoredSolveCount == _stageCount + 1)
    //    {
    //        StopAllCoroutines();
    //        SetAllButtons(Color.white);
    //        _isInputTime = true;
    //        StartCoroutine(DisplayScroll("INPUT " + _digitsToInput.ToString()));
    //    }
    //    else if (_lastNonIgnoredSolveCount < currentNonIgnoredSolveCount)
    //    {
    //        ShowNextStage();
    //        _lastNonIgnoredSolveCount = currentNonIgnoredSolveCount;
    //    }
    //}
}
