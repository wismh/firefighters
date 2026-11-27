using System;
using System.Collections.Generic;
using _Project.Audio;
using MyToolkit.Audio;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Zenject;
using Random = UnityEngine.Random;

namespace _Project.Mission
{
    /// <summary>
    /// The dispatcher's radio on the left of the screen: her portrait that talks and blinks behind a bit of static,
    /// and her words typed out letter by letter with a mumble per syllable. A monologue holds the game: Space, Enter
    /// or the left mouse button finishes the line or turns to the next, Tab skips the rest. A single line during play
    /// types out, stays a moment and the radio closes; lines that come meanwhile wait their turn, an urgent one cuts
    /// in. Runs on unscaled time, so hit-stops do not stall it.
    /// </summary>
    public sealed class RadioView : MonoBehaviour
    {
        private const string Hidden = "<color=#00000000>";
        private const string HiddenEnd = "</color>";

        [SerializeField] private RectTransform panel;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Image portrait;
        [SerializeField] private RawImage staticNoise;
        [SerializeField] private Text body;
        [SerializeField] private Text hint;

        [Header("Portrait")]
        [Tooltip("Mouth closed, then the talking frames.")]
        [SerializeField] private Sprite[] talkFrames;
        [SerializeField] private Sprite blinkFrame;
        [SerializeField] private float mouthInterval = 0.085f;
        [SerializeField] private Vector2 blinkInterval = new Vector2(2f, 5f);
        [SerializeField] private float blinkTime = 0.12f;

        [Header("Typing")]
        [SerializeField] private float charsPerSecond = 40f;
        [Tooltip("Extra seconds after . ! ? and half of it after a comma.")]
        [SerializeField] private float punctuationPause = 0.22f;
        [Tooltip("A mumble every this many letters.")]
        [SerializeField] private int blipEvery = 2;
        [Tooltip("A single line stays this long after it is typed, plus holdPerChar per letter.")]
        [SerializeField] private float holdTime = 2.4f;
        [SerializeField] private float holdPerChar = 0.03f;

        [Header("Show / hide")]
        [SerializeField] private float slideDistance = 60f;
        [SerializeField] private float showSpeed = 6f;

        [Header("Sound")]
        [SerializeField] private SoundData openSound;
        [SerializeField] private SoundData closeSound;
        [SerializeField] private SoundBank blips;

        private readonly List<string> _queue = new List<string>(8);

        private ISfxPlayer _sfx;
        private Vector2 _shownPosition;
        private float _shown;
        private bool _open;

        private string _text;
        private int _typed;
        private float _typeTimer;
        private float _hold;

        private IReadOnlyList<string> _monologue;
        private int _monologueIndex;
        private Action _finished;

        private float _mouthTimer;
        private int _mouthFrame;
        private float _nextBlink;
        private float _blinkUntil;
        private float _hintTimer;

        /// <summary>A monologue is playing: the game waits for it.</summary>
        public bool IsBlocking => _monologue != null;

        [Inject]
        public void Construct(ISfxPlayer sfx)
        {
            _sfx = sfx;
        }

        /// <summary>Plays <paramref name="lines"/> one by one, each waiting for the player; then calls <paramref name="finished"/>.</summary>
        public void Monologue(IReadOnlyList<string> lines, Action finished)
        {
            _monologue = lines;
            _monologueIndex = 0;
            _finished = finished;
            Begin(lines[0]);
        }

        /// <summary>A line during play. An urgent one replaces a line on the radio now; others wait for it.</summary>
        public void Say(string line, bool urgent = false)
        {
            var busy = _text != null;
            if (IsBlocking || (busy && !urgent))
            {
                if (urgent)
                {
                    _queue.Insert(0, line);
                }
                else
                {
                    _queue.Add(line);
                }

                return;
            }

            Begin(line);
        }

        private void Awake()
        {
            _shownPosition = panel.anchoredPosition;
            group.alpha = 0f;
            panel.anchoredPosition = _shownPosition - new Vector2(slideDistance, 0f);
            hint.gameObject.SetActive(false);
            portrait.sprite = talkFrames[0];
            _nextBlink = Random.Range(blinkInterval.x, blinkInterval.y);
        }

        private void Begin(string line)
        {
            _text = line;
            _typed = 0;
            _typeTimer = 0.15f;
            _hold = holdTime + line.Length * holdPerChar;
            body.text = Hidden + line + HiddenEnd;
            if (!_open)
            {
                _open = true;
                _sfx.PlaySfx(openSound);
            }
        }

        private void Close()
        {
            _text = null;
            if (_open)
            {
                _open = false;
                _sfx.PlaySfx(closeSound);
            }
        }

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            UpdateShow(dt);
            UpdatePortrait(dt);
            if (_text == null)
            {
                return;
            }

