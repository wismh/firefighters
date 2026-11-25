using System.Collections.Generic;
using _Project.City;
using _Project.FireHose;
using _Project.Pedestrians;
using _Project.Player;
using _Project.Traffic;
using _Project.Vehicles;
using MyToolkit.Audio;
using MyToolkit.ParticleFx;
using UnityEngine;
using Zenject;

namespace _Project.Comedy
{
    /// <summary>
    /// The slapstick layer over the city. Listens to what the player does — crashes, props knocked over, traffic
    /// cars knocked out, drivers sprayed with the hose, buildings saved — and answers with comic words, emoji over
    /// the drivers, screen shake and freeze-frames, money flying out of what broke, and the bill for all of it.
    /// A knocked-over hydrant opens a geyser that throws cars and the player into the air.
    /// </summary>
    public sealed class ComedyDirector : MonoBehaviour
    {
        private const float HearingDistance = 70f;
        private const float BigTextCooldown = 0.35f;
        private const float SoakCooldown = 1.6f;
        private const float AirTimeForTrail = 0.2f;
        private const float AirTimeForCheer = 0.7f;
        private const int FinaleFireworks = 9;
        private const float FinaleSpacing = 0.45f;
        private const float ReportDuration = 14f;
        // Below this speed the player's car was hit rather than hitting.
        private const float RammedSpeed = 3f;
        private const float ScaredLineCooldown = 0.6f;

        private static readonly ComedyFxId[] SoakedFaces =
        {
            ComedyFxId.EmojiCry, ComedyFxId.EmojiTeary, ComedyFxId.EmojiDerp, ComedyFxId.EmojiClenchTeeth,
            ComedyFxId.EmojiDisappointed,
        };

        private static readonly ComedyFxId[] KnockedFaces =
        {
            ComedyFxId.EmojiAngry, ComedyFxId.EmojiShocked, ComedyFxId.EmojiNervous,
        };

        private static readonly ComedyFxId[] CrashWords = { ComedyFxId.TextPow, ComedyFxId.TextKaPow, ComedyFxId.TextCrack };
        private static readonly ComedyFxId[] Fireworks =
            { ComedyFxId.FireworkRed, ComedyFxId.FireworkBlue, ComedyFxId.FireworkYellow };

        private static readonly string[] SoakedLines =
        {
            "Я ж її щойно помив!", "Ей!!", "Моя зачіска!", "Це не я горю!", "Буль-буль...", "Вікно було відчинене!",
        };

        private static readonly string[] KnockedLines =
        {
            "Мій бампер!", "Ти права купив?!", "Я дзвоню мамі!", "Страховка не покриє!", "ЩО?!",
        };

        private static readonly string[] ScaredLines =
        {
            "ААААА!", "Рятуйте!", "Це ж тротуар!", "Я ще не застрахувався!", "Мамо!",
        };

        private static readonly string[] SoakedPedestrianLines =
        {
            "Я не горів!", "Буль!", "Це був новий костюм!", "Душ я не замовляв!",
        };

        private static readonly string[] GhostLines =
        {
            "Моральна шкода!", "Я буду скаржитись!", "Де ти вчився водити?!", "Поверніть мене назад!",
        };

        private static readonly string[] FlightLines = { "ЙІІІХУУ!", "Я лечууу!", "Вище за пожежу!" };

        private static readonly Color MoneyColor = new Color(1f, 0.35f, 0.3f);
        private static readonly Color GoodColor = new Color(0.45f, 1f, 0.45f);
        private static readonly Color GhostColor = new Color(0.75f, 0.9f, 1f);
        private static readonly Color ShoutColor = new Color(1f, 0.95f, 0.55f);

        [SerializeField] private PlayerModeController modes;
        [SerializeField] private FirstPersonController walker;
        [SerializeField] private CameraFeel feel;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private ComedySounds sounds = new ComedySounds();
        [Tooltip("World-space star trail behind a flying car.")]
        [SerializeField] private ParticleSystem flightTrail;

        [Header("Crashes")]
        [Tooltip("Dent depth, metres, from which a crash gets a comic word, a freeze-frame and a big shake.")]
        [SerializeField] private float bigCrashDepth = 0.12f;
        [SerializeField] private float hitStopSeconds = 0.07f;

