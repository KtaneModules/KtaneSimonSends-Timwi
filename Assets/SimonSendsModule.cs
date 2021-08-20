using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SimonSends;
using UnityEngine;
using Rnd = UnityEngine.Random;

/// <summary>
/// On the Subject of Simon Sends
/// Created by Timwi
/// </summary>
public class SimonSendsModule : MonoBehaviour
{
    public KMBombInfo Bomb;
    public KMBombModule Module;
    public KMAudio Audio;
    public KMColorblindMode ColorblindMode;
    public MeshRenderer Diode;
    public Light[] Lights;
    public KMSelectable[] Buttons;
    public KMSelectable Knob;
    public GameObject AnswerUnitTemplate;
    public Material[] AnswerUnitMaterials;
    public TextMesh ColorblindDiodeText;
    public TextMesh[] ColorblindButtonText;

    private static readonly string[][] _manualText = /*MANUAL*/@"THIS IS THE FIRST WORD FOR PURPOSES OF COUNTING WORDS AND PARAGRAPHS IN THIS TEXT THE FLAVOR TEXT AND APPENDIX ARE EXCLUDED|HYPHENATED WORDS EQUATE TO JUST ONE WORD PUNCTUATION MARKS DO NOT COUNT AS LETTERS|A SIMON SENDS PUZZLE IS EQUIPPED WITH COLORIZED LIGHTS WHICH FLASH UNIQUE LETTERS IN MORSE CODE SIMULTANEOUSLY AND A DIAL FOR ADJUSTING THE FREQUENCY OF FLASHING|OWING TO THEIR PROXIMITY THE LIGHTS RED GREEN AND BLUE MIX BY WAY OF ADDITIVE COLOR MIXING WORK OUT THE INDIVIDUAL COLORS|CONVERT EACH RECOGNIZED LETTER INTO A NUMBER USING ITS ALPHABETIC POSITION CALL YOUR THUSLY ACQUIRED NUMBERS R G AND B DERIVE NEW LETTERS AS FOLLOWS|COUNT R LETTERS FROM THE START OF THE GTH WORD FROM THE START OF THE BTH PARAGRAPH IN THIS MANUAL AND MAKE IT YOUR NEW RED LETTER|COUNT G LETTERS FROM THE START OF THE BTH WORD FROM THE START OF THE RTH PARAGRAPH IN THIS MANUAL AND MAKE IT YOUR NEW GREEN LETTER|COUNT B LETTERS FROM THE START OF THE RTH WORD FROM THE START OF THE GTH PARAGRAPH IN THIS MANUAL AND MAKE IT YOUR NEW BLUE LETTER|REALIZE A NEW COLOR SEQUENCE BY JUXTAPOSING AGAIN USING KNOWN ADDITIVE COLOR MIXING ONE COPY OF EACH NEW LETTERS MORSE CODE|ACKNOWLEDGE A DOT AND A DASH IN MORSE CODE HAVE SIZES OF ONE AND THREE UNITS RESPECTIVELY GAPS BETWEEN THEM ALSO HAVE A SIZE OF JUST ONE UNIT|INPUT YOUR ACQUIRED COLOR SEQUENCE USING EACH QUALIFYING COLOR BUTTON|A MISTAKE IS REJECTED WITH A STRIKE ON SUCH AN OCCASION ADJUST AND FINISH YOUR ANSWER LOOK AT THE DISPLAY TO JUDGE YOUR INPUT THUS FAR|JUMP BACK TO THE FIRST WORD IF WHILE COUNTING YOU ADVANCE BEYOND THE LAST WORD WHICH IS THIS"/*!MANUAL*/
        .Split('|').Select(line => line.Split(' ')).ToArray();

    private static readonly string[] _morse = ".-|-...|-.-.|-..|.|..-.|--.|....|..|.---|-.-|.-..|--|-.|---|.--.|--.-|.-.|...|-|..-|...-|.--|-..-|-.--|--..".Split('|');
    private static readonly string _colorNames = "KBGCRMYW";
    private static readonly string[] _colorblindTextNames = { "BLACK", "BLUE", "GREEN", "CYAN", "RED", "MAGENTA", "YELLOW", "WHITE" };

    private static int _moduleIdCounter = 1;
    private int _moduleId;
    private string _morseR, _morseG, _morseB;
    private List<int[]> _acceptableAnswers;
    private List<int> _answerSoFar;
    private int _morseRPos, _morseGPos, _morseBPos;
    private MeshRenderer[] _answerUnits;
    private float _knobPosition = 1;
    private Coroutine _knobRotation = null;
    private bool _colorblindMode;