            UpdateTyping(dt);
            var typed = _typed >= _text.Length;
            if (IsBlocking)
            {
                UpdateMonologueInput(typed, dt);
                return;
            }

            if (typed)
            {
                _hold -= dt;
                if (_hold <= 0f)
                {
                    Next();
                }
            }
        }

        private void UpdateShow(float dt)
        {
            var target = _open ? 1f : 0f;
            _shown = Mathf.MoveTowards(_shown, target, showSpeed * dt);
            var eased = 1f - (1f - _shown) * (1f - _shown);
            group.alpha = eased;
            panel.anchoredPosition = _shownPosition - new Vector2(slideDistance * (1f - eased), 0f);
        }

        private void UpdateTyping(float dt)
        {
            if (_typed >= _text.Length)
            {
                return;
            }

            _typeTimer -= dt;
            var changed = false;
            while (_typeTimer <= 0f && _typed < _text.Length)
            {
                var letter = _text[_typed++];
                changed = true;
                _typeTimer += 1f / charsPerSecond + PauseAfter(letter);
                if (char.IsLetter(letter) && _typed % blipEvery == 0)
                {
                    blips.Play(_sfx);
                }
            }

            if (changed)
            {
                ShowTyped();
            }
        }

        private float PauseAfter(char letter)
        {
            return letter switch
            {
                '.' or '!' or '?' or '…' => punctuationPause,
                ',' or ':' or '—' => punctuationPause * 0.5f,
                _ => 0f,
            };
        }

        // The rest of the line is laid out but invisible, so words do not jump to the next row while typing.
        private void ShowTyped()
        {
            body.text = _typed >= _text.Length
                ? _text
                : string.Concat(_text.Substring(0, _typed), Hidden, _text.Substring(_typed), HiddenEnd);
        }

        private void UpdateMonologueInput(bool typed, float dt)
        {
            var showHint = typed;
            if (hint.gameObject.activeSelf != showHint)
            {
                hint.gameObject.SetActive(showHint);
                _hintTimer = 0f;
            }

            if (showHint)
            {
                _hintTimer += dt;
                var color = hint.color;
                color.a = 0.55f + 0.45f * Mathf.Cos(_hintTimer * 5f);
                hint.color = color;
            }

            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard != null && keyboard.tabKey.wasPressedThisFrame)
            {
                EndMonologue();
                return;
            }

            var advance = (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame ||
                                                keyboard.enterKey.wasPressedThisFrame)) ||
                          (mouse != null && mouse.leftButton.wasPressedThisFrame);
            if (!advance)
            {
                return;
            }

            if (!typed)
            {
                _typed = _text.Length;
                ShowTyped();
                return;
            }

            _monologueIndex++;
            if (_monologueIndex < _monologue.Count)
            {
                Begin(_monologue[_monologueIndex]);
                hint.gameObject.SetActive(false);
                return;
            }

            EndMonologue();
        }

        private void EndMonologue()
        {
            hint.gameObject.SetActive(false);
            var finished = _finished;
            _monologue = null;
            _finished = null;
            _text = null;
            finished?.Invoke();
            // The callback may have started a line of its own.
            if (_text == null)
            {
                Next();
            }
        }

        private void Next()
        {
            if (_queue.Count == 0)
            {
                Close();
                return;
            }

            var line = _queue[0];
            _queue.RemoveAt(0);
            Begin(line);
        }

        private void UpdatePortrait(float dt)
        {
            var now = Time.unscaledTime;
            if (now >= _nextBlink)
            {
                _blinkUntil = now + blinkTime;
                _nextBlink = now + Random.Range(blinkInterval.x, blinkInterval.y);
            }

            var talking = _text != null && _typed < _text.Length;
            _mouthTimer -= dt;
            if (_mouthTimer <= 0f)
            {
                _mouthTimer = mouthInterval * Random.Range(0.7f, 1.3f);
                _mouthFrame = talking ? Random.Range(0, talkFrames.Length) : 0;
            }

            if (!talking)
            {
                _mouthFrame = 0;
            }

            portrait.sprite = now < _blinkUntil && _mouthFrame == 0 ? blinkFrame : talkFrames[_mouthFrame];

            // The static drifts and flickers, stronger while the radio is talking.
            var uv = staticNoise.uvRect;
            uv.x = Random.value;
            uv.y = Random.value;
            staticNoise.uvRect = uv;
            var color = staticNoise.color;
            color.a = (talking ? 0.07f : 0.035f) * Random.Range(0.6f, 1f);
            staticNoise.color = color;
        }
    }
}