        [Header("Hydrant geyser")]
        [SerializeField] private float geyserSeconds = 7f;
        [SerializeField] private float geyserHeight = 11f;
        [SerializeField] private float geyserRadius = 1.8f;
        [Tooltip("Upward acceleration on bodies in the column, m/s², against gravity's 9.8.")]
        [SerializeField] private float geyserLift = 26f;
        [SerializeField] private float geyserMaxRise = 13f;
        [SerializeField] private float geyserPlayerLaunch = 14f;

        [Header("Hose")]
        [Tooltip("Push of the water stream on loose bodies, newtons.")]
        [SerializeField] private float waterForce = 5000f;
        [SerializeField] private float waterMaxAcceleration = 10f;

        private readonly DamageBill _bill = new DamageBill();
        private readonly FloatingTexts _texts = new FloatingTexts();
        private readonly List<Geyser> _geysers = new List<Geyser>(8);
        private readonly Collider[] _overlap = new Collider[32];
        private readonly HashSet<Rigidbody> _pushed = new HashSet<Rigidbody>();
        private readonly Dictionary<Vehicle, float> _nextSoak = new Dictionary<Vehicle, float>(16);
        private readonly HashSet<Vehicle> _soaked = new HashSet<Vehicle>();
        private readonly List<ParticleSystem> _trailSystems = new List<ParticleSystem>(4);

        private IParticleFxSpawnService<ComedyFxId> _fx;
        private ISfxPlayer _sfx;
        private TrafficSystem _traffic;
        private FireService _fires;
        private WaterNozzle _nozzle;
        private List<KnockableProp> _props;
        private PedestrianSystem _pedestrians;
        private float _nextScaredLine;

        private Vehicle _driven;
        private float _lastBigText = float.NegativeInfinity;
        private float _airTime;
        private bool _cheered;
        private bool _trailOn = true;
        private int _finaleLeft;
        private float _finaleTimer;
        private float _reportUntil;
        private float _billPulse;
        private string _billText;
        private string _statsText;
        private GUIStyle _style;
        private GUIStyle _shadow;
        private GUIStyle _hud;
        private GUIStyle _hudShadow;

        private struct Geyser
        {
            public Vector3 Base;
            public float EndTime;
        }

        [Inject]
        public void Construct(IParticleFxSpawnService<ComedyFxId> fx, ISfxPlayer sfx, TrafficSystem traffic,
            FireService fires, WaterNozzle nozzle, List<KnockableProp> props, PedestrianSystem pedestrians)
        {
            _fx = fx;
            _sfx = sfx;
            _traffic = traffic;
            _fires = fires;
            _nozzle = nozzle;
            _props = props;
            _pedestrians = pedestrians;
        }

        private void Start()
        {
            flightTrail.GetComponentsInChildren(true, _trailSystems);
            SetTrail(false);
            foreach (var prop in _props)
            {
                prop.Knocked += OnPropKnocked;
            }

            _traffic.KnockedOut += OnKnockedOut;
            _fires.BuildingSaved += OnBuildingSaved;
            _pedestrians.Hit += OnPedestrianHit;
            _pedestrians.Scared += OnPedestrianScared;
            _pedestrians.Soaked += OnPedestrianSoaked;
            _pedestrians.GhostAppeared += OnGhostAppeared;
            modes.VehicleEntered += OnVehicleEntered;
            modes.VehicleExited += OnVehicleExited;
            if (modes.Driven != null)
            {
                OnVehicleEntered(modes.Driven);
            }

            UpdateBillText();
        }

        private void OnDestroy()
        {
            foreach (var prop in _props)
            {
                prop.Knocked -= OnPropKnocked;
            }

            _traffic.KnockedOut -= OnKnockedOut;
            _fires.BuildingSaved -= OnBuildingSaved;
            _pedestrians.Hit -= OnPedestrianHit;
            _pedestrians.Scared -= OnPedestrianScared;
            _pedestrians.Soaked -= OnPedestrianSoaked;
            _pedestrians.GhostAppeared -= OnGhostAppeared;
            modes.VehicleEntered -= OnVehicleEntered;
            modes.VehicleExited -= OnVehicleExited;
            if (_driven != null)
            {
                _driven.Crashed -= OnCrashed;
            }
        }

