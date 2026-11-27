using _Project.Economy;
using _Project.Mission;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace _Project.Hud
{
    /// <summary>The closing report after the dispatcher's last words: what was saved, what was broken, the bill.</summary>
    public sealed class MissionReportView : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Text title;
        [SerializeField] private Text saved;
        [SerializeField] private Text props;
        [SerializeField] private Text cars;
        [SerializeField] private Text bill;
        [SerializeField] private Text verdict;
        [SerializeField] private Text passed;

        private MissionDirector _mission;
        private DamageBill _bill;
        private float _hideAt = float.PositiveInfinity;

        [Inject]
        public void Construct(MissionDirector mission, DamageBill damageBill)
        {
            _mission = mission;
            _bill = damageBill;
        }

        private void Start()
        {
            panel.SetActive(false);
            _mission.Completed += Show;
        }

        private void OnDestroy()
        {
            _mission.Completed -= Show;
        }

        private void Show()
        {
            var script = _mission.Script;
            title.text = script.reportTitle;
            saved.text = string.Format(script.reportSaved, _bill.BuildingsSaved);
            props.text = string.Format(script.reportProps, _bill.PropsBroken);
            cars.text = string.Format(script.reportCars, _bill.CarsKnockedOut,
                _bill.DriversSoaked + _bill.PedestriansSoaked, _bill.PedestriansHit);
            bill.text = string.Format(script.reportBill, _bill.Total);
            verdict.text = script.VerdictFor(_bill.Total);
            passed.text = script.reportPassed;
            panel.SetActive(true);
            _hideAt = Time.time + script.reportSeconds;
        }

        private void Update()
        {
            if (Time.time < _hideAt)
            {
                return;
            }

            _hideAt = float.PositiveInfinity;
            panel.SetActive(false);
        }
    }
}
