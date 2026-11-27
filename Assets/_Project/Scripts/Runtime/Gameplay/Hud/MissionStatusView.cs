using _Project.Mission;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace _Project.Hud
{
    /// <summary>The line in the top-left corner: which call it is and, on the double call, how many fires still burn.</summary>
    public sealed class MissionStatusView : MonoBehaviour
    {
        [SerializeField] private Text label;

        private MissionDirector _mission;

        [Inject]
        public void Construct(MissionDirector mission)
        {
            _mission = mission;
        }

        private void Start()
        {
            _mission.StatusChanged += Show;
            Show();
        }

        private void OnDestroy()
        {
            _mission.StatusChanged -= Show;
        }

        private void Show()
        {
            var script = _mission.Script;
            var call = _mission.Stage + 1;
            var calls = _mission.StageCount;
            label.text = _mission.Phase switch
            {
                MissionPhase.Active when _mission.StageFires.Count > 1 =>
                    string.Format(script.statusCallFires, call, calls, _mission.BurningInStage),
                MissionPhase.Active => string.Format(script.statusCall, call, calls),
                MissionPhase.Between => string.Format(script.statusBetween, call, calls),
                MissionPhase.Fireworks or MissionPhase.Outro or MissionPhase.Done => script.statusDone,
                _ => string.Empty,
            };
        }
    }
}