        private void OnVehicleEntered(Vehicle vehicle)
        {
            if (_driven == vehicle)
            {
                return;
            }

            if (_driven != null)
            {
                _driven.Crashed -= OnCrashed;
            }

            _driven = vehicle;
            _driven.Crashed += OnCrashed;
        }

        private void OnVehicleExited(Vehicle vehicle)
        {
            vehicle.Crashed -= OnCrashed;
            _driven = null;
            SetTrail(false);
        }

        private void OnCrashed(Vehicle vehicle, Vector3 point, float depth)
        {
            var cost = _bill.AddDent(depth);
            UpdateBillText();
            var big = depth >= bigCrashDepth;
            if (!big)
            {
                feel.Shake(0.15f + depth * 2f);
                Play(sounds.smallCrash, point, 0.7f);
                return;
            }

            feel.Shake(0.45f + depth);
            feel.HitStop(hitStopSeconds);
            Play(sounds.bigCrash, point);
            Play(sounds.bigCrashThud, point, 0.8f);
            _fx.SpawnParticle(Random.value < 0.5f ? ComedyFxId.Punch : ComedyFxId.ToonPunch, point);
            if (Time.unscaledTime - _lastBigText > BigTextCooldown)
            {
                _lastBigText = Time.unscaledTime;
                _fx.SpawnParticle(Pick(CrashWords), point + Vector3.up * 3.5f);
            }

            _texts.Add(point + Vector3.up * 5.5f, Money(cost), MoneyColor);
        }

        private void OnPropKnocked(KnockableProp prop)
        {
            var kind = prop.Kind;
            var cost = _bill.AddProp(kind);
            UpdateBillText();
            var position = prop.transform.position;
            _fx.SpawnParticle(ComedyFxId.DustPoof, position + Vector3.up * 0.3f);
            switch (kind)
            {
                case PropKind.Lamp:
                    _fx.SpawnParticle(ComedyFxId.MetalSparks, position + Vector3.up * 1.2f);
                    _fx.SpawnParticle(ComedyFxId.TextCrack, position + Vector3.up * 6f);
                    break;
                case PropKind.TrafficLight:
                    _fx.SpawnParticle(ComedyFxId.MetalSparks, position + Vector3.up * 1.2f);
                    _fx.SpawnParticle(ComedyFxId.TextZap, position + Vector3.up * 6f);
                    break;
                case PropKind.RoadSign:
                    _fx.SpawnParticle(ComedyFxId.TextPow, position + Vector3.up * 5f);
                    break;
                case PropKind.Hydrant:
                    OpenGeyser(position);
                    break;
                case PropKind.Bin:
                    _fx.SpawnParticle(ComedyFxId.Feathers, position + Vector3.up * 0.8f);
                    break;
            }

            if (cost >= 1000)
            {
                _fx.SpawnParticle(ComedyFxId.DollarBlast, position + Vector3.up * 1.5f);
            }

            feel.Shake(0.12f);
            Play(sounds.propDown, position);
            _texts.Add(position + Vector3.up * 8f, Money(cost), MoneyColor);
        }

        private void OpenGeyser(Vector3 position)
        {
            _geysers.Add(new Geyser { Base = position, EndTime = Time.time + geyserSeconds });
            _fx.SpawnParticle(ComedyFxId.Geyser, position);
            _fx.SpawnParticle(ComedyFxId.TextKaPow, position + Vector3.up * 5f);
            Play(sounds.geyser, position);
            _texts.Add(position + Vector3.up * 9f, "ФОНТАН!", ShoutColor, 1.2f);
        }

        private void OnKnockedOut(Vehicle vehicle)
        {
            var roof = vehicle.CameraTarget.position;
            _fx.SpawnParticle(ComedyFxId.StunStars, roof + Vector3.up * 0.6f);
            _fx.SpawnParticle(Pick(KnockedFaces), roof + Vector3.up * 2.6f);
            Play(sounds.knockout, roof);
            _texts.Add(roof + Vector3.up * 4f, Pick(KnockedLines), ShoutColor);
            // A traffic car that drove into the standing fire truck is its own driver's fault.
            if (modes.DrivingSpeed < RammedSpeed)
            {
                return;
            }

            var cost = _bill.AddKnockout();
            UpdateBillText();
            _texts.Add(roof + Vector3.up * 1.5f, Money(cost), MoneyColor, 0.8f);
        }