    void Start()
    {
        _moduleId = _moduleIdCounter++;
        SetColorblindMode(ColorblindMode.ColorblindModeActive);
        ColorblindDiodeText.text = "";

        _answerUnits = new MeshRenderer[13];
        for (int i = 0; i < 13; i++)
        {
            var obj = Instantiate(AnswerUnitTemplate);
            obj.name = "Answer unit " + (i + 1);
            obj.transform.parent = AnswerUnitTemplate.transform.parent;
            obj.transform.localRotation = Quaternion.identity;
            obj.transform.localPosition = new Vector3(1.2f * (i - 6), 0, 0);
            obj.transform.localScale = new Vector3(1, 1, 1);
            obj.SetActive(false);
            _answerUnits[i] = obj.GetComponent<MeshRenderer>();
        }
        AnswerUnitTemplate.SetActive(false);

        for (int i = 0; i < 8; i++)
            Buttons[i].OnInteract = getPressHandler(i);

        var available = Enumerable.Range(0, 26).ToList().Shuffle();
        var r = (char) (available[0] + 'A');
        var g = (char) (available[1] + 'A');
        var b = (char) (available[2] + 'A');

        _morseR = getMorse(r) + "___";
        _morseRPos = Rnd.Range(0, _morseR.Length);
        _morseG = getMorse(g) + "___";
        _morseGPos = Rnd.Range(0, _morseG.Length);
        _morseB = getMorse(b) + "___";
        _morseBPos = Rnd.Range(0, _morseB.Length);

        _acceptableAnswers = new List<int[]>();
        var answerLetterR = getLetter(b - 'A', g - 'A', r - 'A');
        var answerLetterG = getLetter(r - 'A', b - 'A', g - 'A');
        var answerLetterB = getLetter(g - 'A', r - 'A', b - 'A');
        var answerR = getMorse(answerLetterR);
        var answerG = getMorse(answerLetterG);
        var answerB = getMorse(answerLetterB);
        var maxLength = Math.Max(Math.Max(answerR.Length, answerG.Length), answerB.Length);
        for (int gr = 0; gr <= maxLength - answerR.Length; gr++)
            for (int gg = 0; gg <= maxLength - answerG.Length; gg++)
                for (int gb = 0; gb <= maxLength - answerB.Length; gb++)
                    _acceptableAnswers.Add(Enumerable.Range(0, maxLength).Select(i =>
                        (i < gr || i >= gr + answerR.Length || answerR[i - gr] != '#' ? 0 : 4) +
                        (i < gg || i >= gg + answerG.Length || answerG[i - gg] != '#' ? 0 : 2) +
                        (i < gb || i >= gb + answerB.Length || answerB[i - gb] != '#' ? 0 : 1)).ToArray());
        _answerSoFar = new List<int>();

        Debug.LogFormat(@"[Simon Sends #{0}] Blinking letters are: {1}{2}{3}", _moduleId, r, g, b);
        Debug.LogFormat(@"[Simon Sends #{0}] Solution letters are: {1}{2}{3}", _moduleId, answerLetterR, answerLetterG, answerLetterB);
        Debug.LogFormat(@"[Simon Sends #{0}] Acceptable answers:", _moduleId);
        foreach (var aa in _acceptableAnswers)
            Debug.LogFormat(@"[Simon Sends #{0}] — {1}", _moduleId, aa.Select(i => "KBGCRMYW"[i]).JoinString());

        StartCoroutine(Blink());

        Knob.OnInteract = knobStart;
        Knob.OnInteractEnded = knobEnd;

        float scalar = transform.lossyScale.x;
        foreach (Light light in Lights)
            light.range *= scalar;
    }

    private void SetColorblindMode(bool mode)
    {
        _colorblindMode = mode;
        for (int i = 0; i < ColorblindButtonText.Length; i++)
            ColorblindButtonText[i].gameObject.SetActive(mode);
        ColorblindDiodeText.gameObject.SetActive(mode);
    }

    private bool knobStart()
    {
        if (_knobRotation != null)
            StopCoroutine(_knobRotation);
        _knobRotation = StartCoroutine(rotateKnob());
        return false;
    }

    private IEnumerator rotateKnob()
    {
        while (true)
        {
            yield return null;
            _knobPosition -= Time.deltaTime;
            while (_knobPosition < 0)
                _knobPosition += 1f;
            Knob.transform.localEulerAngles = new Vector3(0, _knobPosition * 360, 0);
        }
    }

    private void knobEnd()
    {
        if (_knobRotation != null)
            StopCoroutine(_knobRotation);
        _knobRotation = null;
    }

    private IEnumerator Blink()
    {
        const float dark = .1f;
        const float bright = .65f;

        // _answerSoFar is set to null when the module is solved.
        while (_answerSoFar != null)
        {
            bool red = _morseR[_morseRPos] == '#';
            bool green = _morseG[_morseGPos] == '#';
            bool blue = _morseB[_morseBPos] == '#';
            var color = new Color(red ? bright : dark, green ? bright : dark, blue ? bright : dark);
            Diode.material.color = color;
            ColorblindDiodeText.gameObject.SetActive(_colorblindMode);
            ColorblindDiodeText.text = _colorblindTextNames[(red ? 4 : 0) + (green ? 2 : 0) + (blue ? 1 : 0)];
            foreach (var light in Lights)
                light.color = new Color(red ? 1 : 0, green ? 1 : 0, blue ? 1 : 0);
            yield return new WaitForSeconds(_knobPosition);
            _morseRPos = (_morseRPos + 1) % _morseR.Length;
            _morseGPos = (_morseGPos + 1) % _morseG.Length;
            _morseBPos = (_morseBPos + 1) % _morseB.Length;
        }
        Diode.material.color = new Color(dark, dark, dark);
        foreach (var light in Lights)
            light.gameObject.SetActive(false);
        ColorblindDiodeText.gameObject.SetActive(false);
    }

