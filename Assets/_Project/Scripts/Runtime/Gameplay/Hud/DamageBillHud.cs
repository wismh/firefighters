using _Project.Economy;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace _Project.Hud
{
    /// <summary>The running damage bill at the top of the screen; it swells and reddens for a moment when it grows.</summary>
    public sealed class DamageBillHud : MonoBehaviour
    {
        private const float PulseFade = 3f;
        private const float PulseScale = 0.4f;

        [SerializeField] private Text label;
        [Tooltip("{0} is the total.")]
        [SerializeField] private string format = "Рахунок за збитки: ${0:N0}";
        [SerializeField] private Color pulseColor = new Color(1f, 0.35f, 0.3f);

        private DamageBill _bill;
        private int _shown = -1;
        private float _pulse;

        [Inject]
        public void Construct(DamageBill bill)
        {
            _bill = bill;
        }

        private void Start()
        {
            _bill.Changed += OnChanged;
            Show();
            _pulse = 0f;
        }

        private void OnDestroy()
        {
            _bill.Changed -= OnChanged;
        }

        private void OnChanged()
        {
            if (_bill.Total != _shown)
            {
                Show();
            }
        }

        private void Show()
        {
            _shown = _bill.Total;
            label.text = string.Format(format, _shown);
            _pulse = 1f;
        }

        private void Update()
        {
            if (_pulse <= 0f)
            {
                return;
            }

            // Unscaled: hit-stops freeze the game, not the HUD.
            _pulse = Mathf.Max(0f, _pulse - Time.unscaledDeltaTime * PulseFade);
            label.rectTransform.localScale = Vector3.one * (1f + PulseScale * _pulse);
            label.color = Color.Lerp(Color.white, pulseColor, _pulse);
        }
    }
}