        private void OnPedestrianHit(Pedestrian pedestrian)
        {
            _bill.AddPedestrianHit();
            UpdateBillText();
            var position = pedestrian.Position;
            feel.Shake(0.3f);
            feel.HitStop(hitStopSeconds);
            Play(sounds.bigCrashThud, position);
            _fx.SpawnParticle(ComedyFxId.Punch, position + Vector3.up);
            _fx.SpawnParticle(Pick(CrashWords), position + Vector3.up * 3.5f);
        }

        private void OnPedestrianScared(Pedestrian pedestrian)
        {
            if (Time.time < _nextScaredLine)
            {
                return;
            }

            _nextScaredLine = Time.time + ScaredLineCooldown;
            _texts.Add(pedestrian.HeadPosition + Vector3.up, Pick(ScaredLines), ShoutColor);
        }

        private void OnPedestrianSoaked(Pedestrian pedestrian)
        {
            _bill.AddSoakedPedestrian();
            UpdateBillText();
            Play(sounds.driverSoaked, pedestrian.Position);
            _texts.Add(pedestrian.HeadPosition + Vector3.up, Pick(SoakedPedestrianLines), ShoutColor);
        }

        private void OnGhostAppeared(Ghost ghost)
        {
            var cost = _bill.AddMoralDamage();
            UpdateBillText();
            Play(sounds.knockout, ghost.Top);
            _texts.Add(ghost.Top + Vector3.up * 2f, Pick(GhostLines), GhostColor, 1.1f);
            _texts.Add(ghost.Top + Vector3.up, Money(cost), MoneyColor);
        }

        private void OnBuildingSaved(BuildingFire fire)
        {
            _bill.AddSavedBuilding();
            UpdateBillText();
            var area = fire.Area;
            var top = new Vector3(area.center.x, area.max.y, area.center.z);
            _fx.SpawnParticle(ComedyFxId.Confetti, top);
            _fx.SpawnParticle(ComedyFxId.CoinBlast, top + Vector3.up);
            Play(sounds.buildingSaved, top, 1f, true);
            Play(sounds.coins, top, 0.8f, true);
            _texts.Add(top + Vector3.up * 5f, "ВРЯТОВАНО!", GoodColor, 1.4f);
            if (_fires.Burning == 0)
            {
                _finaleLeft = FinaleFireworks;
                _finaleTimer = 1f;
                Play(sounds.allSaved, top, 1f, true);
            }
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            _texts.Tick(dt);
            _billPulse = Mathf.Max(0f, _billPulse - Time.unscaledDeltaTime * 3f);
            UpdateFlight(dt);
            UpdateSoaking();
            UpdateFinale(dt);
        }

        private void FixedUpdate()
        {
            PushWithWater();
            PushWithGeysers();
        }

        private void UpdateFlight(float dt)
        {
            var flying = _driven != null && _driven.IsAirborne;
            _airTime = flying ? _airTime + dt : 0f;
            if (!flying)
            {
                _cheered = false;
            }

            SetTrail(_airTime > AirTimeForTrail);
            if (_trailOn)
            {
                flightTrail.transform.position = _driven.CameraTarget.position;
            }

            if (_airTime > AirTimeForCheer && !_cheered)
            {
                _cheered = true;
                var roof = _driven.CameraTarget.position;
                _fx.SpawnParticle(ComedyFxId.EmojiCool, roof + Vector3.up * 2.6f);
                _texts.Add(roof + Vector3.up * 4.5f, Pick(FlightLines), ShoutColor, 1.2f);
            }
        }

        private void SetTrail(bool on)
        {
            if (on == _trailOn)
            {
                return;
            }

            _trailOn = on;
            for (var i = 0; i < _trailSystems.Count; i++)
            {
                var emission = _trailSystems[i].emission;
                emission.enabled = on;
            }

            if (on)
            {
                flightTrail.transform.position = _driven.CameraTarget.position;
                flightTrail.Play(true);
            }
        }