    private static string getMorse(char letter)
    {
        return _morse[letter - 'A'].Select(ch => ch == '.' ? "#" : "###").JoinString("_");
    }

    private static char getLetter(int paraCount, int wordCount, int letterCount)
    {
        var paraIx = paraCount % _manualText.Length;
        var wordIx = 0;
        for (int w2 = 0; w2 < wordCount; w2++)
        {
            wordIx++;
            if (wordIx >= _manualText[paraIx].Length)
            {
                wordIx = 0;
                paraIx = (paraIx + 1) % _manualText.Length;
            }
        }
        var letterIx = 0;
        for (int l2 = 0; l2 < letterCount; l2++)
        {
            letterIx++;
            if (letterIx >= _manualText[paraIx][wordIx].Length)
            {
                letterIx = 0;
                wordIx++;
                if (wordIx >= _manualText[paraIx].Length)
                {
                    wordIx = 0;
                    paraIx = (paraIx + 1) % _manualText.Length;
                }
            }
        }
        return _manualText[paraIx][wordIx][letterIx];
    }

    private KMSelectable.OnInteractHandler getPressHandler(int color)
    {
        return delegate
        {
            Buttons[color].AddInteractionPunch(.3f);
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, Buttons[color].transform);
            if (_answerSoFar == null)    // Module is solved.
                return false;

            var newAccAnswers = _acceptableAnswers.Where(aa => aa.Take(_answerSoFar.Count + 1).SequenceEqual(_answerSoFar.Concat(new[] { color }))).ToList();
            if (newAccAnswers.Count == 0)
            {
                // Unacceptable button press
                Debug.LogFormat(@"[Simon Sends #{0}] You tried to enter {1}. No acceptable answer begins with that. Strike.", _moduleId, _answerSoFar.Select(a => _colorNames[a]).JoinString() + _colorNames[color]);
                Module.HandleStrike();
            }
            else
            {
                if (color > 0)
                    Audio.PlaySoundAtTransform("Sound" + color, Buttons[color].transform);
                _acceptableAnswers = newAccAnswers;
                _answerUnits[_answerSoFar.Count].material = AnswerUnitMaterials[color];
                _answerUnits[_answerSoFar.Count].gameObject.SetActive(true);
                _answerSoFar.Add(color);
                if (_answerSoFar.Count == _acceptableAnswers[0].Length)
                {
                    Debug.LogFormat(@"[Simon Sends #{0}] Module solved.", _moduleId);
                    Module.HandlePass();
                    Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, transform);
                    _answerSoFar = null;
                    _acceptableAnswers = null;
                }
            }

            return false;
        };
    }

#pragma warning disable 0414
    private readonly string TwitchHelpMessage = "!{0} press 163724 [1–8 in reading order] | !{0} press kbgcrmyw [k=black, b=blue etc.] | !{0} speed .5 [set the speed; 0–1 where 0 is fastest and 1 is slowest]";
#pragma warning restore 0414

    private IEnumerator ProcessTwitchCommand(string command)
    {
        var match = Regex.Match(command, @"^\s*(?:press |submit |send |transmit |tx |)([kbgcrmyw1-8 ,;]+)\s*$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        if (match.Success)
        {
            yield return null;
            foreach (var btn in match.Groups[1].Value.Where(ch => "kbgcrmywKBGCRMYW12345678".Contains(ch)).Select(ch => Buttons["kbgcrmywKBGCRMYW12345678".IndexOf(ch) % 8]))
            {
                btn.OnInteract();
                yield return new WaitForSeconds(.4f);
            }
            yield break;
        }

        match = Regex.Match(command, @"^\s*(?:set ?)?(?:speed |rate |frequency )(\d*\.?\d+)\s*$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        if (match.Success)
        {
            double target;
            if (!double.TryParse(match.Groups[1].Value, out target) || target < 0 || target > 1)
            {
                yield return "sendtochaterror The speed must be between 0 and 1 (for example, 0.1 or 0.75).";
                yield break;
            }
            yield return null;
            var started = Time.time;
            Knob.OnInteract();
            while (Math.Abs(_knobPosition - target) > .025 && Time.time - started < 2)
                yield return null;
            Knob.OnInteractEnded();
            yield break;
        }

        match = Regex.Match(command, @"^\s*(?:colorblind?)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        if (match.Success)
        {
            SetColorblindMode(!_colorblindMode);
            yield return null;
            yield break;
        }
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        while (_acceptableAnswers != null)
        {
            Buttons[_acceptableAnswers[0][_answerSoFar.Count]].OnInteract();
            yield return new WaitForSeconds(.25f);
        }
    }
}
