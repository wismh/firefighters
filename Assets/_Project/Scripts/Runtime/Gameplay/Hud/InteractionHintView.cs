using System;
using _Project.Player;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace _Project.Hud
{
    /// <summary>The controls line at the bottom of the screen and the crosshair while the hose is in hand.</summary>
    public sealed class InteractionHintView : MonoBehaviour
    {
        [Serializable]
        public struct HintLine
        {
            public InteractionHint hint;
            public string text;
        }

        [SerializeField] private Text label;
        [SerializeField] private GameObject crosshair;
        [SerializeField] private HintLine[] lines =
        {
            new HintLine { hint = InteractionHint.Walking, text = "WASD — ходити, Shift — біг, Space — стрибок" },
            new HintLine { hint = InteractionHint.EnterCar, text = "E — сісти в машину" },
            new HintLine { hint = InteractionHint.TakeHose, text = "E — взяти шланг" },
            new HintLine
            {
                hint = InteractionHint.Driving,
                text = "WASD — кермо, Space — ручник, E — вийти (на ходу — вистрибнути)",
            },
            new HintLine { hint = InteractionHint.HoseAtReel, text = "ЛКМ — вода, E — повернути шланг" },
            new HintLine { hint = InteractionHint.HoseStretched, text = "Шланг натягнуто — далі не дістає" },
            new HintLine
            {
                hint = InteractionHint.Hosing,
                text = "ЛКМ — вода; щоб сісти в машину, поверни шланг на котушку",
            },
        };

        private IPlayerState _player;
        private InteractionHint? _shown;

        [Inject]
        public void Construct(IPlayerState player)
        {
            _player = player;
        }

        private void Update()
        {
            var hint = _player.Hint;
            if (hint == _shown)
            {
                return;
            }

            _shown = hint;
            label.text = TextOf(hint);
            crosshair.SetActive(_player.IsHosing);
        }

        private string TextOf(InteractionHint hint)
        {
            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].hint == hint)
                {
                    return lines[i].text;
                }
            }

            return string.Empty;
        }
    }
}