        // A traffic driver caught in the stream sticks an unhappy face out and complains.
        private void UpdateSoaking()
        {
            if (!_nozzle.IsWaterArriving)
            {
                return;
            }

            var body = _nozzle.Hit.rigidbody;
            if (body == null || !body.TryGetComponent<Vehicle>(out var vehicle) || vehicle == _driven)
            {
                return;
            }

            if (_nextSoak.TryGetValue(vehicle, out var next) && Time.time < next)
            {
                return;
            }

            _nextSoak[vehicle] = Time.time + SoakCooldown;
            var roof = vehicle.CameraTarget.position;
            _fx.SpawnParticle(Pick(SoakedFaces), roof + Vector3.up * 2.6f);
            _texts.Add(roof + Vector3.up * 4.5f, Pick(SoakedLines), ShoutColor);
            if (_soaked.Add(vehicle))
            {
                _bill.AddSoakedDriver();
                UpdateBillText();
                Play(sounds.driverSoaked, roof);
            }
        }

        private void PushWithWater()
        {
            if (!_nozzle.IsWaterArriving)
            {
                return;
            }

            var hit = _nozzle.Hit;
            var body = hit.rigidbody;
            if (body == null || body.isKinematic)
            {
                return;
            }

            var direction = (hit.point - _nozzle.transform.position).normalized;
            var force = Mathf.Min(waterForce, body.mass * waterMaxAcceleration);
            body.AddForceAtPosition(direction * force, hit.point, ForceMode.Force);
        }

        private void PushWithGeysers()
        {
            var now = Time.time;
            for (var i = _geysers.Count - 1; i >= 0; i--)
            {
                var geyser = _geysers[i];
                if (now > geyser.EndTime)
                {
                    _geysers.RemoveAt(i);
                    continue;
                }

                var top = geyser.Base + Vector3.up * geyserHeight;
                var count = Physics.OverlapCapsuleNonAlloc(geyser.Base, top, geyserRadius, _overlap, ~0,
                    QueryTriggerInteraction.Ignore);
                _pushed.Clear();
                for (var c = 0; c < count; c++)
                {
                    var body = _overlap[c].attachedRigidbody;
                    if (body == null || body.isKinematic || !_pushed.Add(body))
                    {
                        continue;
                    }

                    // Weaker towards the top of the column, so a car bobs on it instead of shooting off for ever.
                    var height = Mathf.Clamp01((body.worldCenterOfMass.y - geyser.Base.y) / geyserHeight);
                    if (body.linearVelocity.y < geyserMaxRise)
                    {
                        body.AddForce(Vector3.up * (geyserLift * (1f - height * 0.7f)), ForceMode.Acceleration);
                    }
                }

                LaunchWalker(geyser);
                _pedestrians.LaunchNear(geyser.Base, geyserRadius, geyserPlayerLaunch);
            }
        }

        private void LaunchWalker(Geyser geyser)
        {
            if (!walker.isActiveAndEnabled)
            {
                return;
            }

            var offset = walker.transform.position - geyser.Base;
            var inColumn = offset.y > -0.5f && offset.y < 1f &&
                           new Vector2(offset.x, offset.z).sqrMagnitude < geyserRadius * geyserRadius;
            if (inColumn)
            {
                walker.Launch(geyserPlayerLaunch);
            }
        }

        private void UpdateFinale(float dt)
        {
            if (_finaleLeft <= 0)
            {
                return;
            }

            _finaleTimer -= dt;
            if (_finaleTimer > 0f)
            {
                return;
            }

            _finaleTimer = FinaleSpacing;
            _finaleLeft--;
            var around = Random.insideUnitCircle.normalized * Random.Range(14f, 28f);
            var focus = modes.MapFocus.position;
            var position = focus + new Vector3(around.x, Random.Range(16f, 26f), around.y);
            _fx.SpawnParticle(Pick(Fireworks), position);
            Play(sounds.firework, position, 0.8f, true);
            if (_finaleLeft == 0)
            {
                _reportUntil = Time.time + ReportDuration;
            }
        }

        private void Play(SoundData sound, Vector3 at, float volume = 1f, bool everywhere = false)
        {
            var distance = Vector3.Distance(at, viewCamera.transform.position);
            var falloff = everywhere ? 1f : Mathf.Clamp01(1f - distance / HearingDistance);
            if (falloff > 0.05f)
            {
                _sfx.PlaySfx(sound, volume * falloff);
            }
        }

        private void UpdateBillText()
        {
            _billText = $"Рахунок за збитки: ${_bill.Total:N0}";
            _statsText = $"Врятовано: {_bill.BuildingsSaved}   Розбито: {_bill.PropsBroken}   " +
                         $"Збито машин: {_bill.CarsKnockedOut}   Облито: {_bill.DriversSoaked + _bill.PedestriansSoaked}\n" +
                         $"Прохожих у польоті: {_bill.PedestriansHit}";
            _billPulse = 1f;
        }

        private static string Money(int cost)
        {
            return $"-${cost:N0}";
        }

        private static T Pick<T>(T[] items)
        {
            return items[Random.Range(0, items.Length)];
        }

        private void OnGUI()
        {
            EnsureStyles();
            _texts.Draw(viewCamera, _style, _shadow);

            var size = Mathf.RoundToInt(20f + 8f * _billPulse);
            _hud.fontSize = size;
            _hudShadow.fontSize = size;
            _hud.normal.textColor = Color.Lerp(Color.white, MoneyColor, _billPulse);
            // Top centre: the fire counter has the left corner, the minimap the right one.
            var rect = new Rect(Screen.width * 0.5f - 300f, 10f, 600f, 34f);
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), _billText, _hudShadow);
            GUI.Label(rect, _billText, _hud);
            _hud.fontSize = 15;
            _hudShadow.fontSize = 15;
            _hud.normal.textColor = Color.white;
            var stats = new Rect(Screen.width * 0.5f - 300f, 42f, 600f, 48f);
            GUI.Label(new Rect(stats.x + 1f, stats.y + 1f, stats.width, stats.height), _statsText, _hudShadow);
            GUI.Label(stats, _statsText, _hud);

            if (Time.time < _reportUntil)
            {
                DrawReport();
            }
        }

        private void DrawReport()
        {
            const float width = 560f;
            const float height = 250f;
            var box = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.4f, width, height);
            GUI.Box(box, GUIContent.none);
            GUI.Box(box, GUIContent.none);
            _style.fontSize = 30;
            _style.normal.textColor = GoodColor;
            GUI.Label(new Rect(box.x, box.y + 14f, width, 40f), "МІСТО ВРЯТОВАНО!", _style);
            _style.fontSize = 19;
            _style.normal.textColor = Color.white;
            var y = box.y + 64f;
            GUI.Label(new Rect(box.x, y, width, 28f), $"Будинків врятовано: {_bill.BuildingsSaved}", _style);
            GUI.Label(new Rect(box.x, y + 28f, width, 28f),
                $"Ліхтарів, знаків і смітників знищено: {_bill.PropsBroken}", _style);
            GUI.Label(new Rect(box.x, y + 56f, width, 28f),
                $"Машин збито: {_bill.CarsKnockedOut}, облито: {_bill.DriversSoaked + _bill.PedestriansSoaked}, " +
                $"прохожих у польоті: {_bill.PedestriansHit}", _style);
            _style.fontSize = 24;
            _style.normal.textColor = MoneyColor;
            GUI.Label(new Rect(box.x, y + 96f, width, 34f), $"Рахунок пожежній частині: ${_bill.Total:N0}", _style);
            _style.fontSize = 15;
            _style.normal.textColor = new Color(1f, 1f, 1f, 0.7f);
            GUI.Label(new Rect(box.x, y + 140f, width, 24f), Verdict(), _style);
        }

        private string Verdict()
        {
            if (_bill.Total == 0)
            {
                return "Жодної подряпини. Ви взагалі пожежник?";
            }

            if (_bill.Total < 5000)
            {
                return "Майже акуратно. Мер навіть не помітить.";
            }

            return _bill.Total < 30000
                ? "Пожежі згашено. Бюджет міста — ні."
                : "Пожежа завдала б менше шкоди.";
        }

        private void EnsureStyles()
        {
            if (_style != null)
            {
                return;
            }

            _style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _shadow = new GUIStyle(_style);
            _hud = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, fontStyle = FontStyle.Bold };
            _hudShadow = new GUIStyle(_hud);
            _hudShadow.normal.textColor = new Color(0f, 0f, 0f, 0.75f);
        }
    }
}
